using UnityEngine;
namespace Unknown.Battle
{
    // Position/size animation is independent of the hover scale and drag ghost.
    public sealed class HandCardMotion : MonoBehaviour
    {
        public Vector2 Destination { get; private set; }
        public bool Held;
        RectTransform rect;
        Vector2 from, sizeFrom, sizeTo;
        float elapsed, duration;
        public void Layout(Vector2 position, Vector2 size, float seconds, bool immediate)
        {
            if (rect == null) rect = (RectTransform)transform;
            if (!immediate && Destination == position && sizeTo == size) return;
            Destination = position; sizeTo = size; from = rect.anchoredPosition; sizeFrom = rect.sizeDelta;
            elapsed = 0; duration = Mathf.Max(0.01f, seconds);
            if (immediate) { rect.anchoredPosition = position; rect.sizeDelta = size; elapsed = duration; }
        }
        void Update()
        {
            if (Held || elapsed >= duration || rect == null) return;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
            rect.anchoredPosition = Vector2.Lerp(from, Destination, t);
            rect.sizeDelta = Vector2.Lerp(sizeFrom, sizeTo, t);
        }
    }
}
