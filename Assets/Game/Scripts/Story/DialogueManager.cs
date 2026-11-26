using System;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Opening subtitles follow the reel. Later Stage visits wait for the reader, and arena captions leave controls live.
public class DialogueManager : MonoBehaviour
{
    public StoryBook book;
    public PlayableDirector director;
    public AudioSource voice;
    private StoryBook.Line[] lines;
    private int index;
    private float age;
    private Action finished;
    private bool opening, reelStarted;
    private float backstageTime;
    private CinemachineCamera cuffs, crest;
    public bool CanAdvance => Current != null && (!opening || index + 1 < lines.Length || VisibleCount < Current.text.Length);
    public StoryBook.Line Current => lines == null ? null : lines[index];
    public bool IsConsole => finished != null;
    public string VisibleText => Current == null ? "" : Current.text.Substring(0, VisibleCount);
    private int VisibleCount => Mathf.Min(Current.text.Length, Mathf.FloorToInt(age * 52));

    public void Show(string key, Action onComplete = null)
    {
        lines = book.Read(key);
        finished = onComplete;
        index = 0;
        ShowLine();
    }

    // Negative cues are two backstage inserts. From zero onward the original director owns
    // every shot and light. Reading ahead changes the subtitle, never the running Timeline.
    public void ShowOpening(CinemachineCamera cuffsCamera, CinemachineCamera crestCamera, Action onComplete)
    {
        cuffs = cuffsCamera;
        crest = crestCamera;
        opening = true;
        reelStarted = false;
        backstageTime = book.Read("intro")[0].shotTime;
        director.extrapolationMode = DirectorWrapMode.Hold;
        Show("intro", onComplete);
    }

    private void ShowLine()
    {
        age = 0;
        if (Current.sound) voice.PlayOneShot(Current.sound);
        if (opening)
        {
            cuffs.Priority = index == 0 ? 200 : 0;
            crest.Priority = index == 1 ? 200 : 0;
            if (!reelStarted && Current.shotTime >= 0)
            {
                reelStarted = true;
                director.time = 0;
                director.Play();
            }
        }
        else if (director && IsConsole)
        {
            SeekStage(Current.shotTime);
            director.Resume();
        }
    }

    private void Update()
    {
        if (Current == null) return;
        age += Time.deltaTime;
        if (opening)
        {
            if (!reelStarted) backstageTime += Time.deltaTime;
            double cueTime = reelStarted ? director.time : backstageTime;
            if (index + 1 < lines.Length && cueTime >= lines[index + 1].shotTime) Next();
            else if (reelStarted && director.time >= director.duration - .025) Complete();
            return;
        }
        if (!IsConsole && age >= Mathf.Max(4, Current.text.Length / 13f)) Next();
        // Hold this shot until the reader advances, even if they linger over the line.
        if (director && IsConsole && director.time >= Current.shotTime + 1.1)
            director.Pause();
    }

    private void SeekStage(double time)
    {
        // These existing signals switch spotlights on. Apply missed cues even when a reader
        // advances across a marker before it has played in real time.
        foreach (var track in ((TimelineAsset)director.playableAsset).GetOutputTracks())
        {
            if (track is not SignalTrack || director.GetGenericBinding(track) is not SignalReceiver receiver)
                continue;
            foreach (var marker in track.GetMarkers())
                if (marker is SignalEmitter signal && signal.time <= time && signal.asset)
                    receiver.GetReaction(signal.asset)?.Invoke();
        }
        director.time = time;
        director.Evaluate();
    }

    public void Advance()
    {
        if (!IsConsole || !CanAdvance) return;
        if (VisibleCount < Current.text.Length) age = Current.text.Length / 52f + .01f;
        else
        {
            if (opening && !reelStarted && lines[index + 1].shotTime < 0)
                backstageTime = lines[index + 1].shotTime;
            Next();
        }
    }

    private void Next()
    {
        if (++index < lines.Length) ShowLine();
        else Complete();
    }

    public void Complete()
    {
        bool reelFinished = opening && reelStarted && director.time >= director.duration - .025;
        if (opening)
        {
            cuffs.Priority = 0;
            crest.Priority = 0;
            // A backstage skip still needs a graph to evaluate the terminal light and camera cues.
            if (!reelStarted) director.Play();
            opening = false;
        }
        var callback = finished;
        lines = null;
        finished = null;
        if (director)
        {
            // A skip and a normal finish hold the same terminal shot and the same light cues.
            if (!reelFinished) SeekStage(director.duration - .025);
            director.Pause();
        }
        callback?.Invoke();
    }
}
