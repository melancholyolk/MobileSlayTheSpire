using System;
using System.Linq;
using Spire;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpirePresentation
{
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }
        public Run State => network!=null ? network.Run : solo;
        public int Player => network!=null ? network.LocalPlayer : 0;
        Run solo;
        CoopSession network;
        byte[] table;
        Font font;
        RectTransform page;
        string address="127.0.0.1", status="Choose a class and begin the ascent.", resumeToken;
        int lastRevision=-1;
        string lastNetworkStatus;
        readonly Color paper=new Color(.88f,.84f,.74f), accent=new Color(.67f,.39f,.24f), ink=new Color(.09f,.10f,.12f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(SceneManager.GetActiveScene().name=="GameStart" && FindObjectOfType<GameApp>()==null)
                new GameObject("SpireGame").AddComponent<GameApp>();
        }
        void Awake()
        {
            Instance=this; Application.runInBackground=true;
            table=Resources.Load<TextAsset>("Tables/cards").bytes;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(FindObjectOfType<EventSystem>()==null) new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var canvas=new GameObject("SpireCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
            page=canvas.GetComponent<RectTransform>(); Render();
        }
        void Update()
        {
            network?.Pump();
            if(network!=null && (lastRevision!=(State?.Revision ?? -1) || lastNetworkStatus!=network.Status)) Render();
        }
        void OnDestroy() { network?.Dispose(); if(Instance==this) Instance=null; }
        public void StartSolo(HeroClass cls,uint seed=42)
        {
            network?.Dispose(); network=null; solo=new Run(CardTable.Read(table),seed,cls); status="Choose a route."; Render();
        }
        public void Send(string action,int argument=0)
        {
            if(network!=null) { network.Submit(action,argument); status=network.Status; }
            else if(solo!=null) { if(!solo.Apply(0,action,argument,out var error)) status=error; else status=""; }
            Render();
        }
        void Host()
        {
            try { network?.Dispose(); network=new CoopSession(table); network.Host(7777,42); solo=null; Render(); }
            catch(Exception e) { status=e.Message; network?.Dispose(); network=null; Render(); }
        }
        void Join(bool resume)
        {
            try
            {
                if(resume && network!=null) resumeToken=network.ResumeToken;
                network?.Dispose(); network=new CoopSession(table); network.Join(address,7777,resume?resumeToken:null); solo=null; Render();
            }
            catch(Exception e) { status=e.Message; network?.Dispose(); network=null; Render(); }
        }
        RectTransform Box(string name,float x,float y,float w,float h,Color color,Transform parent=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent??page,false);
            var rt=go.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1); rt.anchoredPosition=new Vector2(x,-y); rt.sizeDelta=new Vector2(w,h);
            go.GetComponent<Image>().color=color; return rt;
        }
        Text Label(string value,float x,float y,float w,float h,int size=20,Transform parent=null)
        {
            var go=new GameObject("Label",typeof(RectTransform)); go.transform.SetParent(parent??page,false);
            var rt=go.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(x,-y); rt.sizeDelta=new Vector2(w,h);
            var t=rt.gameObject.AddComponent<Text>(); t.text=value; t.font=font; t.fontSize=size; t.color=paper; t.raycastTarget=false; t.verticalOverflow=VerticalWrapMode.Truncate;
            return t;
        }
        void Button(string name,string caption,float x,float y,float w,float h,Action action,bool enabled=true)
        {
            var rt=Box(name,x,y,w,h,enabled?accent:new Color(.22f,.23f,.25f));
            var button=rt.gameObject.AddComponent<Button>(); button.interactable=enabled;
            button.onClick.AddListener(()=>action());
            var t=Label(caption,8,6,w-16,h-12,18,rt); t.alignment=TextAnchor.MiddleCenter;
        }
        public void Render()
        {
            foreach(Transform child in page) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            Box("Background",0,0,1280,720,ink);
            Label("ASHEN ASCENT",32,20,780,55,36);
            Label("A cooperative deckbuilding prototype",34,70,800,30,17);
            var run=State; lastRevision=run?.Revision??-1; lastNetworkStatus=network?.Status;
            Label(network?.Status??status,32,672,1040,40,18);
            Button("Menu","Menu",1140,25,108,42,()=>{ network?.Dispose(); network=null; solo=null; status="Choose a class."; Render(); });
            if(run==null)
            {
                Label("Choose your path",80,150,600,50,30);
                Button("Warrior","WARRIOR\nStrength & steel",80,230,260,130,()=>StartSolo(HeroClass.Warrior));
                Button("Hunter","HUNTER\nPoison & precision",370,230,260,130,()=>StartSolo(HeroClass.Hunter));
                Button("Host","Host co-op\nPlay as Warrior",80,410,260,90,Host);
                Button("Join","Join co-op\nPlay as Hunter",370,410,260,90,()=>Join(false));
                var inputRoot=Box("HostAddress",80,535,550,48,new Color(.2f,.21f,.23f));
                var input=inputRoot.gameObject.AddComponent<InputField>(); var t=Label(address,10,8,530,32,20,inputRoot);
                input.textComponent=t; input.text=address; input.onValueChanged.AddListener(v=>address=v);
                Label("Host address / TCP 7777",80,590,550,30,17);
                Label("Eight floors. One shared route.\nBuild your deck, survive the Warden.\n\nCo-op: vote on rooms and play your\nown cards; enemies act after both\nplayers end their turn.",740,240,460,250,23);
                return;
            }
            for(int p=0;p<run.Heroes.Count;p++)
            {
                var h=run.Heroes[p]; Label($"{(p==Player?"> ":"")}{h.Class}  HP {h.Hp}/{h.MaxHp}  Gold {h.Gold}  Deck {h.Deck.Count}",32+p*610,115,610,35,22);
            }
            Label($"{run.Phase}   /   Floor {(run.Current<0?0:run.Map[run.Current].Floor+1)} of {Run.Floors}",32,160,900,35,24);
            bool ready=network==null || network.Connected;
            if(!ready && !network.IsHost) Button("Reconnect","Reconnect",1080,665,160,40,()=>Join(true));
            var hero=run.Heroes[Player];
            switch(run.Phase)
            {
                case Phase.Map:
                    foreach(var n in run.Map)
                    {
                        bool reachable=run.Choices.Contains(n.Id); int id=n.Id;
                        string mark=run.Votes.Values.Contains(id)?" [VOTE]":"";
                        Button("Node_"+id,$"{n.Floor+1}\n{n.Kind}{mark}",32+n.Floor*153,245+n.Lane*116,139,94,()=>Send("vote",id),reachable && ready && hero.Alive);
                    }
                    break;
                case Phase.Combat:
                    var enemy=run.Enemies[0];
                    Box("EnemyPanel",340,225,600,170,new Color(.22f,.15f,.17f));
                    Label($"{enemy.Name}    HP {enemy.Hp}\nIntent: attack all for {enemy.Damage+(run.Turn%3==0?3:0)}\nPoison {enemy.Poison}",370,245,550,130,26);
                    Label($"Turn {run.Turn}     Energy {hero.Energy}     Block {hero.Block}     Strength {hero.Strength}\nDraw {hero.Draw.Count}  /  Discard {hero.Discard.Count}  /  Exhaust {hero.Exhausted.Count}",32,410,1000,65,21);
                    float width=Math.Min(190,1180f/Math.Max(1,hero.Hand.Count));
                    for(int i=0;i<hero.Hand.Count;i++)
                    {
                        int index=i; var c=run.Cards[hero.Hand[i]];
                        Button("Card_"+i,$"{c.Name}\nCost {c.Cost}\n{c.Effect} {c.Amount}{(c.Hits>1?" x"+c.Hits:"")}\n{(c.Exhaust?"Exhaust":"")}",32+i*(width+4),495,width-4,155,()=>Send("play",index),ready && hero.Alive && !hero.Ended && hero.Energy>=c.Cost);
                    }
                    Button("EndTurn",hero.Ended?"Waiting...":"End turn",1060,400,185,70,()=>Send("end"),ready && hero.Alive && !hero.Ended);
                    break;
                case Phase.Reward:
                    Label("Victory — choose one card, or skip.",60,240,1100,50,27);
                    for(int i=0;i<run.Rewards[Player].Count;i++)
                    {
                        int index=i; var c=run.Cards[run.Rewards[Player][i]];
                        Button("Reward_"+i,$"{c.Name}\nCost {c.Cost} / {c.Effect} {c.Amount}",60+i*380,330,350,150,()=>Send("reward",index),ready && !run.Resolved.Contains(Player) && hero.Alive);
                    }
                    Button("SkipReward","Skip",500,540,280,60,()=>Send("reward",-1),ready && !run.Resolved.Contains(Player) && hero.Alive); break;
                case Phase.Camp: Room("Rest: heal 33%","Train: +5 max HP"); break;
                case Phase.Shop: Room("Heal 25 HP: 30 gold","Leave shop"); break;
                case Phase.Event: Room("Pay 6 HP: gain 35 gold","Walk away"); break;
                case Phase.Won: case Phase.Lost:
                    Label(run.Phase==Phase.Won?"THE WARDEN FALLS":"THE ASCENT ENDS",220,300,900,90,48);
                    Label("Return to the menu to begin a new run.",300,420,800,60,25); break;
            }
            void Room(string a,string b)
            {
                bool enabled=ready && hero.Alive && !run.Resolved.Contains(Player);
                Button("Room_0",a,160,320,440,150,()=>Send("room",0),enabled);
                Button("Room_1",b,660,320,440,150,()=>Send("room",1),enabled);
            }
        }
    }
}
