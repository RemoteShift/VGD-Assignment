using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Chapter = StoryStateManager.Chapter;
using Breath = StoryStateManager.Breath;

// Scene references are authored in each level. Only chapter and inventory survive a load.
public class StoryScene : MonoBehaviour
{
    public enum Location { Menu, Stage, Garden, Castle }
    public enum Panel { None, Pause, Inventory, Ending }
    public Location location;
    public DialogueManager dialogue;
    public LocomotionController player;
    public EncounterGuard[] guards;
    public EncounterGuard[] secondWatch, lastWatch;
    public Transform station;
    public GameObject carriedClapper, fittedClapper;
    public Transform valveWheel, bellPivot, portcullis;
    public ParticleSystem pressureVent;
    public Transform spawn, exit, well, canister, flower, rope;
    public GameObject exitLight;
    public CinemachineCamera cuffsShot, crestShot, crowdShot;
    public static StoryScene Active { get; private set; }
    public Panel ScreenPanel { get; private set; }
    public string Objective { get; private set; }
    public string Card { get; private set; }
    public float Fade { get; private set; }
    public bool IsLoading => Card != null;
    public bool IsArena => location == Location.Garden || location == Location.Castle;
    public bool PlayingArena => IsArena && !IsLoading && ScreenPanel == Panel.None && player.canMove;
    public bool BellRung { get; private set; }
    // Each location has three distinct posts. A cleared post never empties the next one's roster.
    public enum Beat { Arrival, FirstFight, Station, SecondFight, Approach, LastFight, Bell, Return }
    public Beat Progress { get; private set; }
    public bool ExitOpen => Progress == Beat.Return;
    public bool AtOpenExit => PlayingArena && ExitOpen && Near(exit, 3.1f);
    public int Targets => remaining.Count;
    public bool HasClapper => location == Location.Castle && Progress >= Beat.SecondFight && Progress <= Beat.Approach;
    public bool AtStation => PlayingArena && Progress == Beat.Station && Near(station, 3f);
    public bool AtBellSocket => PlayingArena && location == Location.Castle && Progress == Beat.Approach && Near(rope, 3.1f);
    public Transform Destination => Progress == Beat.Station ? station : Progress == Beat.Approach ?
        (location == Location.Garden ? exit : rope) : Progress == Beat.Bell ? rope : Progress == Beat.Return ? exit : null;
    private readonly HashSet<EncounterGuard> remaining = new();
    public float LastHit { get; private set; } = -10f;
    public void RegisterHit() => LastHit = Time.time;
    private bool sawNotice;
    private float nextBreath, turnWheel, ringTime;
    private bool Near(Transform point, float range) => Vector3.Distance(player.transform.position, point.position) <= range;

    private void Awake() => Active = this;
    private void OnDestroy() { if (Active == this) Active = null; }

    private void Start()
    {
        AudioListener.pause = false;
        Time.timeScale = 1;
        if (IsArena)
        {
            StoryStateManager.Enter(location == Location.Garden ? Chapter.Garden : Chapter.Castle);
            foreach (var squad in new[] { guards, secondWatch, lastWatch })
                foreach (var guard in squad) guard.gameObject.SetActive(false);
            if (fittedClapper) fittedClapper.SetActive(false);
            exitLight.SetActive(false);
            player.ResetPositionRotation(spawn.position, Quaternion.identity);
            CameraLook.Instance.ResetRotation(spawn.eulerAngles.y, spawn.eulerAngles.x);
            SetControls(true);
            if (location == Location.Garden)
            {
                Objective = "Refill your oxygen.";
                dialogue.Show("garden");
            }
            else
            {
                StartFight(Beat.FirstFight, guards, "Break the gate watch.");
                dialogue.Show("castle");
            }
            return;
        }
        SetControls(false);
        if (location == Location.Menu) { StoryStateManager.ResetRun(); return; }
        if (StoryStateManager.Visit != Chapter.Intermission && StoryStateManager.Visit != Chapter.Finale)
            StoryStateManager.Enter(Chapter.Intro);
        StartStage();
    }

