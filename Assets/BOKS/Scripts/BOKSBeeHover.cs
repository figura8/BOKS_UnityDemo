using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>Small procedural equivalent of the source bee-hover Lottie; no runtime dependency.</summary>
    public sealed class BOKSBeeHover : MonoBehaviour
    {
        const float LoopSeconds = 59f / 60f; // 59 frames / 30 fps at source runtimeSpeed 2.
        RectTransform upperWing;
        RectTransform lowerWing;
        RectTransform rect;
        float size;
        float boardWidth;
        float boardHeight;
        float x;
        float y;
        float startX;
        float startY;
        float targetX;
        float targetY;
        float legStart;
        float legDuration;
        float bobPhase;
        uint randomState;

        /// <summary>
        /// Mirrors the web bee actor: golden-ratio swarm placement then cosine-eased flight legs
        /// across the board. The web samples Math.random per leg; this uses a per-bee seeded stream
        /// so a Unity scene remains repeatable while retaining those exact source ranges.
        /// </summary>
        public void Build(float displaySize, int actorIndex, int swarmCount, float homeX, float homeY)
        {
            rect = (RectTransform)transform;
            boardWidth = ((RectTransform)transform.parent).rect.width;
            boardHeight = ((RectTransform)transform.parent).rect.height;
            size = Mathf.Max(10f, displaySize - Mathf.Min(actorIndex, 4));
            rect.sizeDelta = new Vector2(size, size);
            randomState = (uint)(actorIndex + 1) * 2654435761u;
            bobPhase = NextRandom() * Mathf.PI * 2f;

            float cellX = 75f / boardWidth;
            float cellY = 75f / boardHeight;
            float spread = .6f + Mathf.Sqrt(Mathf.Max(1, swarmCount)) * .42f;
            float radiusX = Mathf.Min(.34f, cellX * spread);
            float radiusY = Mathf.Min(.32f, cellY * (spread * .92f));
            float seed = Mathf.Repeat((actorIndex + 1) * .61803398875f, 1f);
            float distance = swarmCount <= 1 ? 0f : .34f + actorIndex / (float)Mathf.Max(1, swarmCount - 1) * .72f;
            x = Mathf.Clamp01(homeX + Mathf.Cos(seed * Mathf.PI * 2f) * radiusX * distance);
            y = Mathf.Clamp01(homeY + Mathf.Sin(seed * Mathf.PI * 2f) * radiusY * distance);
            startX = targetX = x;
            startY = targetY = y;
            legStart = -1f;

            AddLayer("Base", "lottie-part-5", Vector2.zero, new Vector2(322f, 147f));
            upperWing = AddLayer("Upper Wing", "lottie-part-4", new Vector2(0f, 81f), new Vector2(155f, 155f));
            lowerWing = AddLayer("Lower Wing", "lottie-part-3", new Vector2(0f, -82f), new Vector2(155f, 155f));
            AddLayer("Body", "lottie-part-2", new Vector2(-108f, -5f), new Vector2(40f, 43f));
            AddLayer("Detail", "lottie-part-1", new Vector2(-108f, -5f), new Vector2(20f, 20f));
        }

        RectTransform AddLayer(string name, string resource, Vector2 nativeOffset, Vector2 nativeSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = nativeOffset * (size / 512f);
            rt.sizeDelta = nativeSize * (size / 512f);
            Image image = go.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>("BOKS/Bee/" + resource);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rt;
        }

        void Update()
        {
            if (upperWing == null || lowerWing == null) return;
            float now = Time.unscaledTime;
            float frame = Mathf.Repeat(now, LoopSeconds) * 60f;
            upperWing.localScale = new Vector3(1f, SampleWing(frame, true) / 100f, 1f);
            lowerWing.localScale = new Vector3(1f, SampleWing(frame, false) / 100f, 1f);

            if (legStart < 0f) PickTarget(now);
            float progress = Mathf.Clamp01((now - legStart) / legDuration);
            float eased = .5f - Mathf.Cos(Mathf.PI * progress) * .5f;
            x = Mathf.Lerp(startX, targetX, eased);
            y = Mathf.Lerp(startY, targetY, eased);
            float bob = Mathf.Sin(now / .260f + bobPhase) * 5f;
            float drift = Mathf.Cos(now / .410f + bobPhase) * 3f;
            float heading = Mathf.Atan2(targetY - startY, targetX - startX) * Mathf.Rad2Deg;
            float tilt = Mathf.Sin(now / .520f + bobPhase) * 6f;
            rect.anchoredPosition = new Vector2(x * boardWidth + drift, -y * boardHeight + bob);
            rect.localRotation = Quaternion.Euler(0f, 0f, -heading + tilt);
            if (progress >= 1f) PickTarget(now);
        }

        void PickTarget(float now)
        {
            // getBeeFlightBounds: 0.45 cell margin, clamped to .05-.11 X and .06-.12 Y.
            float marginX = Mathf.Clamp((75f * .45f) / boardWidth, .05f, .11f);
            float marginY = Mathf.Clamp((75f * .45f) / boardHeight, .06f, .12f);
            startX = x;
            startY = y;
            targetX = Mathf.Lerp(marginX, 1f - marginX, NextRandom());
            targetY = Mathf.Lerp(marginY, 1f - marginY, NextRandom());
            legStart = now;
            legDuration = 1.4f + NextRandom() * 2.2f;
        }

        float NextRandom()
        {
            randomState = randomState * 1664525u + 1013904223u;
            return (randomState & 0x00ffffffu) / 16777216f;
        }

        static float SampleWing(float frame, bool upper)
        {
            float local = Mathf.Repeat(frame, 20f);
            float dip = upper ? (local < 10f ? 55f : 50f) : (local < 10f ? 62f : 68f);
            float progress = local < 5f ? local / 5f : local < 10f ? (10f - local) / 5f
                : local < 15f ? (local - 10f) / 5f : (20f - local) / 5f;
            progress = Mathf.Clamp01(progress);
            progress = progress * progress * (3f - 2f * progress); // smooth source bezier approximation.
            return Mathf.Lerp(100f, dip, progress);
        }
    }
}
