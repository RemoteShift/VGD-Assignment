using UnityEngine;

// Only CombatController's sword sweep invokes this target. Area attacks cannot ring the bell.
public class BellRope : MonoBehaviour
{
    public StoryScene encounter;
    public void Strike() => encounter.StrikeBell();
}
