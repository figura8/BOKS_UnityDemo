using UnityEngine;
using UnityEngine.EventSystems;

namespace BOKS.Demo
{
    /// <summary>Source decoration squash: 520 ms, cubic-bezier(.22,.76,.24,1), 760 ms cooldown.</summary>
    public sealed class BOKSDecorationReaction : MonoBehaviour, IPointerDownHandler
    {
        const float Duration = .520f;
        const float Cooldown = .760f;
        Vector3 restScale;
        Quaternion restRotation;
        float until;
        float nextAllowed;

        public int Column { get; private set; }
        public int Row { get; private set; }
        public void Configure(int column, int row)
        {
            Column = column;
            Row = row;
            restScale = transform.localScale;
            restRotation = transform.localRotation;
        }

        // The web implementation starts on pointerdown, not pointer-up/click.
        public void OnPointerDown(PointerEventData eventData) => Trigger();

        public bool Trigger()
        {
            if (Time.unscaledTime < nextAllowed) return false;
            nextAllowed = Time.unscaledTime + Cooldown;
            // Web triggerDecorationReaction plays the random rubber tap immediately with the
            // accepted reaction, before its 520 ms Web Animations squash begins.
            BOKSAudioManager.PlayDecorationRubberTap();
            until = Time.unscaledTime + Duration;
            restScale = transform.localScale;
            restRotation = transform.localRotation;
            return true;
        }

        void Update()
        {
            if (until <= 0f) return;
            float t = 1f - Mathf.Clamp01((until - Time.unscaledTime) / Duration);
            if (t >= 1f)
            {
                transform.localScale = restScale;
                transform.localRotation = restRotation;
                until = 0f;
                return;
            }
            // Keyframes at 0, .26, .62, 1. The source cubic-bezier is applied to the whole timing curve.
            float eased = CubicBezierYForX(t, .22f, .76f, .24f, 1f);
            if (eased <= .26f)
                Apply(Vector2.Lerp(Vector2.one, new Vector2(1.07f, .9f), eased / .26f), Mathf.Lerp(0f, -1f, eased / .26f));
            else if (eased <= .62f)
                Apply(Vector2.Lerp(new Vector2(1.07f, .9f), new Vector2(.965f, 1.045f), (eased - .26f) / .36f), Mathf.Lerp(-1f, .9f, (eased - .26f) / .36f));
            else
                Apply(Vector2.Lerp(new Vector2(.965f, 1.045f), Vector2.one, (eased - .62f) / .38f), Mathf.Lerp(.9f, 0f, (eased - .62f) / .38f));
        }

        void Apply(Vector2 scale, float rotation) { transform.localScale = Vector3.Scale(restScale, new Vector3(scale.x, scale.y, 1f)); transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, rotation); }
        static float CubicBezierYForX(float x, float x1, float y1, float x2, float y2)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 12; i++) { float u = (lo + hi) * .5f; float q = 1f - u; float bx = 3f * q * q * u * x1 + 3f * q * u * u * x2 + u * u * u; if (bx < x) lo = u; else hi = u; }
            float t = (lo + hi) * .5f, r = 1f - t;
            return 3f * r * r * t * y1 + 3f * r * t * t * y2 + t * t * t;
        }
    }
}