    private void StartStage()
    {
        var chapter = StoryStateManager.Visit;
        string key = chapter == Chapter.Intro ? "intro" : chapter == Chapter.Finale ? "finale" :
            StoryStateManager.FirstBreath == Breath.Bulb ? "interBulb" : "interCanister";
        Objective = chapter == Chapter.Finale ? "Listen for an answer." : chapter == Chapter.Intro ?
            "Hear the Ringmaster’s terms." : "Hold him to his promise.";
        if (chapter == Chapter.Intro)
        {
            dialogue.ShowOpening(cuffsShot, crestShot, FinishStage);
            return;
        }
        // Rebind selected shots per visit while retaining the Stage director, host cameras and signal track.
        var director = dialogue.director;
        foreach (var track in ((TimelineAsset)director.playableAsset).GetOutputTracks())
        foreach (var clip in track.GetClips())
        {
            if (clip.asset is not CinemachineShot shot) continue;
            CinemachineCamera replacement = null;
            if (chapter != Chapter.Intermission && clip.displayName == "Low Camera") replacement = cuffsShot;
            if (clip.displayName == "Default") replacement = crowdShot;
            if (replacement) director.SetReferenceValue(shot.VirtualCamera.exposedName, replacement);
        }
        director.Play();
        dialogue.Show(key, FinishStage);
    }

