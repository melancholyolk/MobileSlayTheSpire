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
        var bg=Sprite("Assets/Resources/UI/Views/GameStart/Art/Background.png");
        var warrior=Sprite("Assets/UI/Shared/Portraits/Warrior.png");
        var hunter=Sprite("Assets/UI/Shared/Portraits/Hunter.png");
        var go=new GameObject("GameStartView",typeof(RectTransform),typeof(GameStartView)); go.SetActive(false);
        try
        {
            Stretch(go.GetComponent<RectTransform>());
            var image=Panel("Background",go.transform,0,0,1280,720,Color.white,bg); Stretch(image.rectTransform);
            var shade=Panel("Shade",go.transform,0,0,1280,720,new Color(.025f,.035f,.045f,.50f)); Stretch(shade.rectTransform);
            // Centered reference layout stays legible on landscape phones and tablets.
            var layout=Rect("Layout",go.transform,0,0,1280,720); layout.anchorMin=layout.anchorMax=layout.pivot=new Vector2(.5f,.5f); layout.anchoredPosition=Vector2.zero;
            var s=new SerializedObject(go.GetComponent<GameStartView>());
            Label("Edition",layout,58,42,500,26,15,"A S H E N   S P I R E   /   0 1");
            Set(s,"title",Label("Title",layout,54,85,530,90,58));
            Set(s,"subtitle",Label("Subtitle",layout,58,182,480,60,19));
            Panel("Accent",layout,58,264,70,3,new Color(.72f,.46f,.29f));
            Set(s,"choose",Label("Choose",layout,58,284,480,45,24));
            var w=Button("Warrior",layout,600,105,280,490,Color.white);
            w.GetComponent<Image>().sprite=warrior;
            var h=Button("Hunter",layout,904,105,280,490,Color.white);
            h.GetComponent<Image>().sprite=hunter;
            Set(s,"warrior",w); Set(s,"hunter",h);
            foreach(var b in new[]{w,h}) Panel("CaptionShade",b.transform,0,394,280,96,new Color(.025f,.03f,.04f,.87f));
            Set(s,"warriorCaption",Label("Caption",w.transform,22,414,240,65,23));
            Set(s,"hunterCaption",Label("Caption",h.transform,22,414,240,65,23));
            Set(s,"addressCaption",Label("AddressCaption",layout,58,370,480,32,16));
            var addressPanel=Panel("HostAddress",layout,58,408,470,48,new Color(.06f,.07f,.08f,.92f)); addressPanel.raycastTarget=true;
            var input=addressPanel.gameObject.AddComponent<InputField>(); input.targetGraphic=addressPanel;
            input.textComponent=Label("Value",addressPanel.transform,14,0,442,48,21); input.characterLimit=253;
            input.contentType=InputField.ContentType.Standard; Set(s,"hostAddress",input);
            var host=Button("Host",layout,58,476,226,58,new Color(.40f,.25f,.18f));
            var join=Button("Join",layout,302,476,226,58,new Color(.16f,.28f,.28f));
            Set(s,"host",host); Set(s,"join",join);
            var ht=Label("Caption",host.transform,8,0,210,58,18); ht.alignment=TextAnchor.MiddleCenter; Set(s,"hostCaption",ht);
            var jt=Label("Caption",join.transform,8,0,210,58,18); jt.alignment=TextAnchor.MiddleCenter; Set(s,"joinCaption",jt);
            Set(s,"status",Label("Status",layout,58,550,480,95,16));
            var lang=Button("Language",layout,1034,36,150,40,new Color(.07f,.09f,.1f,.85f)); Set(s,"language",lang);
            var lt=Label("Caption",lang.transform,0,0,150,40,16); lt.alignment=TextAnchor.MiddleCenter; Set(s,"languageCaption",lt);
            Label("Footer",layout,600,628,584,30,13,"I   /   W A R R I O R                      II   /   H U N T E R");
            s.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/UI/Views/GameStart/GameStartView.prefab");
        }
        finally { Object.DestroyImmediate(go); }
        var game=new GameObject("GameplayView",typeof(RectTransform),typeof(GameplayView)); game.SetActive(false);
        try {
            Stretch(game.GetComponent<RectTransform>());
            var content=Rect("Content",game.transform,0,0,1280,720); Stretch(content);
            var s=new SerializedObject(game.GetComponent<GameplayView>()); Set(s,"content",content); s.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(game,"Assets/Resources/UI/Views/Gameplay/GameplayView.prefab");
        } finally { Object.DestroyImmediate(game); }
        AssetDatabase.SaveAssets();
    }
}
