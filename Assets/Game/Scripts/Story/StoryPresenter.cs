using UnityEngine;

// All rectangles use a common scaled coordinate system so the console and controls stay together.
public class StoryPresenter : MonoBehaviour
{
    public StoryScene story;
    public Texture2D marquee;
    private readonly Color ink = new(.045f,.055f,.083f,.97f);
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
            Fill(24,22,383,75,ink); Fill(24,22,4,75,gold);
            Label(43,30,360,22,story.location == StoryScene.Location.Garden ? "ACT I    /    THE GARDEN" :
                story.location == StoryScene.Location.Castle ? "ACT II    /    THE CAPTURED CASTLE" : "ON AIR    /    THE STAGE",14,gold);
            string count = story.Objective == "Defeat the guards." ? "   (" + story.Targets + ")" : "";
            Label(42,53,363,36,story.Objective + count,23,paper);
        }
        if (story.IsArena)
        {
            Label(w-230,24,205,28,"ESC   /   PAUSE",15,paper,TextAnchor.MiddleRight);
            if (story.location == StoryScene.Location.Garden && StoryStateManager.FirstBreath == StoryStateManager.Breath.None)
            {
                Mark(story.canister,"E   •   USE CANISTER",1.3f);
                Mark(story.flower,"LMB   •   STRIKE FLOWER",1.05f);
            }
            if (story.ExitOpen) Mark(story.exit,"E   •   RETURN",1.1f);
            else if (story.location == StoryScene.Location.Castle) Mark(story.rope,story.Targets == 0 ? "STRIKE   •   BELL" : "VILLAGE WELL",1.6f);
        }
        var dialogue = story.dialogue;
        if (dialogue.Current == null) return;
        bool console = dialogue.IsConsole;
        float width = console ? 810 : 620;
        float x = console ? (w-width)/2 : w-width-28, y = h-(console ? 186 : 134);
        Fill(x,y,width,console ? 164 : 110,ink); Fill(x,y,width,3,gold);
        var line = dialogue.Current;
        if (line.portrait) GUI.DrawTexture(new Rect(x+16,y+15,75,78),line.portrait,ScaleMode.ScaleToFit);
        Label(x+106,y+12,width-124,24,line.speaker.ToUpperInvariant(),16,gold);
        Label(x+106,y+40,width-126,66,dialogue.VisibleText,console ? 23 : 20,paper);
        if (console)
        {
            if (Button(x+106,y+111,200,"Next   [Space]")) dialogue.Advance();
            if (Button(x+width-190,y+111,170,"Skip   [Tab]")) dialogue.Complete();
        }
    }

    private void Mark(Transform target,string text,float lift)
    {
        if (!target || !target.gameObject.activeInHierarchy) return;
        var cam = story.player.GetComponent<PlayerInteraction>().view;
        var p = cam.WorldToViewportPoint(target.position + Vector3.up * lift);
        if (p.z <= .2f || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) return;
        float x = Mathf.Clamp(p.x*w - 77, 3, w-157), y = Mathf.Clamp((1-p.y)*h - 15, 105, h-165);
        Fill(x,y,154,28,ink);
        Label(x,y,154,28,text,14,gold,TextAnchor.MiddleCenter);
    }

    private void Menu()
    {
        bool main = story.location == StoryScene.Location.Menu;
        bool end = story.ScreenPanel == StoryScene.Panel.Ending;
        if (main && marquee) GUI.DrawTexture(new Rect(0,0,w,h),marquee,ScaleMode.ScaleAndCrop);
        Fill(0,0,w,h,new Color(.025f,.033f,.052f,main ? .32f : .74f));
        float x = w*.1f, y = h*.19f;
        Fill(x-24,y-28,550,440,ink); Fill(x-24,y-28,550,4,gold);
        Label(x,y,505,25,"RINGMASTER'S THEATRE     /     "+(end ? "CURTAIN CALL" : "HOUSE PROGRAMME"),15,gold);
        bool inventory = story.ScreenPanel == StoryScene.Panel.Inventory;
        Label(x,y+38,510,75,main ? "Dash’n’Slash" : end ? "The last sound" : inventory ? "Inventory" : "Paused",49,paper);
        float row=y+130;
        if (main || end)
        {
            Label(x,row-10,475,65,main ? "A captive swordsman. Two arenas. One breath before the lights come up." :
                "The cuffs are still here. Across the curtain, Earth’s rhythm carries on.",23,paper);
            if (Button(x,row+90,245,main ? "Start" : "Replay")) story.NewRun();
            if (Button(x,row+148,245,"Quit")) story.Quit();
        }
        else if (inventory)
        {
            Label(x,row,495,42,"Sword    /    The one cue he can give himself.",20,paper);
            if (StoryStateManager.HasCanister) Label(x,row+50,495,48,"Oxygen canister    /    Measured for one performer.",20,paper);
            if (StoryStateManager.HasBulb) Label(x,row+104,495,52,"Spore Bulb    /    It grew outside the spotlight.",20,paper);
            if (Button(x,row+190,245,"Back")) story.Back();
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
