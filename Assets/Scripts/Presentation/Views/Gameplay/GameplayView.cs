using SpireUI;
using UnityEngine;
namespace SpirePresentation
{
    public sealed class GameplayView : BaseUIView
    {
        public const int Path=2;
        [SerializeField] RectTransform content;
        public RectTransform Content=>content;
    }
}
