using UnityEngine;
using UnityEngine.UI;

namespace SpireUI
{
    /// <summary>
    /// Invisible rectangular UI raycast target. Pair with a Button or pointer event handler.
    /// </summary>
    [AddComponentMenu("UI/Mono Empty For Raycast")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MonoEmptyForRaycast : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            // Keep Graphic raycasting and CanvasGroup filtering without rendering geometry.
            vertexHelper.Clear();
        }
    }
}
