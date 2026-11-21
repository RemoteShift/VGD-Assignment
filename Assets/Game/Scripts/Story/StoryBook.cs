using UnityEngine;

// Each cue is editable in the Inspector. Shot times refer to the existing Stage Timeline.
[CreateAssetMenu(menuName = "Dash and Slash/Story Book")]
public class StoryBook : ScriptableObject
{
    [System.Serializable]
    public class Line
    {
        public string speaker;
        public Texture2D portrait;
        [TextArea(2, 5)] public string text;
        public AudioClip sound;
        public float shotTime;
        public bool startsApplause;
    }
    [System.Serializable]
    public class Conversation
    {
        public string key;
        public Line[] lines;
    }
    public Conversation[] conversations;
    public AudioClip clank, bell, breath;
    public Line[] Read(string key) => System.Array.Find(conversations, c => c.key == key).lines;
}
