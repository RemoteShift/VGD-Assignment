using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Stage lines wait for the player. Arena captions expire on game time and leave controls live.
public class DialogueManager : MonoBehaviour
{
    public StoryBook book;
    public PlayableDirector director;
    public AudioSource voice;
    private StoryBook.Line[] lines;
    private int index;
    private float age;
    private Action finished;
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

    private void ShowLine()
    {
        age = 0;
        if (Current.sound) voice.PlayOneShot(Current.sound);
        if (director && IsConsole)
        {
            SeekStage(Current.shotTime);
            director.Resume();
        }
    }

    private void Update()
    {
        if (Current == null) return;
        age += Time.deltaTime;
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
        if (!IsConsole) return;
        if (VisibleCount < Current.text.Length) age = Current.text.Length / 52f + .01f;
        else Next();
    }

    private void Next()
    {
        if (++index < lines.Length) ShowLine();
        else Complete();
    }

    public void Complete()
    {
        var callback = finished;
        lines = null;
        finished = null;
        if (director)
        {
            // A skip and a normal finish hold the same terminal shot and the same light cues.
            SeekStage(director.duration - .05);
            director.Pause();
        }
        callback?.Invoke();
    }
}
