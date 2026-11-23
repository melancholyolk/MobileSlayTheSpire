using System;
using System.Threading.Tasks;
using Spire;
using SpireUI;
using SpireResources;
using SpireLocalization;
using UnityEngine;
using UnityEngine.UI;

namespace SpirePresentation
{
    public sealed class CardUIView : UiElement
    {
        [SerializeField]
        Image artwork;
        [SerializeField]
        Text title, cost, description;
        [SerializeField]
        Button button;
        int generation;
        public async Task Bind(Card card, ResourceScope owner, Action action, bool interactable)
        {
            int version = ++generation;
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.font == null)
                    text.font = UiFonts.Body;
            title.text = ExcelLocalization.Text(card.NameId);
            cost.text = card.Cost.ToString();
            description.text = card.Effect + " " + card.Amount + (card.Hits > 1 ? " ×" + card.Hits : "") + (card.Exhaust ? "\n" + ExcelLocalization.Text(LocIds.UiExhaust) : "");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
            button.interactable = interactable;
            artwork.sprite = null;
            var sprite = await owner.LoadAsync<Sprite>(card.ArtId);
            if (this != null && generation == version && !owner.IsDisposed)
                artwork.sprite = sprite;
        }

        public async Task BindCharacter(Card character, ResourceScope owner, Action action)
        {
            int version = ++generation;
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.font == null)
                    text.font = UiFonts.Body;
            title.text = ExcelLocalization.Text(character.CharacterNameId);
            cost.text = "";
            description.text = "HP " + character.CharacterMaxHp;
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action());
            artwork.sprite = null;
            var sprite = await owner.LoadAsync<Sprite>(character.PortraitId);
            if (this != null && generation == version && !owner.IsDisposed)
                artwork.sprite = sprite;
        }

        public void Clear()
        {
            generation++;
            artwork.sprite = null;
            button.onClick.RemoveAllListeners();
        }

        void OnDisable()
        {
            Clear();
        }

        public override void Release()
        {
            Clear();
            base.Release();
        }
    }
}
