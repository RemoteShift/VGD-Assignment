using UnityEngine;

// The front-row performer gives the cue on the host's unfinished line.
// The house answers on the next bar, even if the reader leaves the console open.
public class StageApplause : MonoBehaviour
{
    public DialogueManager dialogue;
    public Transform chest, leftElbow, leftHand, rightElbow, rightHand;
    public AudioClip firstClaps, crowdClaps;
    public Light houseLight;
    private float elapsed;
    private bool started, leadHeard;
    private float nextHouseBar = Bar;
    private const float Bar = 1.48f;

    private void LateUpdate()
    {
        if (StoryStateManager.Visit == StoryStateManager.Chapter.End)
        {
            houseLight.enabled = true;
            return;
        }
        if (!started)
        {
            if (dialogue.Current == null || !dialogue.Current.startsApplause) return;
            started = true;
            houseLight.enabled = true;
        }
        if (StoryStateManager.Visit != StoryStateManager.Chapter.Finale) return;
        elapsed += Time.deltaTime;
        float beat = elapsed - .28f;
        if (beat < 0) return;
        if (!leadHeard)
        {
            dialogue.voice.PlayOneShot(firstClaps);
            leadHeard = true;
        }
        if (beat >= nextHouseBar)
        {
            dialogue.voice.PlayOneShot(crowdClaps, nextHouseBar == Bar ? 1f : .65f);
            nextHouseBar += 2 * Bar;
        }

        // Bring both hands together at the same three offsets used by the bell and clap recordings.
        float phase = Mathf.Repeat(beat, Bar);
        float separation = Mathf.Min(Mathf.Min(phase, Bar - phase),
            Mathf.Min(Mathf.Abs(phase - .28f), Mathf.Abs(phase - .79f)));
        float spread = Mathf.Lerp(.012f, .25f, Mathf.SmoothStep(0, 1, separation / .15f));
        var centre = chest.position + Vector3.up * .04f + transform.forward * .36f;
        PointForearm(leftElbow, leftHand, centre - transform.right * spread);
        PointForearm(rightElbow, rightHand, centre + transform.right * spread);
    }

    private static void PointForearm(Transform elbow, Transform hand, Vector3 target)
    {
        elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, target - elbow.position) * elbow.rotation;
    }
}
