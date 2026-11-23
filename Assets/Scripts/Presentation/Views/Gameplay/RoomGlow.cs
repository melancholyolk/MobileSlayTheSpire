using UnityEngine;

namespace SpirePresentation
{
    // Ambient motion only; all visual composition and tuning are authored on the prefab.
    public sealed class RoomGlow : MonoBehaviour
    {
        [SerializeField]
        CanvasGroup glow;
        [SerializeField]
        float minimum = .35f, maximum = .7f, period = 5f;
        void Update()
        {
            if (glow != null)
                glow.alpha = Mathf.Lerp(minimum, maximum, (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / Mathf.Max(.1f, period)) + 1) * .5f);
        }
    }
}
