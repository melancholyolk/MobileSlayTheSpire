using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SpirePresentation;

// One-time migration. Existing authored layouts are intentionally never regenerated.
public static class BattleUiPrefabBuilder
{
    const string Path="Assets/GameResources/UI/Views/Gameplay/GameplayView.prefab";
    static readonly Color Panel=new Color(.13f,.15f,.18f);
    static RectTransform Node(string name,Transform parent,float width=-1,float height=-1,float flex=0)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        var l=r.gameObject.AddComponent<LayoutElement>(); l.preferredWidth=width; l.minWidth=Mathf.Max(0,width); l.preferredHeight=height; l.flexibleWidth=flex;
        return r;
    }
    static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
    static void Background(RectTransform r,Color color) { var image=r.gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false; }
    static T Layout<T>(RectTransform r,int padding=0,float spacing=12) where T:HorizontalOrVerticalLayoutGroup
    {
        var group=r.gameObject.AddComponent<T>(); group.padding=new RectOffset(padding,padding,padding,padding); group.spacing=spacing;
        group.childControlWidth=group.childControlHeight=true; group.childForceExpandWidth=group.childForceExpandHeight=false; group.childAlignment=TextAnchor.MiddleCenter; return group;
    }
    static Text Text(string name,Transform parent,string value,int size=20,float height=30)
    {
        var r=Node(name,parent,-1,height,1); var t=r.gameObject.AddComponent<Text>(); t.font=UiFonts.Body;
        t.text=value; t.fontSize=size; t.color=new Color(.94f,.89f,.79f); t.alignment=TextAnchor.MiddleCenter; t.raycastTarget=false; return t;
    }
    static Button Button(string name,Transform parent,string value,float width)
    {
        var r=Node(name,parent,width,48); Background(r,new Color(.35f,.25f,.18f)); var b=r.gameObject.AddComponent<Button>();
        b.targetGraphic=r.GetComponent<Image>(); b.targetGraphic.raycastTarget=true;
        var t=Text("Caption",r,value); Stretch(t.rectTransform); return b;
    }
    static void Health(Transform parent,string name,out Image fill,out Text label)
    {
        var r=Node(name,parent,-1,26,1); Background(r,new Color(.19f,.12f,.14f));
        var f=Node("Fill",r); Stretch(f); Background(f,new Color(.65f,.23f,.25f)); fill=f.GetComponent<Image>();
        fill.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); fill.type=Image.Type.Filled; fill.fillMethod=Image.FillMethod.Horizontal; fill.fillOrigin=0;
        label=Text("Value",r,"80 / 80",17,26); Stretch(label.rectTransform);
    }
    static RectTransform Anchor(string name,RectTransform parent,Vector2 point,Vector3 scale)
    {
        var r=Node(name,parent); r.GetComponent<LayoutElement>().ignoreLayout=true;
        r.anchorMin=r.anchorMax=point; r.anchoredPosition=Vector2.zero; r.sizeDelta=Vector2.zero; r.localScale=scale; return r;
    }
    [MenuItem("Tools/Spire/Create Battle Layout If Missing")]
    public static void EnsureLayout()
    {
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var v=root.GetComponent<GameplayView>(); if(v.battleRoot!=null) return;
            var battle=Node("Battle",root.transform); Stretch(battle); Background(battle,new Color(.09f,.10f,.12f)); v.battleRoot=battle.gameObject;
            var rows=Layout<VerticalLayoutGroup>(battle,16,16); rows.childForceExpandWidth=true;
            var top=Node("Top",battle,-1,96); Background(top,Panel); Layout<HorizontalLayoutGroup>(top,12,24);
            var relic=Node("Relics",top,250,-1); Layout<VerticalLayoutGroup>(relic,0,6); v.relics=Text("Title",relic,"遗物 · 暂无");
            var slots=Node("Slots",relic,-1,32,1); Layout<HorizontalLayoutGroup>(slots,0,8);
            for(int i=0;i<5;i++) Background(Node("RelicSlot_"+i,slots,36,32),new Color(.22f,.23f,.26f));
            var potions=Node("Potions",top,200,-1); Layout<VerticalLayoutGroup>(potions,0,6); v.potions=Text("Title",potions,"药水 · 暂无");
            slots=Node("Slots",potions,-1,32,1); Layout<HorizontalLayoutGroup>(slots,0,8);
            for(int i=0;i<3;i++) Background(Node("PotionSlot_"+i,slots,38,32),new Color(.20f,.26f,.29f));
            var info=Node("ChapterInfo",top,-1,-1,1); Layout<VerticalLayoutGroup>(info,0,8); v.boss=Text("Boss",info,"本章首领 · 灰烬守卫"); v.mapInfo=Text("MapInfo",info,"第 1/8 层   金币 50   牌组 10",18);
            v.language=Button("Language",top,"zh-Hans",110); v.menu=Button("Menu",top,"菜单",124);
            var middle=Node("Middle",battle,-1,340); middle.GetComponent<LayoutElement>().flexibleHeight=1; Layout<HorizontalLayoutGroup>(middle,0,24);
            var player=Node("Player",middle,-1,-1,1); var pg=Layout<HorizontalLayoutGroup>(player,0,12); pg.childForceExpandHeight=true;
            var stats=Node("Status",player,216); Background(stats,Panel); var sg=Layout<VerticalLayoutGroup>(stats,16,10); sg.childForceExpandWidth=true;
            var portraitFrame=Node("PortraitFrame",stats,-1,88,1); Layout<HorizontalLayoutGroup>(portraitFrame,0,0);
            var portrait=Node("Portrait",portraitFrame,88,88); Background(portrait,Color.white); v.portrait=portrait.GetComponent<Image>(); v.portrait.preserveAspect=true;
            v.characterName=Text("Name",stats,"角色",21); Health(stats,"Health",out v.playerHealthFill,out v.playerHealth);
            v.energy=Text("Energy",stats,"能量 3",23); v.handCount=Text("HandCount",stats,"手牌 5",23); v.playerStatus=Text("Buffs",stats,"格挡 0   力量 0",17);
            var actorArea=Node("Actors",player,-1,-1,1);
            v.heroAnchors=new RectTransform[4];
            for(int i=0;i<4;i++) v.heroAnchors[i]=Anchor("Hero_"+i,actorArea,new Vector2(.4f+i*.18f,.10f),new Vector3(.68f,.73f,1));
            var actions=Node("TurnActions",middle,190); Layout<VerticalLayoutGroup>(actions,0,12);
            v.turn=Text("Turn",actions,"回合 1",22);
            var spacer=Node("PlayTarget",actions,-1,100,1); spacer.GetComponent<LayoutElement>().flexibleHeight=1; v.playAnchor=spacer;
            v.reconnect=Button("Reconnect",actions,"重新连接",190); v.reconnect.gameObject.SetActive(false);
            v.endTurn=Button("EndTurn",actions,"结束回合",190);
            var enemy=Node("Enemy",middle,-1,-1,1); var eg=Layout<VerticalLayoutGroup>(enemy,0,6); eg.childForceExpandWidth=true;
            var intent=Node("Intent",enemy,-1,66,1); Background(intent,Panel); Layout<VerticalLayoutGroup>(intent,6,0);
            v.enemyIntent=Text("Action",intent,"意图 · 攻击全体 6",20); v.enemyName=Text("Name",intent,"Hollow",16,24);
            var enemyArea=Node("Actor",enemy,-1,-1,1); enemyArea.GetComponent<LayoutElement>().flexibleHeight=1;
            v.enemyAnchors=new[]{Anchor("Enemy_0",enemyArea,new Vector2(.5f,0),new Vector3(-.6f,.6f,1))};
            Health(enemy,"Health",out v.enemyHealthFill,out v.enemyHealth); v.enemyStatus=Text("Buffs",enemy,"格挡 0   中毒 0",17,26);
            var bottom=Node("Bottom",battle,-1,236); Background(bottom,Panel); Layout<HorizontalLayoutGroup>(bottom,12,20);
            v.drawAnchor=Node("DrawPile",bottom,124,150); Background(v.drawAnchor,new Color(.19f,.25f,.31f)); v.drawCount=Text("Count",v.drawAnchor,"抽牌堆\n5",24); Stretch(v.drawCount.rectTransform);
            var hand=Node("Hand",bottom,-1,-1,1); var hg=Layout<VerticalLayoutGroup>(hand,0,2); hg.childForceExpandWidth=true;
            v.handTitle=Text("Title",hand,"手牌 5",16,20); v.handTitle.GetComponent<LayoutElement>().minHeight=20;
            v.hand=Node("Cards",hand,-1,180,1); var hl=v.hand.gameObject.AddComponent<BattleHandLayout>();
            var serialized=new SerializedObject(hl); serialized.FindProperty("cardSize").vector2Value=new Vector2(156,180); serialized.FindProperty("spacing").floatValue=8; serialized.ApplyModifiedPropertiesWithoutUndo();
            var piles=Node("RightPiles",bottom,128); Layout<VerticalLayoutGroup>(piles,0,16);
            v.exhaustAnchor=Node("ExhaustPile",piles,128,80); Background(v.exhaustAnchor,new Color(.29f,.21f,.20f)); v.exhaustCount=Text("Count",v.exhaustAnchor,"消耗牌堆\n0",21); Stretch(v.exhaustCount.rectTransform);
            v.discardAnchor=Node("DiscardPile",piles,128,80); Background(v.discardAnchor,new Color(.22f,.23f,.30f)); v.discardCount=Text("Count",v.discardAnchor,"弃牌堆\n0",21); Stretch(v.discardCount.rectTransform);
            v.status=Text("Status",battle,"",15,20);
            v.effects=Node("Effects",battle); Stretch(v.effects); v.effects.GetComponent<LayoutElement>().ignoreLayout=true;
            EditorUtility.SetDirty(v); PrefabUtility.SaveAsPrefabAsset(root,Path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
