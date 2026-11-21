using UnityEngine;

// Only narrative memory crosses scene boundaries. Every arena load gets fresh physical objects.
public static class StoryStateManager
{
    public enum Chapter { Menu, Intro, Garden, Intermission, Castle, Finale, End }
    public enum Breath { None, Canister, Bulb }
    public static Chapter Visit { get; private set; }
    public static Breath FirstBreath { get; private set; }
    public static bool HasCanister { get; private set; }
    public static bool HasBulb { get; private set; }
    private static bool castleCanister, castleBulb;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetRun()
    {
        Visit = Chapter.Menu;
        FirstBreath = Breath.None;
        HasCanister = HasBulb = castleCanister = castleBulb = false;
    }

    public static void Enter(Chapter chapter)
    {
        Visit = chapter;
        if (chapter == Chapter.Garden)
        {
            FirstBreath = Breath.None;
            HasCanister = HasBulb = false;
        }
        if (chapter == Chapter.Intermission)
        {
            castleCanister = HasCanister;
            castleBulb = HasBulb;
        }
        if (chapter == Chapter.Castle)
        {
            HasCanister = castleCanister;
            HasBulb = castleBulb;
        }
    }

    // A completed refill is valid at the oxygen cap. Only the first Garden receipt selects a line.
    public static bool RecordRefill(Breath source)
    {
        if (Visit != Chapter.Garden && Visit != Chapter.Castle) return false;
        HasCanister |= source == Breath.Canister;
        HasBulb |= source == Breath.Bulb;
        if (Visit != Chapter.Garden || FirstBreath != Breath.None) return false;
        FirstBreath = source;
        return true;
    }
}
