using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SpireUI;
using SpirePresentation;

// Explicit authoring tool; runtime loads the saved prefabs, never rebuilds this layout.
public static class GameUiPrefabBuilder
{
    static readonly Color Paper=new Color(.94f,.89f,.79f);
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UiElement));
        var r=go.GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
    }
    static Image Panel(string name,Transform p,float x,float y,float w,float h,Color c,Sprite sprite=null)
    {
        var r=Rect(name,p,x,y,w,h); var i=r.gameObject.AddComponent<Image>(); i.color=c; i.sprite=sprite; i.raycastTarget=false; return i;
    }
    static Text Label(string name,Transform p,float x,float y,float w,float h,int size,string value="")
    {
        var t=Rect(name,p,x,y,w,h).gameObject.AddComponent<Text>();
        t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontSize=size; t.color=Paper;
        t.text=value; t.raycastTarget=false; t.alignment=TextAnchor.MiddleLeft; return t;
    }
    static Button Button(string name,Transform p,float x,float y,float w,float h,Color c)
    {
        var image=Panel(name,p,x,y,w,h,c); image.raycastTarget=true;
        var b=image.gameObject.AddComponent<Button>(); b.targetGraphic=image;
        var colors=b.colors; colors.highlightedColor=new Color(1,.90f,.74f); colors.pressedColor=new Color(.65f,.65f,.65f); b.colors=colors; return b;
    }
    static Sprite Sprite(string path)
    {
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.mipmapEnabled=false; importer.maxTextureSize=2048; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void Set(SerializedObject s,string field,Object value) { s.FindProperty(field).objectReferenceValue=value; }
    static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
    [MenuItem("Tools/Spire/Rebuild UI Prefabs")]
    public static void Build()
    {

        var bg=Sprite("Assets/GameResources/UI/Views/GameStart/Art/101.png");
        foreach(var path in System.IO.Directory.GetFiles("Assets/GameResources/Characters","*.png",System.IO.SearchOption.AllDirectories)) Sprite(path.Replace('\\','/'));
        var go=new GameObject("GameStartView",typeof(RectTransform),typeof(GameStartView)); go.SetActive(false);
        try
        {
            Stretch(go.GetComponent<RectTransform>());
            var image=Panel("Background",go.transform,0,0,1280,720,Color.white,bg); Stretch(image.rectTransform);
            Stretch(Panel("Shade",go.transform,0,0,1280,720,new Color(.025f,.035f,.045f,.65f)).rectTransform);
            var layout=Rect("Layout",go.transform,0,0,1280,720);
            layout.anchorMin=layout.anchorMax=layout.pivot=new Vector2(.5f,.5f); layout.anchoredPosition=Vector2.zero;
            var s=new SerializedObject(go.GetComponent<GameStartView>());
            Set(s,"title",Label("Title",layout,64,35,700,90,52));
            var home=Rect("Home",layout,0,0,1280,720);
            var selection=Rect("Selection",layout,0,0,1280,720);
            var connection=Rect("Connection",layout,0,0,1280,720);
            var lobby=Rect("Lobby",layout,0,0,1280,720);
            Set(s,"home",home.gameObject); Set(s,"selection",selection.gameObject); Set(s,"connection",connection.gameObject); Set(s,"lobby",lobby.gameObject);
            Set(s,"single",CaptionButton("SinglePlayer",home,80,245,420,80,"Single Player"));
            Set(s,"multiplayer",CaptionButton("Multiplayer",home,80,350,420,80,"Multiplayer"));
            Set(s,"back",CaptionButton("Back",layout,1010,40,180,48,"Back"));
            Set(s,"language",CaptionButton("Language",layout,1010,104,180,38,"EN / 中文"));
            Set(s,"host",CaptionButton("Host",connection,80,300,420,70,"Create Room"));
            Set(s,"join",CaptionButton("Join",connection,80,480,420,70,"Join Room"));
            var inputImage=Panel("HostAddress",connection,80,395,420,58,new Color(.08f,.10f,.12f)); inputImage.raycastTarget=true;
            var input=inputImage.gameObject.AddComponent<InputField>(); input.targetGraphic=inputImage;
            input.textComponent=Label("Value",input.transform,16,0,388,58,23); input.text="127.0.0.1"; input.characterLimit=253; Set(s,"address",input);
            // Horizontal scrolling supports additional table-defined characters without layout code.
            var viewport=Panel("Viewport",selection,64,200,1152,330,new Color(0,0,0,.15f)); viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=true; scroll.vertical=false; scroll.viewport=viewport.rectTransform;
            var choices=Rect("Choices",viewport.transform,0,0,1152,330); scroll.content=choices; Set(s,"choices",choices);
            Set(s,"ready",CaptionButton("Ready",lobby,940,560,250,64,"Ready"));
            Set(s,"start",CaptionButton("StartGame",lobby,940,560,250,64,"Start Game"));
            Set(s,"players",Label("Players",lobby,64,545,850,120,21));
            Set(s,"status",Label("Status",layout,64,150,1130,45,18));
            s.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go,"Assets/GameResources/UI/Views/GameStart/GameStartView.prefab");
        }
        finally { Object.DestroyImmediate(go); }
        var game=new GameObject("GameplayView",typeof(RectTransform),typeof(GameplayView)); game.SetActive(false);
        try {
            Stretch(game.GetComponent<RectTransform>()); var content=Rect("Content",game.transform,0,0,1280,720); Stretch(content);
            var s=new SerializedObject(game.GetComponent<GameplayView>()); Set(s,"content",content); s.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(game,"Assets/GameResources/UI/Views/Gameplay/GameplayView.prefab");
        } finally { Object.DestroyImmediate(game); }
        System.IO.Directory.CreateDirectory("Assets/GameResources/UI/Views/Card");
        AssetDatabase.Refresh();
        var card=new GameObject("CardView",typeof(RectTransform),typeof(Image),typeof(Button),typeof(CardUIView)); card.SetActive(false);
        try
        {
            var r=card.GetComponent<RectTransform>(); r.sizeDelta=new Vector2(224,330);
            var frame=card.GetComponent<Image>(); frame.color=new Color(.15f,.18f,.18f);
            var b=card.GetComponent<Button>(); b.targetGraphic=frame;
            var art=Panel("Art",r,0,0,224,330,Color.white); Stretch(art.rectTransform);
            var shade=Panel("Shade",r,0,0,0,0,new Color(.025f,.03f,.04f,.90f)); 
            shade.rectTransform.anchorMin=Vector2.zero; shade.rectTransform.anchorMax=new Vector2(1,.37f); shade.rectTransform.offsetMin=shade.rectTransform.offsetMax=Vector2.zero;
            var title=Label("Title",r,0,0,0,0,18);
            Anchors(title.rectTransform,new Vector2(.06f,.20f),new Vector2(.94f,.36f));
            var description=Label("Description",r,0,0,0,0,14);
            Anchors(description.rectTransform,new Vector2(.06f,.02f),new Vector2(.94f,.21f));
            var cost=Label("Cost",r,0,0,0,0,24); cost.alignment=TextAnchor.MiddleCenter;
            Anchors(cost.rectTransform,new Vector2(.02f,.79f),new Vector2(.23f,.98f));
            var s=new SerializedObject(card.GetComponent<CardUIView>());
            Set(s,"artwork",art); Set(s,"title",title); Set(s,"description",description); Set(s,"cost",cost); Set(s,"button",b);
            s.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(card,"Assets/GameResources/UI/Views/Card/3.prefab");
        } finally { Object.DestroyImmediate(card); }
        AssetDatabase.SaveAssets(); ResourceCatalogBuilder.Build();
    }
    static void Anchors(RectTransform r,Vector2 min,Vector2 max) { r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero; }
    static Button CaptionButton(string name,Transform parent,float x,float y,float w,float h,string caption)
    {
        var b=Button(name,parent,x,y,w,h,new Color(.35f,.25f,.18f));
        var t=Label("Caption",b.transform,8,0,w-16,h,24,caption); t.alignment=TextAnchor.MiddleCenter; return b;
    }
}
