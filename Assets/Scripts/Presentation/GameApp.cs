using System;
using System.Threading.Tasks;
using SpireUI;
using SpireResources;
using SpireAudio;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using Spire;
using SpireLocalization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
        UiManager ui;
        public ResourceManager Resources { get; private set; }
        ResourceScope appResources;
        FmodAudioManager audioManager;
        FmodAudioScope appAudio;
        readonly Dictionary<int,FmodAudioScope> characterAudio=new Dictionary<int,FmodAudioScope>();
        bool destroying;
        public string AudioStatus=>audioManager==null?"Not initialized":audioManager.Status;
        void StopNetwork() { if(network==null) return; network.Changed-=Render; network.Dispose(); network=null; }

        readonly Dictionary<int,ResourceScope> characterResources=new Dictionary<int,ResourceScope>();
        readonly List<(GameObject go,ResourceScope scope)> visibleCards=new List<(GameObject,ResourceScope)>();
        int renderVersion;
        public Card[] Characters { get; private set; }
        public CoopSession Network=>network;
        public StartPage MenuPage { get; private set; }=StartPage.Home;
        public string LocalAddress { get; private set; }="127.0.0.1";
        public Task CardsReady { get; private set; }=Task.CompletedTask;
        public void Navigate(StartPage page) { appAudio?.TryPlay(1); MenuPage=page; Render(); }
        void ReleaseCharacters()
        {
            renderVersion++;
            ClearCards();
            foreach(var scope in characterResources.Values) scope.Dispose();
            characterResources.Clear(); characterAudio.Clear();
        }
        void ClearCards()
        {
            foreach(var item in visibleCards)
            {
                if(item.go!=null) { item.go.GetComponent<CardUIView>().Clear(); gameplayView.Unregister(item.go.GetComponent<CardUIView>()); item.scope.Return(item.go); }
            }
            visibleCards.Clear();
        }
        ResourceScope CharacterScope(int seat)
        {
            if(!characterResources.TryGetValue(seat,out var scope))
            {
                characterResources[seat]=scope=Resources.CreateScope((int)State.Heroes[seat].Class);
                _=LoadCharacterAudio(seat,scope);
            }
            return scope;
        }

        GameStartView startView;
        GameplayView gameplayView;
        async Task LoadCharacterAudio(int seat,ResourceScope scope)
        {
            if(audioManager==null || !audioManager.Available) return;
            try { var audio=await audioManager.CreateScopeAsync(scope); if(!scope.IsDisposed) characterAudio[seat]=audio; }
            catch(OperationCanceledException) { }
            catch(ObjectDisposedException) { }
            catch(Exception error) { Debug.LogWarning(error.Message); }
        }
        public Task UiReady { get; private set; }
        public UiManager UI=>ui;
        public string HostAddress { get=>address; set=>address=value; }
        public string MenuStatus=>network?.Status??status;
        string address="127.0.0.1", status="", resumeToken;
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
            Resources=ResourceManager.Create(gameObject); appResources=Resources.CreateScope();
            try { LocalAddress=Dns.GetHostAddresses(Dns.GetHostName()).First(a=>a.AddressFamily==AddressFamily.InterNetwork).ToString(); } catch { }

            font=UiFonts.Body;
            ExcelLocalization.Initialize();
            LocalizationSettings.SelectedLocaleChanged+=OnLocaleChanged;
            if(FindObjectOfType<EventSystem>()==null) new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var canvas=new GameObject("SpireCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false); canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            ui=gameObject.AddComponent<UiManager>(); ui.Configure(canvas.transform,appResources);
            UiReady=LoadViews();
        }
        async Task LoadViews()
        {
            try
            {
                audioManager=gameObject.AddComponent<FmodAudioManager>();
                await audioManager.ConfigureAsync(appResources);
                if(audioManager.TryInitialize()) appAudio=await audioManager.CreateScopeAsync(appResources);
                table=(await appResources.LoadAsync<TextAsset>(ResourceIds.Cards)).bytes;
                Characters=CardTable.Read(table).GroupBy(c=>c.Class).Select(g=>g.First()).OrderBy(c=>(int)c.Class).ToArray();
                gameplayView=await ui.OpenAsync<GameplayView>(GameplayView.Path);
                if(this==null) return;
                ui.Hide(GameplayView.Path); page=gameplayView.Content;
                startView=await ui.OpenAsync<GameStartView>(GameStartView.Path);
                if(this==null) return;
                startView.Bind(this); Render();
            }
            catch(OperationCanceledException) { if(this!=null) throw; }
            catch(Exception error) { Debug.LogException(error); throw; }
        }
        public void ReturnToMenu()
        {
            StopNetwork(); solo=null; ReleaseCharacters(); MenuPage=StartPage.Home; status=""; Render();
        }
        public void CycleLanguage()
        {
            var languages=ExcelLocalization.Languages;
            int index=Array.IndexOf(languages.ToArray(),ExcelLocalization.Language);
            ExcelLocalization.Select(languages[(index+1)%languages.Count]);
        }
        void Update()
        {
            network?.Pump();
            if(network!=null && (lastRevision!=(State?.Revision ?? -1) || lastNetworkStatus!=network.Status)) Render();
        }
        void OnLocaleChanged(Locale locale) { if(startView!=null) startView.RefreshLanguage(); if(page!=null) Render(); }
        static string L(int key) => ExcelLocalization.Text(key);
        void OnDestroy() { destroying=true; LocalizationSettings.SelectedLocaleChanged-=OnLocaleChanged; StopNetwork(); ReleaseCharacters(); appResources?.Dispose(); if(Instance==this) Instance=null; }
        public void StartSolo(HeroClass cls,uint seed=42)
        {
            StopNetwork(); ReleaseCharacters(); solo=new Run(CardTable.Read(table),seed,cls); status="Choose a route."; Render();
        }
        public void Send(string action,int argument=0)
        {
            if(action=="play" && characterAudio.TryGetValue(Player,out var audio)) audio.TryPlay(2);
            if(network!=null) { network.Submit(action,argument); status=network.Status; }
            else if(solo!=null) { if(!solo.Apply(0,action,argument,out var error)) status=error; else status=""; }
            Render();
        }
        public void Host(int port=7777)
        {
            try { StopNetwork(); network=new CoopSession(table); network.Host(port,42); network.Changed+=Render; solo=null; MenuPage=StartPage.Lobby; Render(); }
            catch(Exception e) { status=e.Message; StopNetwork(); Render(); }
        }
        public void Join(bool resume,int port=7777)
        {
            try
            {
                if(resume && network!=null) resumeToken=network.ResumeToken;
                StopNetwork(); network=new CoopSession(table); network.Join(address,port,resume?resumeToken:null); network.Changed+=Render; solo=null; MenuPage=StartPage.Lobby; Render();
            }
            catch(Exception e) { status=e.Message; StopNetwork(); Render(); }
        }
        RectTransform Box(string name,float x,float y,float w,float h,Color color,Transform parent=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent??page,false);
            var rt=go.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1); rt.anchoredPosition=new Vector2(x,-y); rt.sizeDelta=new Vector2(w,h);
            go.GetComponent<Image>().color=color;
            var element=go.AddComponent<UiElement>(); gameplayView.Register(element); return rt;
        }
        Text Label(string value,float x,float y,float w,float h,int size=20,Transform parent=null)
        {
            var go=new GameObject("Label",typeof(RectTransform)); go.transform.SetParent(parent??page,false);
            var rt=go.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1);
            rt.anchoredPosition=new Vector2(x,-y); rt.sizeDelta=new Vector2(w,h);
            gameplayView.Register(go.AddComponent<UiElement>());
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
        async Task RenderCards(string[] cards,bool reward,bool enabled,int version)
        {
            if(cards.Length==0) return;
            var run=State; var scope=CharacterScope(Player);
            float width=Math.Min(reward?250:180,1160f/cards.Length);
            try
            {
                for(int i=0;i<cards.Length;i++)
                {
                    if(version!=renderVersion || scope.IsDisposed) return;
                    int index=i; var card=run.Cards[cards[i]];
                    var go=await scope.InstantiateAsync(ResourceIds.CardView,page);
                    if(version!=renderVersion || this==null) { scope.Return(go); return; }
                    var rt=go.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
                    rt.anchoredPosition=new Vector2(40+i*(width+4),reward?-310:-475); rt.sizeDelta=new Vector2(width-4,reward?220:180);
                    go.name=(reward?"Reward_":"Card_")+i; go.SetActive(true);
                    var view=go.GetComponent<CardUIView>(); gameplayView.Register(view); visibleCards.Add((go,scope));
                    await view.Bind(card,scope,()=>Send(reward?"reward":"play",index),enabled && (reward || run.Heroes[Player].Energy>=card.Cost));
                }
            }
            catch(OperationCanceledException) { }
            catch(ObjectDisposedException) { }
            catch(Exception e) { Debug.LogException(e); }
        }
        public void Render()
        {
            if(destroying || startView==null || gameplayView==null) return;
            lastRevision=State?.Revision??-1; lastNetworkStatus=network?.Status;
            if(State==null)
            {
                ui.Hide(GameplayView.Path); ui.Show(GameStartView.Path); startView.Refresh(); return;
            }
            ui.Hide(GameStartView.Path); ui.Show(GameplayView.Path);
            renderVersion++; ClearCards();
            foreach(Transform child in page) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var background=Box("Background",0,0,1280,720,ink);
            background.anchorMin=Vector2.zero; background.anchorMax=Vector2.one; background.offsetMin=background.offsetMax=Vector2.zero;
            Label(L(LocIds.UiTitle),32,20,780,55,36);
            Label(L(LocIds.UiSubtitle),34,70,800,30,17);
            var run=State; lastRevision=run?.Revision??-1; lastNetworkStatus=network?.Status;
            Label(network?.Status??status,32,672,1040,40,18);
            Button("Menu",L(LocIds.UiMenu),1140,25,108,42,ReturnToMenu);
            Button("Language",ExcelLocalization.Language,990,25,135,42,CycleLanguage);
            for(int p=0;p<run.Heroes.Count;p++)
            {
                var h=run.Heroes[p]; Label($"{(p==Player?"> ":"")}{ExcelLocalization.Text(Characters.First(c=>c.Class==h.Class).CharacterNameId)}  HP {h.Hp}/{h.MaxHp}  Gold {h.Gold}  Deck {h.Deck.Count}",32+p*610,115,610,35,22);
            }
            Label($"{run.Phase}   /   Floor {(run.Current<0?0:run.Map[run.Current].Floor+1)} of {Run.Floors}",32,160,900,35,24);
            bool ready=network==null || network.Connected;
            if(!ready && !network.IsHost) Button("Reconnect",L(LocIds.UiReconnect),1080,665,160,40,()=>Join(true));
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
                    CardsReady=RenderCards(hero.Hand.ToArray(),false,ready && hero.Alive && !hero.Ended,renderVersion);
                    Button("EndTurn",hero.Ended?L(LocIds.UiWait):L(LocIds.UiEnd),1060,400,185,70,()=>Send("end"),ready && hero.Alive && !hero.Ended);
                    break;
                case Phase.Reward:
                    Label(L(LocIds.UiReward),60,240,1100,50,27);
                    CardsReady=RenderCards(run.Rewards[Player].ToArray(),true,ready && !run.Resolved.Contains(Player) && hero.Alive,renderVersion);
                    Button("SkipReward",L(LocIds.UiSkip),500,540,280,60,()=>Send("reward",-1),ready && !run.Resolved.Contains(Player) && hero.Alive); break;
                case Phase.Camp: Room("Rest: heal 33%","Train: +5 max HP"); break;
                case Phase.Shop: Room("Heal 25 HP: 30 gold","Leave shop"); break;
                case Phase.Event: Room("Pay 6 HP: gain 35 gold","Walk away"); break;
                case Phase.Won: case Phase.Lost:
                    Label(run.Phase==Phase.Won?L(LocIds.UiWin):L(LocIds.UiLose),220,300,900,90,48);
                    Label(L(LocIds.UiRestart),300,420,800,60,25); break;
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
