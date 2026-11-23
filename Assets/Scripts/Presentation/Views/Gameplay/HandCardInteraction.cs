using System.Threading.Tasks;
using SpireAnimation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpirePresentation
{
    [RequireComponent(typeof(BetterAnimation), typeof(CanvasGroup))]
    public sealed class HandCardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        BattleHandLayout hand;
        bool dragging;
        public string CardId { get; private set; }

        public bool Locked;
        public BetterAnimation Motion { get; private set; }
        public CanvasGroup Group { get; private set; }

        public Task Transition = Task.CompletedTask;
        public void Configure(BattleHandLayout owner, string id)
        {
            hand = owner;
            CardId = id;
            Motion = GetComponent<BetterAnimation>();
            Group = GetComponent<CanvasGroup>();
            Locked = false;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Locked)
                hand.Hover(this, true);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (!Locked)
                hand.Hover(this, false);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Locked && !dragging && e.button == PointerEventData.InputButton.Left && !e.dragging)
                hand.Play(this);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (!Locked && e.button == PointerEventData.InputButton.Left)
                dragging = hand.BeginDrag(this);
        }

        public void OnDrag(PointerEventData e)
        {
            if (dragging)
                hand.Drag(this, e.position, e.pressEventCamera);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (dragging)
            {
                dragging = false;
                hand.EndDrag(this);
            }
        }
    }
}
