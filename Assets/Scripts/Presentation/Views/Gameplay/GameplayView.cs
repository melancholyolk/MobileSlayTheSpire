using SpireUI;
using UnityEngine;
namespace SpirePresentation
{
    public sealed class GameplayView : BaseUIView
    {
        public const string Path="UI/Views/Gameplay/GameplayView";
        [SerializeField] RectTransform content;
        public RectTransform Content=>content;
    }
}
