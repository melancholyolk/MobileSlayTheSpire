using System;
using System.Threading.Tasks;
using SpireUI;
using SpireResources;
using SpireAudio;
using SpireAnimation;
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
    /// <summary>
    /// 应用组装入口。生命周期和共享状态在此；会话、资源、页面与战斗 HUD 按 partial 文件分工。
    /// 业务规则统一提交给 Run，异步视图使用版本号阻止过期结果回写。
    /// </summary>
    public sealed partial class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }
        public Run State => network != null ? network.Run : solo;
        public int Player => network != null ? network.LocalPlayer : 0;

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
        readonly Dictionary<int, FmodAudioScope> characterAudio = new Dictionary<int, FmodAudioScope>();
        bool destroying;
        public BattlePresentation BattleVisuals { get; private set; }
        public string AudioStatus => audioManager == null ? "Not initialized" : audioManager.Status;

        readonly Dictionary<int, ResourceScope> characterResources = new Dictionary<int, ResourceScope>();
        readonly List<(GameObject go, ResourceScope scope)> visibleCards = new List<(GameObject, ResourceScope)>();
        int renderVersion;
        BattleHandLayout handMotion;
        Run handRun;
        public Card[] Characters { get; private set; }
        public CoopSession Network => network;
        public StartPage MenuPage { get; private set; } = StartPage.Home;
        public string LocalAddress { get; private set; } = "127.0.0.1";
        public Task CardsReady { get; private set; } = Task.CompletedTask;

        MapUIView mapView;
        UiTheme uiTheme;
        GameStartView startView;
        GameplayView gameplayView;
        public Task UiReady { get; private set; }
        public UiManager UI => ui;
        public string HostAddress { get => address; set => address = value; }
        public string MenuStatus => network?.Status ?? status;

        string address = "127.0.0.1", status = "", resumeToken;
        int lastRevision = -1;
        string lastNetworkStatus;
        readonly Color paper = new Color(.88f, .84f, .74f), accent = new Color(.67f, .39f, .24f), ink = new Color(.09f, .10f, .12f);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name == "GameStart" && FindObjectOfType<GameApp>() == null)
                new GameObject("SpireGame").AddComponent<GameApp>();
        }

        void Awake()
        {
            Instance = this;
            Application.runInBackground = true;
            Resources = ResourceManager.Create(gameObject);
            appResources = Resources.CreateScope();
            try
            {
                LocalAddress = Dns.GetHostAddresses(Dns.GetHostName()).First(a => a.AddressFamily == AddressFamily.InterNetwork).ToString();
            }
            catch
            {
            }

            font = UiFonts.Body;
            ExcelLocalization.Initialize();
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = new GameObject("SpireCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            ui = gameObject.AddComponent<UiManager>();
            ui.Configure(canvas.transform, appResources);
            UiReady = LoadViews();
        }

        async Task LoadViews()
        {
            try
            {
                audioManager = gameObject.AddComponent<FmodAudioManager>();
                await audioManager.ConfigureAsync(appResources);
                if (audioManager.TryInitialize())
                    appAudio = await audioManager.CreateScopeAsync(appResources);
                uiTheme = await appResources.LoadAsync<UiTheme>(30);
                var mapTheme = await appResources.LoadAsync<MapTheme>(31);
                mapView = await ui.OpenAsync<MapUIView>(MapUIView.Path);
                mapView.Bind(this, mapTheme);
                ui.Hide(MapUIView.Path);
                table = (await appResources.LoadAsync<TextAsset>(ResourceIds.Cards)).bytes;
                Characters = CardTable.Read(table).GroupBy(c => c.Class).Select(g => g.First()).OrderBy(c => (int)c.Class).ToArray();
                gameplayView = await ui.OpenAsync<GameplayView>(GameplayView.Path);
                if (this == null)
                    return;
                ui.Hide(GameplayView.Path);
                page = gameplayView.Content;
                gameplayView.Bind(this, uiTheme);
                BattleVisuals = gameObject.AddComponent<BattlePresentation>();
                BattleVisuals.IsCombatAuthority = () => network == null || network.IsHost;
                BattleVisuals.AttackResolved = (id, part) =>
                {
                    if (network != null)
                        network.ResolveAttack(id, part);
                    else if (solo != null)
                        solo.ResolveAttack(id, part);
                    Render();
                };
                BattleVisuals.Configure(Resources, gameplayView.effects);
                BattleVisuals.ConfigureLayout(gameplayView.heroAnchors, gameplayView.enemyAnchors, gameplayView.hand, gameplayView.drawAnchor, gameplayView.discardAnchor, gameplayView.exhaustAnchor, gameplayView.playAnchor);
                handMotion = gameplayView.hand.GetComponent<BattleHandLayout>();
                BattleVisuals.CardPresenter = cue => handMotion.Present(cue);
                startView = await ui.OpenAsync<GameStartView>(GameStartView.Path);
                if (this == null)
                    return;
                startView.Bind(this);
                Render();
            }
            catch (OperationCanceledException)
            {
                if (this != null)
                    throw;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                throw;
            }
        }

        public void CycleLanguage()
        {
            var languages = ExcelLocalization.Languages;
            int index = Array.IndexOf(languages.ToArray(), ExcelLocalization.Language);
            ExcelLocalization.Select(languages[(index + 1) % languages.Count]);
        }

        void Update()
        {
            network?.Pump();
            if (network != null && (lastRevision != (State?.Revision ?? -1) || lastNetworkStatus != network.Status))
                Render();
        }

        void OnLocaleChanged(Locale locale)
        {
            if (startView != null)
                startView.RefreshLanguage();
            if (page != null)
                Render();
        }

        static string L(int key) => ExcelLocalization.Text(key);
        void OnDestroy()
        {
            destroying = true;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            StopNetwork();
            ReleaseCharacters();
            appResources?.Dispose();
            if (Instance == this)
                Instance = null;
        }
    }
}
