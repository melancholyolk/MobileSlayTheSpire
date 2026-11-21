using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SpireUI;
using SpirePresentation;
public static class MapAssetBuilder
{
    const string Skin="Assets/GameResources/UI/Shared/Skins/";
    static Sprite Sprite(int id)=>AssetDatabase.LoadAssetAtPath<Sprite>(Skin+id+".png");
    [MenuItem("Tools/Spire/Build Map and UI Skins")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach(var path in Directory.GetFiles(Skin,"*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.wrapMode=path.EndsWith("402.png")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            if(int.Parse(Path.GetFileNameWithoutExtension(path))<400) importer.spriteBorder=new Vector4(20,20,20,20);
            importer.SaveAndReimport();
        }
        var theme=Load<UiTheme>(Skin+"30.asset"); theme.Normal=Enumerable.Range(0,3).Select(i=>Sprite(301+i*3)).ToArray();
        theme.Hover=Enumerable.Range(0,3).Select(i=>Sprite(302+i*3)).ToArray(); theme.Pressed=Enumerable.Range(0,3).Select(i=>Sprite(303+i*3)).ToArray(); theme.Disabled=Sprite(310); theme.CardFrame=Sprite(311); EditorUtility.SetDirty(theme);
        var map=Load<MapTheme>(Skin+"31.asset"); map.Background=Sprite(401); map.Road=AssetDatabase.LoadAssetAtPath<Texture2D>(Skin+"402.png");map.Rooms=Enumerable.Range(410,6).Select(Sprite).ToArray(); EditorUtility.SetDirty(map);
        foreach(var path in new[]{"Assets/GameResources/UI/Views/GameStart/GameStartView.prefab","Assets/GameResources/UI/Views/Gameplay/GameplayView.prefab","Assets/GameResources/UI/Views/Card/3.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            foreach(var button in root.GetComponentsInChildren<Button>(true))
                theme.Apply(button,new[]{"SinglePlayer","Multiplayer","Host","Join","StartGame","Ready"}.Contains(button.name)?ButtonIntent.Primary:ButtonIntent.Secondary);
            if(root.GetComponent<BaseUIView>()!=null) { if(root.GetComponent<CanvasGroup>()==null) root.AddComponent<CanvasGroup>(); if(root.GetComponent<SpireAnimation.BetterAnimation>()==null) root.AddComponent<SpireAnimation.BetterAnimation>(); }
            PrefabUtility.SaveAsPrefabAsset(root,path); PrefabUtility.UnloadPrefabContents(root);
        }
        Directory.CreateDirectory("Assets/GameResources/UI/Views/Map");
        var view=new GameObject("MapView",typeof(RectTransform),typeof(MapUIView)); view.SetActive(false);
        var rt=view.GetComponent<RectTransform>(); rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
        var bg=Image("Parchment",rt,0,0,1280,720); bg.sprite=map.Background; bg.type=UnityEngine.UI.Image.Type.Sliced;
        var title=Text("Title",rt,52,16,850,48,30,"远征路线  /  THE ASCENT"); title.color=new Color(.22f,.18f,.13f);
        var status=Text("Status",rt,52,655,1150,55,17,""); status.color=title.color;
        var backImage=Image("Menu",rt,1100,18,140,48); var back=backImage.gameObject.AddComponent<Button>(); theme.Apply(back,ButtonIntent.Destructive);
        Text("Caption",back.transform,0,0,140,48,20,"返回").alignment=TextAnchor.MiddleCenter;
        var viewport=Rect("Viewport",rt,55,78,1170,570); viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
        var content=Rect("Content",viewport,0,0,1100,1160);content.anchorMin=content.anchorMax=Vector2.zero;content.pivot=Vector2.zero;content.anchoredPosition=Vector2.zero;
        scroll.viewport=viewport;scroll.content=content;
        var serialized=new SerializedObject(view.GetComponent<MapUIView>());
        serialized.FindProperty("content").objectReferenceValue=content;serialized.FindProperty("scroll").objectReferenceValue=scroll;serialized.FindProperty("status").objectReferenceValue=status;serialized.FindProperty("back").objectReferenceValue=back;serialized.FindProperty("background").objectReferenceValue=bg;serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(view,"Assets/GameResources/UI/Views/Map/4.prefab");Object.DestroyImmediate(view);
        AssetDatabase.SaveAssets();ResourceCatalogBuilder.Build();
    }
    static T Load<T>(string path) where T:ScriptableObject { var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset; }
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform),typeof(UiElement)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    static Image Image(string name,Transform parent,float x,float y,float w,float h)=>Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();
    static Text Text(string name,Transform parent,float x,float y,float w,float h,int size,string text)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.text=text;t.color=new Color(.96f,.88f,.72f);t.raycastTarget=false;return t;
    }
}
