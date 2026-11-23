using UnityEngine;
using UnityEngine.UI;

namespace SpirePresentation
{
    public enum ButtonIntent
    {
        Primary,
        Secondary,
        Destructive
    }

    [CreateAssetMenu(menuName = "Spire/UI Theme")]
    public sealed class UiTheme : ScriptableObject
    {
        public Sprite[] Normal, Hover, Pressed;
        public Sprite Disabled, CardFrame;
        public void Apply(Button button, ButtonIntent intent = ButtonIntent.Secondary)
        {
            int index = (int)intent;
            var image = button.GetComponent<Image>();
            image.sprite = Normal[index];
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Hover[index],
                selectedSprite = Hover[index],
                pressedSprite = Pressed[index],
                disabledSprite = Disabled
            };
        }
    }
}
