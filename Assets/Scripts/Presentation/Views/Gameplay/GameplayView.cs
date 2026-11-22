using SpireUI;
using UnityEngine;
using UnityEngine.UI;
namespace SpirePresentation
{
    public sealed class GameplayView : BaseUIView
    {
        public const int Path=2;
        [SerializeField] RectTransform content;
        public RectTransform Content=>content;
        [Header("Authored battle layout")]
        public GameObject battleRoot;
        public RectTransform hand, effects, drawAnchor, discardAnchor, exhaustAnchor, playAnchor;
        public RectTransform[] heroAnchors, enemyAnchors;
        public Image portrait, playerHealthFill, enemyHealthFill;
        public Text relics, potions, boss, mapInfo, characterName, playerHealth, energy, handCount, playerStatus;
        public Text turn, enemyIntent, enemyName, enemyHealth, enemyStatus, drawCount, discardCount, exhaustCount, handTitle, status;
        public Button language, menu, endTurn, reconnect;
        public void ShowCombat(bool combat)
        {
            battleRoot.SetActive(combat);
            content.gameObject.SetActive(!combat);
        }
        public void Bind(GameApp app,UiTheme theme)
        {
            foreach(var text in battleRoot.GetComponentsInChildren<Text>(true)) text.font=UiFonts.Body;
            language.onClick.AddListener(app.CycleLanguage);
            menu.onClick.AddListener(app.ReturnToMenu);
            endTurn.onClick.AddListener(()=>app.Send("end"));
            reconnect.onClick.AddListener(()=>app.Join(true));
            theme.Apply(language); theme.Apply(menu,ButtonIntent.Destructive);
            theme.Apply(endTurn,ButtonIntent.Primary); theme.Apply(reconnect,ButtonIntent.Primary);
        }
    }
}
