using UnityEngine;

// All rectangles use a common scaled coordinate system so the console and controls stay together.
public class StoryPresenter : MonoBehaviour
{
    public StoryScene story;
    public Texture2D marquee;
    public Font displayFont;
    private readonly Color ink = new(.045f,.055f,.073f,.94f);
    private readonly Color gold = new(.97f,.74f,.38f);
    private readonly Color paper = new(.96f,.95f,.89f);
    private GUIStyle type;
    private float w,h;

    private void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 1180f, Screen.height / 680f);
        w = Screen.width / scale; h = Screen.height / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        type ??= new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset() };
        if (story.IsLoading)
        {
            Fill(0,0,w,h,new Color(.025f,.029f,.045f,story.Fade));
            Label(w*.13f,h*.45f,w*.75f,100,story.Card,36,paper);
            return;
        }
        if (story.location == StoryScene.Location.Menu || story.ScreenPanel != StoryScene.Panel.None) { Menu(); return; }
        if (!string.IsNullOrEmpty(story.Objective))
        {
            bool choice = story.location == StoryScene.Location.Garden && story.Progress == StoryScene.Beat.Arrival;
            float height = choice ? 105 : 75;
            Fill(24,22,402,height,ink); Fill(24,22,4,height,gold);
            Label(43,30,360,22,story.location == StoryScene.Location.Garden ? "ACT I    /    THE GARDEN" :
                story.location == StoryScene.Location.Castle ? "ACT II    /    THE CAPTURED CASTLE" : "ON AIR    /    THE STAGE",14,gold);
            string count = story.Targets > 0 ? "   ·   " + story.Targets : "";
            Label(42,53,379,36,story.Objective + count,23,paper);
            if (choice) Label(43,90,370,28,"E   Use bottle       LMB   Strike nearby flower",16,paper);
        }
        if (story.IsArena)
        {
            if (Time.time - story.LastHit < .17f)
                Label(w*.5f-18,h*.5f-19,36,38,"×",30,gold,TextAnchor.MiddleCenter);
            Label(w-230,24,205,28,"ESC   /   PAUSE",15,paper,TextAnchor.MiddleRight);
            if (story.location == StoryScene.Location.Garden && StoryStateManager.FirstBreath == StoryStateManager.Breath.None)
            {
                Mark(story.canister,"E   •   USE CANISTER",1.3f);
                Mark(story.flower,"LMB   •   CUT FLOWER",1.05f);
            }
            if (story.Destination)
            {
                string label = story.Progress switch
                {
                    StoryScene.Beat.Station => story.location == StoryScene.Location.Garden ? "AIR MAIN" : "BELL CLAPPER",
                    StoryScene.Beat.Approach => story.location == StoryScene.Location.Garden ? "PORTAL ARCH" : "VILLAGE WELL",
                    StoryScene.Beat.Bell => "STRIKE THE ROPE",
                    _ => "RETURN"
                };
                Mark(story.Destination, label, 1.45f, true);
            }
        }
        var dialogue = story.dialogue;
        if (dialogue.Current == null) return;
        bool console = dialogue.IsConsole;
        float width = console ? 900 : 646;
        float x = console ? (w-width)/2 : w-width-28;
        float y = h-(console ? 192 : 146);
        var line = dialogue.Current;
        Color accent = line.speaker == "Ringmaster" ? gold : line.speaker == "Lead Spectator" ?
            new Color(.55f,.85f,.68f) : new Color(.62f,.81f,.95f);
        Fill(x,y,width,console ? 171 : 124,ink);
        Fill(x,y,4,console ? 171 : 124,accent);
        Fill(x+19,y+17,80,80,new Color(.15f,.18f,.23f));
        if (line.portrait) GUI.DrawTexture(new Rect(x+21,y+19,76,76),line.portrait,ScaleMode.ScaleToFit);
        Label(x+117,y+14,width-147,25,line.speaker.ToUpperInvariant(),16,accent);
        Label(x+116,y+43,width-144,console ? 77 : 78,console ? dialogue.VisibleText : line.text,console ? 23 : 20,paper);
        if (console)
        {
            if (dialogue.CanAdvance && Button(x+116,y+122,180,"Next   [Space]")) dialogue.Advance();
            if (Button(x+width-171,y+122,150,"Skip   [Tab]")) dialogue.Complete();
        }
    }

    // Keep the next landmark findable when the player turns away in a fight.
    private void Mark(Transform target,string text,float lift,bool directional=false)
    {
        if (!target || !target.gameObject.activeInHierarchy) return;
        var cam = story.player.GetComponent<PlayerInteraction>().view;
        var p = cam.WorldToViewportPoint(target.position + Vector3.up * lift);
        bool outside = p.z <= .2f || p.x < .05f || p.x > .95f || p.y < 0 || p.y > 1;
        if (outside)
        {
            if (!directional) return;
            bool left = cam.transform.InverseTransformPoint(target.position).x < 0;
            p = new Vector3(left ? 0 : 1,.56f,1);
            text = left ? "‹  " + text : text + "  ›";
        }
        float x = Mathf.Clamp(p.x*w - 86, 10, w-182), y = Mathf.Clamp((1-p.y)*h - 15, 132, h-163);
        Fill(x,y,172,28,ink);
        Label(x,y,172,28,text,14,gold,TextAnchor.MiddleCenter);
    }

    private void Menu()
    {
        bool main = story.location == StoryScene.Location.Menu;
        bool end = story.ScreenPanel == StoryScene.Panel.Ending;
        if (main && marquee) GUI.DrawTexture(new Rect(0,0,w,h),marquee,ScaleMode.ScaleAndCrop);
        Fill(0,0,w,h,new Color(.025f,.033f,.052f,main ? .32f : .74f));
        float x = w*.1f, y = h*.19f;
        if (main)
        {
            // Keep the performer visible to the right of the printed programme.
            Fill(0,0,x+475,h,new Color(.035f,.035f,.052f,.92f));
            Fill(x+475,0,2,h,gold);
        }
        else { Fill(x-24,y-28,550,440,ink); Fill(x-24,y-28,550,4,gold); }
        Label(x,y,505,25,"RINGMASTER'S THEATRE     /     "+(end ? "CURTAIN CALL" : "HOUSE PROGRAMME"),15,gold);
        bool inventory = story.ScreenPanel == StoryScene.Panel.Inventory;
        type.font = displayFont;
        Label(x,y+25,510,98,main ? "DASH’N’SLASH" : end ? "The last sound" : inventory ? "Inventory" : "Paused",main ? 72 : 55,paper);
        type.font = null;
        float row=y+130;
        if (main || end)
        {
            Label(x,row-10,475,100,main ? "Win two bouts, and the Ringmaster will take you back to your village well. The whole theatre heard him promise." :
                "Tomas rests his hand on the boards. Even through the wood, he can feel the back row answering.",23,paper);
            if (Button(x,row+90,245,main ? "Start" : "Replay")) story.NewRun();
            if (Button(x,row+148,245,"Quit")) story.Quit();
        }
        else if (inventory)
        {
            Label(x,row,495,42,"Sword    /    They let Tomas keep it for the bouts.",20,paper);
            if (StoryStateManager.HasCanister) Label(x,row+50,495,48,"Oxygen canister    /    Measured for one performer.",20,paper);
            if (StoryStateManager.HasBulb) Label(x,row+104,495,52,"Spore Bulb    /    It grew outside the spotlight.",20,paper);
            if (story.HasClapper) Label(x,row+158,495,42,"Bell clapper    /    His father’s stamp is still on the iron.",18,gold);
            if (Button(x,row+202,245,"Back")) story.Back();
        }
        else
        {
            if (Button(x,row,245,"Resume")) story.Resume();
            if (Button(x,row+54,245,"Inventory")) story.Inventory();
            if (story.IsArena && Button(x,row+108,245,"Retry Arena")) story.Retry();
            if (Button(x,row+(story.IsArena ? 162 : 108),245,"Main Menu")) story.MainMenu();
        }
    }

    private bool Button(float x,float y,float width,string text)
    {
        var rect = new Rect(x,y,width,39);
        bool hover = rect.Contains(Event.current.mousePosition);
        Fill(x,y,width,39,hover ? gold : new Color(.18f,.21f,.27f));
        Label(x+12,y,width-24,39,text,18,hover ? ink : paper,TextAnchor.MiddleLeft);
        return GUI.Button(rect,GUIContent.none,GUIStyle.none);
    }
    private void Label(float x,float y,float width,float height,string text,int size,Color color,TextAnchor anchor=TextAnchor.UpperLeft)
    {
        type.fontSize=size; type.normal.textColor=color; type.alignment=anchor;
        GUI.Label(new Rect(x,y,width,height),text,type);
    }
    private static void Fill(float x,float y,float width,float height,Color color)
    {
        GUI.color=color; GUI.DrawTexture(new Rect(x,y,width,height),Texture2D.whiteTexture); GUI.color=Color.white;
    }
}
