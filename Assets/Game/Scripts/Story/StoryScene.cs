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
    public bool ExitOpen => IsArena && remaining.Count == 0 &&
        (location == Location.Castle ? BellRung : StoryStateManager.FirstBreath != Breath.None);
    public bool AtOpenExit => PlayingArena && ExitOpen && Vector3.Distance(player.transform.position, exit.position) <= 3.1f;
    public int Targets => remaining.Count;
    private readonly HashSet<EncounterGuard> remaining = new();
    private bool engaged;
    private float nextBreath;

    private void Awake() => Active = this;
    private void OnDestroy() { if (Active == this) Active = null; }

    private void Start()
    {
        AudioListener.pause = false;
        Time.timeScale = 1;
        if (IsArena)
        {
            StoryStateManager.Enter(location == Location.Garden ? Chapter.Garden : Chapter.Castle);
            foreach (var guard in guards)
            {
                remaining.Add(guard);
                guard.gameObject.SetActive(false);
            }
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
                Objective = "Reach the well.";
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
        Objective = chapter == Chapter.Finale ? "Listen to the crowd." : "Advance the Stage console.";
        // Rebind selected shots per visit while retaining the Stage director, host cameras and signal track.
        var director = dialogue.director;
        foreach (var track in ((TimelineAsset)director.playableAsset).GetOutputTracks())
        foreach (var clip in track.GetClips())
        {
            if (clip.asset is not CinemachineShot shot) continue;
            CinemachineCamera replacement = null;
            if (chapter != Chapter.Intermission && clip.displayName == "Low Camera") replacement = cuffsShot;
            if (chapter == Chapter.Intro && clip.displayName == "High Camera") replacement = crestShot;
            if (chapter == Chapter.Finale && clip.displayName == "Default") replacement = crowdShot;
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
        if (location == Location.Castle && !engaged && Vector3.Distance(player.transform.position, well.position) < 8f)
        {
            dialogue.Show("notice");
            Engage();
        }
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
        Engage();
    }

    private void Engage()
    {
        engaged = true;
        foreach (var guard in remaining) guard.Arm(this, player.transform);
        Objective = "Defeat the guards.";
    }

    public void GuardFell(EncounterGuard guard)
    {
        if (!remaining.Remove(guard) || remaining.Count != 0) return;
        if (location == Location.Castle)
        {
            Objective = "Ring the bell.";
            dialogue.Show("wait");
        }
        else OpenExit("portal", "Return to the portal.");
    }

    public void StrikeBell()
    {
        if (!PlayingArena || BellRung) return;
        if (remaining.Count != 0)
        {
            dialogue.voice.PlayOneShot(dialogue.book.clank);
            dialogue.Show("early");
            return;
        }
        BellRung = true;
        dialogue.voice.PlayOneShot(dialogue.book.bell);
        OpenExit("home", "Use the return point.");
    }

    private void OpenExit(string key, string objective)
    {
        Objective = objective;
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
