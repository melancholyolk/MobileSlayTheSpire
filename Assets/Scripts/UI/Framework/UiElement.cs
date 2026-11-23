using UnityEngine;

namespace SpireUI
{
    // Adapter for Unity Graphics/Selectable objects; derived elements can own their subscriptions.
    public class UiElement : MonoBehaviour, IUiElement
    {
        public BaseUIView Owner { get; private set; }

        public virtual void Initialize(BaseUIView owner)
        {
            Owner = owner;
        }

        public virtual void OnShow()
        {
        }

        public virtual void OnHide()
        {
        }

        protected virtual void OnDestroy()
        {
            if (Owner != null)
                Owner.Unregister(this);
        }

        public virtual void Release()
        {
            Owner = null;
        }
    }
}
