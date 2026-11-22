using System;
using System.IO;
using Spire;
using SpirePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
public static class BattleStyleBuilder
{
    const string ViewPath="Assets/GameResources/UI/Views/Gameplay/GameplayView.prefab";
    static Color Gold=new Color(.55f,.40f,.23f);
    static RectTransform Layer(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        var r=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero;
        var image=r.GetComponent<Image>(); image.color=color; image.raycastTarget=false; return r;
    }
    static void Inset(RectTransform r,float value) { r.offsetMin=Vector2.one*value; r.offsetMax=Vector2.one*-value; }
    static void Frame(RectTransform r)
    {
        var old=r.Find("Border"); if(old!=null) return;
        var edge=Layer("Border",r,Vector2.zero,Vector2.one,Gold); Inset(edge,0);
        var layout=edge.gameObject.AddComponent<LayoutElement>(); layout.ignoreLayout=true; edge.SetAsFirstSibling();
        var inside=Layer("Inset",edge,Vector2.zero,Vector2.one,new Color(.055f,.065f,.075f,.98f)); Inset(inside,1);
    }
    static void Health(Image fill,Text value,bool enemy)
    {
        var root=(RectTransform)fill.transform.parent;
        root.GetComponent<LayoutElement>().preferredHeight=34;
        root.GetComponent<Image>().color=new Color(.055f,.025f,.028f);
        fill.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        fill.type=Image.Type.Filled; fill.fillMethod=Image.FillMethod.Horizontal; fill.fillOrigin=0;
        fill.color=enemy?new Color(.65f,.16f,.12f):new Color(.53f,.12f,.15f);
        Inset(fill.rectTransform,4);
        Frame(root);
        var sheen=root.Find("Sheen");
        if(sheen==null) { var r=Layer("Sheen",root,new Vector2(0,.63f),new Vector2(1,1),new Color(1,.76f,.43f,.12f)); r.offsetMin=new Vector2(5,0); r.offsetMax=new Vector2(-5,-5); }
        value.rectTransform.anchorMin=Vector2.zero; value.rectTransform.anchorMax=Vector2.one;
        value.rectTransform.offsetMin=value.rectTransform.offsetMax=Vector2.zero; value.transform.SetAsLastSibling();
        value.alignment=TextAnchor.MiddleCenter; value.alignByGeometry=true; value.fontSize=20;
        var shadow=value.GetComponent<Shadow>()??value.gameObject.AddComponent<Shadow>(); shadow.effectColor=new Color(0,0,0,.9f); shadow.effectDistance=new Vector2(1,-1);
    }
    static void MakeRoom(int id,RoomKind kind)
    {
        string path="Assets/GameResources/Rooms/"+id+".prefab"; if(File.Exists(path)) return;
        var root=new GameObject(kind+"Background",typeof(RectTransform)); var rect=(RectTransform)root.transform;
        rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
        Color[] bases={new Color(.08f,.115f,.14f),new Color(.13f,.08f,.12f),new Color(.10f,.15f,.13f),new Color(.16f,.12f,.075f),new Color(.10f,.085f,.17f),new Color(.18f,.065f,.055f)};
        Color[] lights={new Color(.24f,.43f,.48f),new Color(.5f,.26f,.38f),new Color(.45f,.57f,.32f),new Color(.6f,.42f,.20f),new Color(.39f,.30f,.58f),new Color(.64f,.25f,.12f)};
        var c=bases[(int)kind]; var accent=lights[(int)kind];
        try
        {
            Layer("DistantStone",rect,Vector2.zero,Vector2.one,c);
            for(int i=0;i<7;i++)
            {
                float x=.035f+i*.145f;
                Layer("Recess_"+i,rect,new Vector2(x,.28f),new Vector2(x+.115f,.86f),c*.6f);
                Layer("Lintel_"+i,rect,new Vector2(x-.008f,.83f),new Vector2(x+.123f,.86f),Color.Lerp(c,accent,.22f));
                Layer("Column_"+i,rect,new Vector2(x-.016f,.26f),new Vector2(x-.004f,.88f),Color.Lerp(c,accent,.28f));
                Layer("ColumnEdge_"+i,rect,new Vector2(x-.016f,.26f),new Vector2(x-.014f,.88f),Color.Lerp(c,accent,.6f));
            }
            Layer("Floor",rect,Vector2.zero,new Vector2(1,.28f),Color.Lerp(c,Color.black,.4f));
            for(int i=0;i<5;i++) Layer("FloorSeam_"+i,rect,new Vector2(0,.055f*i),new Vector2(1,.055f*i+.002f),Color.Lerp(c,accent,.16f));
            var emblem=Layer("RoomEmblem",rect,new Vector2(.46f,.50f),new Vector2(.54f,.68f),Color.Lerp(c,accent,.45f));
            emblem.localEulerAngles=new Vector3(0,0,45);
            var inset=Layer("EmblemInset",emblem,Vector2.zero,Vector2.one,c*.7f); Inset(inset,5);
            // Type-specific geometry is authored independently in each room prefab.
            if(kind==RoomKind.Boss || kind==RoomKind.Elite)
                for(int i=0;i<3;i++) Layer("Banner_"+i,rect,new Vector2(.3f+i*.18f,.66f),new Vector2(.34f+i*.18f,.91f),Color.Lerp(c,accent,.48f));
            if(kind==RoomKind.Camp)
                for(int i=0;i<4;i++) { var log=Layer("CampLogs_"+i,rect,new Vector2(.46f,.28f+i*.008f),new Vector2(.54f,.295f+i*.008f),new Color(.24f,.16f,.09f)); log.localEulerAngles=new Vector3(0,0,i%2==0?15:-15); }
            if(kind==RoomKind.Shop)
                for(int i=0;i<3;i++) Layer("Shelves_"+i,rect,new Vector2(.15f,.4f+i*.13f),new Vector2(.85f,.41f+i*.13f),Color.Lerp(c,accent,.35f));
            var glow=Layer("AmbientLight",rect,Vector2.zero,Vector2.one,Color.clear); var group=glow.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts=false;
            for(int i=0;i<3;i++)
            {
                float x=.19f+i*.30f;
                Layer("LightHalo_"+i,glow,new Vector2(x-.025f,.44f),new Vector2(x+.025f,.78f),new Color(accent.r,accent.g,accent.b,.13f));
                Layer("Sconce_"+i,glow,new Vector2(x-.004f,.58f),new Vector2(x+.004f,.66f),new Color(accent.r,accent.g,accent.b,.9f));
            }
            var motion=root.AddComponent<RoomGlow>(); var so=new SerializedObject(motion); so.FindProperty("glow").objectReferenceValue=group; so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [MenuItem("Tools/Spire/Apply Battle Style And Room Backgrounds")]
    public static void Build()
    {
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/GameResources/UI/Shared/Fonts/SourceHanSerifCN-Regular.otf");
        if(font==null) throw new Exception("Game font has not imported");
        Directory.CreateDirectory("Assets/GameResources/Rooms"); AssetDatabase.Refresh();
        foreach(RoomKind kind in Enum.GetValues(typeof(RoomKind))) MakeRoom(500+(int)kind,kind);
        var root=PrefabUtility.LoadPrefabContents(ViewPath);
        try
        {
            var v=root.GetComponent<GameplayView>();
            foreach(var text in v.battleRoot.GetComponentsInChildren<Text>(true)) { text.font=font; text.alignment=TextAnchor.MiddleCenter; text.alignByGeometry=true; text.verticalOverflow=VerticalWrapMode.Overflow; text.color=new Color(.93f,.86f,.71f); }
            v.enemyIntent.text="攻击全体 6";
            Health(v.playerHealthFill,v.playerHealth,false); Health(v.enemyHealthFill,v.enemyHealth,true);
            v.battleRoot.GetComponent<Image>().color=Color.clear;
            foreach(string path in new[]{"Top","Middle/Player/Status","Middle/Enemy/Intent","Bottom"})
            {
                var panel=(RectTransform)v.battleRoot.transform.Find(path); panel.GetComponent<Image>().color=new Color(.06f,.07f,.08f,.87f); Frame(panel);
            }
            if(v.backdrop==null)
            {
                var mount=Layer("RoomBackground",root.transform,Vector2.zero,Vector2.one,new Color(.06f,.07f,.08f)); mount.SetAsFirstSibling();
                v.backdrop=mount.gameObject.AddComponent<RoomBackdrop>();
                var so=new SerializedObject(v.backdrop); so.FindProperty("container").objectReferenceValue=mount;
                var list=so.FindProperty("rooms"); list.arraySize=6;
                for(int i=0;i<6;i++) { var e=list.GetArrayElementAtIndex(i); e.FindPropertyRelative("room").enumValueIndex=i; e.FindPropertyRelative("prefabId").intValue=500+i; }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(v); PrefabUtility.SaveAsPrefabAsset(root,ViewPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        const string cardPath="Assets/GameResources/UI/Views/Card/3.prefab";
        var card=PrefabUtility.LoadPrefabContents(cardPath);
        try {
            foreach(var text in card.GetComponentsInChildren<Text>(true)) { text.font=font; text.alignment=TextAnchor.MiddleCenter; text.alignByGeometry=true; text.verticalOverflow=VerticalWrapMode.Overflow; }
            PrefabUtility.SaveAsPrefabAsset(card,cardPath);
        } finally { PrefabUtility.UnloadPrefabContents(card); }
        ResourceCatalogBuilder.Build();
    }
}
