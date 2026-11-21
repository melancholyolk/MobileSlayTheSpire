using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spire;
using SpireUI;
using SpireResources;
using SpireLocalization;
using UnityEngine;
using UnityEngine.UI;
namespace SpirePresentation
{
    public enum StartPage { Home, Solo, Multiplayer, Lobby }
    public sealed class GameStartView : BaseUIView
    {
        public const int Path=ResourceIds.GameStart;
        [SerializeField] GameObject home, selection, connection, lobby;
        [SerializeField] RectTransform choices;
        [SerializeField] Button single, multiplayer, back, host, join, ready, start, language;
        [SerializeField] InputField address;
        [SerializeField] Text title, status, players;
        GameApp app;
        readonly List<ResourceScope> previews=new List<ResourceScope>();
        int generation;
        StartPage shown=(StartPage)(-1);
        public Task ChoicesReady { get; private set; }=Task.CompletedTask;
        public void Bind(GameApp owner) { app=owner; Refresh(); }
        protected override void OnInitialize()
        {
            single.onClick.AddListener(()=>app.Navigate(StartPage.Solo));
            multiplayer.onClick.AddListener(()=>app.Navigate(StartPage.Multiplayer));
            back.onClick.AddListener(()=>app.ReturnToMenu());
            host.onClick.AddListener(()=>app.Host());
            join.onClick.AddListener(()=> { app.HostAddress=address.text; app.Join(false); });
            ready.onClick.AddListener(()=>app.Network.SetReady(!app.Network.GuestReady));
            start.onClick.AddListener(()=>app.Network.StartGame());
            language.onClick.AddListener(()=>app.CycleLanguage());
            foreach(var text in GetComponentsInChildren<Text>(true)) text.font=UiFonts.Body;
        }
        public void Refresh()
        {
            if(app==null) return;
            var page=app.MenuPage;
            title.text=ExcelLocalization.Text(LocIds.UiTitle);
            Set(single,LocIds.UiSingle); Set(multiplayer,LocIds.UiMultiplayer); Set(back,LocIds.UiBack);
            Set(host,LocIds.UiCreateRoom); Set(join,LocIds.UiJoinRoom); Set(start,LocIds.UiStartGame);
            Set(ready,app.Network!=null && app.Network.GuestReady?LocIds.UiUnready:LocIds.UiReady);
            home.SetActive(page==StartPage.Home); connection.SetActive(page==StartPage.Multiplayer);
            selection.SetActive(page==StartPage.Solo || page==StartPage.Lobby); lobby.SetActive(page==StartPage.Lobby);
            back.gameObject.SetActive(page!=StartPage.Home);
            status.text=page==StartPage.Home?"":app.MenuStatus;
            if(page==StartPage.Multiplayer) status.text=ExcelLocalization.Text(LocIds.UiLocalIp)+": "+app.LocalAddress+"  TCP 7777\n"+app.MenuStatus;
            if(page==StartPage.Lobby && app.Network!=null)
            {
                var n=app.Network;
                start.gameObject.SetActive(n.IsHost); start.interactable=n.CanStart;
                ready.gameObject.SetActive(!n.IsHost); ready.interactable=n.Connected && n.SelectedCharacter(1)>=0;
                players.text=ExcelLocalization.Text(LocIds.UiLocalIp)+": "+app.LocalAddress+"\n"+
                    ExcelLocalization.Text(LocIds.UiHostSeat)+": "+CharacterName(n.SelectedCharacter(0))+"\n"+
                    ExcelLocalization.Text(LocIds.UiGuestSeat)+": "+CharacterName(n.SelectedCharacter(1))+"  "+
                    ExcelLocalization.Text(n.GuestReady?LocIds.UiReady:LocIds.UiUnready);
            }
            if(shown!=page) { shown=page; ChoicesReady=RebuildChoices(); }
        }
        string CharacterName(int id)
        {
            var card=app.Characters.FirstOrDefault(c=>(int)c.Class==id);
            return card==null?"—":ExcelLocalization.Text(card.CharacterNameId);
        }
        void Set(Button b,int id) { b.GetComponentInChildren<Text>(true).text=ExcelLocalization.Text(id); }
        public void RefreshLanguage() { shown=(StartPage)(-1); Refresh(); }
        async Task RebuildChoices()
        {
            int version=++generation;
            foreach(var scope in previews) scope.Dispose(); previews.Clear();
            if(app.MenuPage!=StartPage.Solo && app.MenuPage!=StartPage.Lobby) return;
            try
            {
                int index=0;
                foreach(var card in app.Characters)
                {
                    if(version!=generation || this==null) return;
                    var scope=app.Resources.CreateScope((int)card.Class); previews.Add(scope);
                    var go=await scope.InstantiateAsync(ResourceIds.CardView,choices,false);
                    if(version!=generation || this==null) { scope.Dispose(); return; }
                    var rt=go.GetComponent<RectTransform>(); rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);
                    rt.anchoredPosition=new Vector2(index*244,0); rt.sizeDelta=new Vector2(224,330);
                    go.name="Character_"+(int)card.Class; go.SetActive(true);
                    var view=go.GetComponent<CardUIView>(); Register(view);
                    await view.BindCharacter(card,scope,()=> {
                        if(app.MenuPage==StartPage.Solo) app.StartSolo(card.Class);
                        else app.Network.SelectCharacter((int)card.Class);
                    });
                    index++;
                }
                choices.sizeDelta=new Vector2(Math.Max(1100,index*244),330);
            }
            catch(OperationCanceledException) { }
            catch(ObjectDisposedException) { }
            catch(Exception e) { Debug.LogException(e); }
        }
        protected override void OnHidden()
        {
            generation++; foreach(var scope in previews) scope.Dispose(); previews.Clear(); shown=(StartPage)(-1);
        }
        protected override void OnReleased() { OnHidden(); app=null; }
    }
    public static class UiFonts
    {
        static Font body;
        public static Font Body
        {
            get { if(body==null) body=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","PingFang SC","Droid Sans Fallback","Arial"},24); return body; }
        }
    }
}
