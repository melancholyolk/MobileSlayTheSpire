using UnityEngine;

namespace SpirePresentation
{
    [CreateAssetMenu(menuName = "Spire/Map Theme")]
    public sealed class MapTheme : ScriptableObject
    {
        public Sprite Background;
        public Sprite[] Rooms;
        public Texture2D Road;
        public Color Future = new Color(.24f, .22f, .20f, .65f), Visited = new Color(.72f, .30f, .12f), Available = new Color(.15f, .33f, .32f), Blocked = new Color(.35f, .33f, .30f, .23f);
    }
}