    private void Update()
    {
        if (IsLoading) return;
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && location != Location.Menu && ScreenPanel != Panel.Ending)
        {
            if (ScreenPanel == Panel.None) Pause();
            else if (ScreenPanel == Panel.Inventory) ScreenPanel = Panel.Pause;
            else Resume();
            return;
        }
        if (ScreenPanel != Panel.None) return;
        if (dialogue.IsConsole && keyboard != null)
        {
            if (keyboard.tabKey.wasPressedThisFrame) dialogue.Complete();
            else if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) dialogue.Advance();
        }
        if (!PlayingArena) return;
        if (location == Location.Castle && !sawNotice && Progress == Beat.Approach && Near(well, 8f))
        {
            dialogue.Show("notice");
            sawNotice = true;
        }
        if (location == Location.Garden && Progress == Beat.Approach && Near(exit, 11f))
            StartFight(Beat.LastFight, lastWatch, "Clear the portal watch.");
        AnimateLandmarks();
        if (player.Vitals.OxygenFraction < .25f && Time.time >= nextBreath)
        {
            dialogue.voice.PlayOneShot(dialogue.book.breath, .8f);
            nextBreath = Time.time + 1.5f;
        }
        if (player.transform.position.y < spawn.position.y - 9) Defeated(false);
    }

    public void Refilled(Breath source)
    {
        if (!PlayingArena || !StoryStateManager.RecordRefill(source)) return;
        dialogue.Show(source == Breath.Canister ? "canister" : "bulb");
        StartFight(Beat.FirstFight, guards, "Defeat the bottle guards.");
    }

    private void StartFight(Beat beat, EncounterGuard[] squad, string objective)
    {
        Progress = beat;
        Objective = objective;
        foreach (var guard in squad)
        {
            remaining.Add(guard);
            guard.Arm(this, player.transform);
        }
    }

    public void GuardFell(EncounterGuard guard)
    {
        if (!remaining.Remove(guard) || remaining.Count > 0) return;
        bool garden = location == Location.Garden;
        switch (Progress)
        {
            case Beat.FirstFight:
                Progress = Beat.Station;
                Objective = garden ? "Open the air main." : "Take the bell clapper.";
                dialogue.Show(garden ? "valveReady" : "clapperReady");
                break;
            case Beat.SecondFight:
                Progress = Beat.Approach;
                Objective = garden ? "Reach the portal arch." : "Refit the bell clapper.";
                dialogue.Show(garden ? "portalWatch" : "wellRoad");
                break;
            case Beat.LastFight:
                if (garden) OpenExit("portal", "Return to the portal.");
                else
                {
                    Progress = Beat.Bell;
                    Objective = "Ring the bell.";
                    dialogue.Show("wait");
                }
                break;
        }
    }

    // Use and sword remain separate inputs. Neither an early hit nor a second press can skip a post.
    public void UseLandmark()
    {
        if (AtStation)
        {
            bool garden = location == Location.Garden;
            if (garden)
            {
                player.Vitals.RestoreOxygen(player.Vitals.maxOxygen);
                pressureVent.Play();
                pressureVent.GetComponent<AudioSource>().Play();
                turnWheel = 240f;
            }
            else carriedClapper.SetActive(false);
            dialogue.voice.PlayOneShot(dialogue.book.clank);
            dialogue.Show(garden ? "valveUsed" : "clapperTaken");
            StartFight(Beat.SecondFight, secondWatch, garden ? "Defend the air main." : "Clear the market lane.");
        }
        else if (AtBellSocket)
        {
            fittedClapper.SetActive(true);
            dialogue.voice.PlayOneShot(dialogue.book.clank);
            dialogue.Show("clapperFitted");
            StartFight(Beat.LastFight, lastWatch, "Hold the village well.");
        }
    }

    private void AnimateLandmarks()
    {
        // The Castle has a grille on each face of the arch. Both travel on the same hoist.
        if (portcullis && Progress >= Beat.Approach)
            portcullis.localPosition = Vector3.MoveTowards(portcullis.localPosition, Vector3.up * 1.6f, 2.6f * Time.deltaTime);
        if (turnWheel > 0f)
        {
            float angle = Mathf.Min(turnWheel, 150 * Time.deltaTime);
            valveWheel.Rotate(Vector3.forward, angle, Space.Self);
            turnWheel -= angle;
        }
        if (BellRung && ringTime < 3f)
        {
            ringTime = Mathf.Min(3f, ringTime + Time.deltaTime);
            float angle = Mathf.Sin(ringTime * 11) * 22 * Mathf.Exp(-1.4f * ringTime);
            bellPivot.localRotation = Quaternion.Euler(0, 0, ringTime >= 3f ? 0 : angle);
        }
    }

    public void StrikeBell()
    {
        if (!PlayingArena || BellRung) return;
        if (Progress != Beat.Bell)
        {
            dialogue.voice.PlayOneShot(dialogue.book.clank);
            dialogue.Show(Progress == Beat.LastFight ? "early" : "emptyBell");
            return;
        }
        BellRung = true;
        dialogue.voice.PlayOneShot(dialogue.book.bell);
        OpenExit("home", "Use the return point.");
    }

    private void OpenExit(string key, string objective)
    {
        Objective = objective;
        Progress = Beat.Return;
        exitLight.SetActive(true);
        dialogue.Show(key);
    }

    public void ReturnToStage()
    {
        if (AtOpenExit) Travel(location == Location.Garden ? Chapter.Intermission : Chapter.Finale);
    }

    private void FinishStage()
    {
        if (IsLoading) return;
        if (StoryStateManager.Visit == Chapter.Finale)
        {
            StoryStateManager.Enter(Chapter.End);
            ScreenPanel = Panel.Ending;
            SetControls(false);
        }
        else Travel(StoryStateManager.Visit == Chapter.Intro ? Chapter.Garden : Chapter.Castle);
    }

    public void NewRun()
    {
        if (IsLoading) return;
        StoryStateManager.ResetRun();
        Travel(Chapter.Intro);
    }
    public void MainMenu() => Travel(Chapter.Menu);
    public void Retry()
    {
        if (IsArena) Travel(location == Location.Garden ? Chapter.Garden : Chapter.Castle, "Arena retry");
    }
    public void Defeated(bool faint)
    {
        if (IsArena) Travel(location == Location.Garden ? Chapter.Garden : Chapter.Castle,
            faint ? "Oxygen depleted   /   Regaining consciousness" : "Defeated   /   Resetting the arena");
    }
    public void Pause()
    {
        if (IsLoading) return;
        ScreenPanel = Panel.Pause;
        AudioListener.pause = true;
        Time.timeScale = 0;
        SetControls(false);
    }
    public void Inventory() => ScreenPanel = Panel.Inventory;
    public void Back() => ScreenPanel = Panel.Pause;
    public void Resume()
    {
        ScreenPanel = Panel.None;
        AudioListener.pause = false;
        Time.timeScale = 1;
        SetControls(IsArena);
    }

    private void SetControls(bool on)
    {
        if (player)
        {
            player.canMove = on;
            player.GetComponent<PlayerInteraction>().enabled = on;
            player.GetComponent<CombatController>().enabled = on;
            var look = player.GetComponentInChildren<CameraLook>();
            if (on) look.LockCursor(); else look.UnlockCursor();
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Travel(Chapter destination, string title = null)
    {
        if (IsLoading) return;
        string scene, act;
        switch (destination)
        {
            case Chapter.Garden: scene = "Arena 1 - The Garden"; act = "Act I   /   The Garden"; break;
            case Chapter.Castle: scene = "Arena 2 - Castle"; act = "Act II   /   The Captured Castle"; break;
            case Chapter.Menu: scene = "Main Menu"; act = "Back to the marquee"; break;
            default: scene = "Stage"; act = destination == Chapter.Intro ? "Opening   /   Behind the curtain" :
                destination == Chapter.Intermission ? "Intermission   /   On cue" : "Finale   /   The last sound"; break;
        }
        Card = title ?? act;
        SetControls(false);
        AudioListener.pause = false;
        Time.timeScale = 1;
        StartCoroutine(Load(scene, destination));
    }

    private IEnumerator Load(string scene, Chapter destination)
    {
        while (Fade < 1)
        {
            Fade = Mathf.Min(1, Fade + Time.unscaledDeltaTime * 3);
            yield return null;
        }
        var operation = SceneManager.LoadSceneAsync(scene);
        operation.allowSceneActivation = false;
        yield return new WaitForSecondsRealtime(.65f);
        StoryStateManager.Enter(destination);
        operation.allowSceneActivation = true;
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
