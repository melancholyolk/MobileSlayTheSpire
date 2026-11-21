using System;
using Spire;
using SpireUI;
using SpireLocalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
namespace SpirePresentation
{
    public sealed class GameStartView : BaseUIView
    {
        public const string Path="UI/Views/GameStart/GameStartView";
        [SerializeField] Button warrior, hunter, host, join, language;
        [SerializeField] InputField hostAddress;
        [SerializeField] Text title, subtitle, choose, warriorCaption, hunterCaption, hostCaption, joinCaption, languageCaption, status, addressCaption;
        GameApp app;
        public void Bind(GameApp owner)
        {
            app=owner;
            hostAddress.SetTextWithoutNotify(app.HostAddress);
            Refresh();
        }
        protected override void OnInitialize()
        {
            warrior.onClick.AddListener(StartWarrior); hunter.onClick.AddListener(StartHunter);
            host.onClick.AddListener(Host); join.onClick.AddListener(Join);
            language.onClick.AddListener(ChangeLanguage); hostAddress.onValueChanged.AddListener(AddressChanged);
            foreach(var text in GetComponentsInChildren<Text>(true)) text.font=UiFonts.Body;
        }
        void StartWarrior() { app?.StartSolo(HeroClass.Warrior); }
        void StartHunter() { app?.StartSolo(HeroClass.Hunter); }
        void Host() { app?.Host(); }
        void Join() { app?.Join(false); }
        void ChangeLanguage() { app?.CycleLanguage(); }
        void AddressChanged(string value) { if(app!=null) app.HostAddress=value; }
        protected override void OnShown() { LocalizationSettings.SelectedLocaleChanged+=LocaleChanged; Refresh(); }
        protected override void OnHidden() { LocalizationSettings.SelectedLocaleChanged-=LocaleChanged; }
        void LocaleChanged(Locale locale) { Refresh(); }
        public void Refresh()
        {
            title.text=ExcelLocalization.Text(LocIds.UiTitle);
            subtitle.text=ExcelLocalization.Text(LocIds.UiStartTagline);
            choose.text=ExcelLocalization.Text(LocIds.UiChoose);
            warriorCaption.text=ExcelLocalization.Text(LocIds.UiWarrior);
            hunterCaption.text=ExcelLocalization.Text(LocIds.UiHunter);
            hostCaption.text=ExcelLocalization.Text(LocIds.UiHost).Replace('\n',' ');
            joinCaption.text=ExcelLocalization.Text(LocIds.UiJoin).Replace('\n',' ');
            languageCaption.text=ExcelLocalization.Language=="zh-Hans"?"EN / 中文":"EN / 中";
            addressCaption.text=ExcelLocalization.Text(LocIds.UiHostAddress);
            status.text=app?.MenuStatus??"";
        }
        protected override void OnReleased()
        {
            warrior.onClick.RemoveListener(StartWarrior); hunter.onClick.RemoveListener(StartHunter);
            host.onClick.RemoveListener(Host); join.onClick.RemoveListener(Join);
            language.onClick.RemoveListener(ChangeLanguage); hostAddress.onValueChanged.RemoveListener(AddressChanged);
            app=null;
        }
    }
    public static class UiFonts
    {
        static Font body;
        public static Font Body
        {
            get
            {
                if(body==null) body=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Noto Sans CJK SC","PingFang SC","Droid Sans Fallback","Arial"},24);
                return body;
            }
        }
    }
}
