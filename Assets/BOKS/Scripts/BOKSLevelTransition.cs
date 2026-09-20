using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>
    /// Source-faithful BOKS level transition. Reproduces the web SVG overlay: an opaque
    /// <c>--level-transition-fill</c> (#ddd1bb) fill with a feathered square "cloud" hole
    /// (440px base) that shrinks on close and grows on open, with the source cubic-bezier
    /// easing and close/hold/open timings.
    /// </summary>
    public sealed class BOKSLevelTransition : MonoBehaviour
    {
        // Source timings (runtime-rules.json / game.js triggerLevelExit).
        public const float CloseSeconds = 1.036f;   // total * 0.56 (total = 1850ms)
        public const float HoldSeconds = 0.333f;    // total * 0.18
        public const float OpenSeconds = 0.481f;    // remainder
        public const float FadeSeconds = 0.120f;    // overlay opacity fade-in/out

        // Source fill colour (#ddd1bb = --level-transition-fill).
        static readonly Color FillColor = new Color32(221, 209, 187, 255);

        // Source hole geometry (LEVEL_CLOUD_BASE_W/H) and feather (feGaussianBlur stdDeviation).
        const float CloudBase = 440f;
        const float FeatherPx = 2.5f;
        const float MinScale = 0.0001f;

        CanvasGroup blocker;
        Image fill;
        Material fillMaterial;
        float fullScale = 1.05f;

        public float TimingScale { get; set; } = 1f;
        public bool IsRunning { get; private set; }

        void Awake()
        {
            GameObject canvasGo = new GameObject("Level Transition Overlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            // Root the overlay at scene level so the full-screen rect is never affected by the
            // portrait's ScreenSpaceCamera canvas / scaler, guaranteeing 100% coverage.
            canvasGo.transform.SetParent(null, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9900;
            canvas.overrideSorting = true;

            blocker = canvasGo.GetComponent<CanvasGroup>();
            blocker.blocksRaycasts = false;
            blocker.alpha = 0f;

            GameObject fillGo = new GameObject("Transition Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(canvasGo.transform, false);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill = fillGo.GetComponent<Image>();
            fill.raycastTarget = true;

            fill.color = FillColor;

            Shader shader = Shader.Find("BOKS/LevelTransition");
            if (shader != null)
            {
                fillMaterial = new Material(shader);
                fill.material = fillMaterial;
            }

            TransitionLog($"shader = {(shader != null ? shader.name : "<missing>")}, supported = {(shader != null && shader.isSupported)}, graphics API = {SystemInfo.graphicsDeviceType}");

            fillGo.SetActive(false);
            RefreshScreenMetrics();
        }

        void OnDestroy()
        {
            // The overlay canvas is rooted at scene level; clean it and the runtime material up.
            if (fillMaterial != null) { Destroy(fillMaterial); fillMaterial = null; }
            if (blocker != null) { Destroy(blocker.gameObject); blocker = null; }
        }


        void RefreshScreenMetrics()
        {
            float w = Mathf.Max(1, Screen.width);
            float h = Mathf.Max(1, Screen.height);
            fullScale = Mathf.Max((w + 220f) / CloudBase, (h + 260f) / CloudBase, 1.05f);

            if (fillMaterial != null)
            {
                fillMaterial.SetVector("_ScreenSize", new Vector4(w, h, 0f, 0f));
                fillMaterial.SetVector("_HoleCenter", new Vector4(w * 0.5f, h * 0.5f, 0f, 0f));
            }
        }

        public void Play(Action covered, Action finished = null)
        {
            if (!IsRunning) StartCoroutine(Run(covered, finished));
        }

        IEnumerator Run(Action covered, Action finished)
        {
            IsRunning = true;
            RefreshScreenMetrics();
            TransitionLog("close start");

            // The first alpha-faded frame must already contain the full-screen hole. Previously
            // this state was assigned only after the fade, exposing the solid fill on first use.
            SetHoleEnabled(true);
            SetHole(fullScale, 0f);
            fill.gameObject.SetActive(true);
            blocker.blocksRaycasts = true;

            // Overlay fades in quickly (matches #levelTransition opacity transition).
            yield return Animate(FadeSeconds, p => blocker.alpha = p);
            blocker.alpha = 1f;

            // Close: hole shrinks from fullScale to MinScale with the source rotation keyframes.
            yield return Animate(CloseSeconds, p =>
            {
                float t = Ease(p);
                SetHole(CloseScale(t) * fullScale, CloseRotation(t));
            });

            // Fully covered: disable the hole so the solid fill covers 100% of the screen.
            SetHoleEnabled(false);
            TransitionLog("closed");

            // Swap/initialise the next level while the screen is fully covered.
            covered?.Invoke();

            // Hold while covered, then open.
            yield return Wait(HoldSeconds);

            SetHoleEnabled(true);
            SetHole(MinScale, 0f);
            TransitionLog("open start");
            yield return Animate(OpenSeconds, p =>
            {
                float t = Ease(p);
                SetHole(OpenScale(t) * fullScale, OpenRotation(t));
            });
            SetHole(fullScale, 0f);

            // Overlay fades out.
            yield return Animate(FadeSeconds, p => blocker.alpha = 1f - p);

            blocker.alpha = 0f;
            blocker.blocksRaycasts = false;
            fill.gameObject.SetActive(false);
            IsRunning = false;
            TransitionLog("complete");
            finished?.Invoke();
        }
        void SetHole(float scale, float rotationDegrees)
        {
            if (fillMaterial != null)
            {
                fillMaterial.SetFloat("_HoleHalf", CloudBase * 0.5f * scale);
                fillMaterial.SetFloat("_Rotation", rotationDegrees);
                fillMaterial.SetFloat("_Feather", FeatherPx);
                if (scale <= MinScale || scale >= fullScale)
                    TransitionLog($"radius = {CloudBase * 0.5f * scale:F2}");
            }
            else
            {
                // Shader-less fallback: keep the full opaque fill (still fully covers on close).
                fill.color = FillColor;
            }
        }

        void SetHoleEnabled(bool enabled)
        {
            if (fillMaterial != null)
                fillMaterial.SetFloat("_HoleEnabled", enabled ? 1f : 0f);
        }

        // Source keyframes (game.js animateTransitionHole), offsets in EASED time t. Scales are
        // relative to fullScale; rotations are degrees.
        static float CloseScale(float t) =>
            t <= 0.16f ? Mathf.Lerp(1f, 1.05f, t / 0.16f)
            : t <= 0.82f ? Mathf.Lerp(1.05f, 0.18f, (t - 0.16f) / 0.66f)
            : Mathf.Lerp(0.18f, 0f, (t - 0.82f) / 0.18f);

        static float CloseRotation(float t) =>
            t <= 0.16f ? Mathf.Lerp(0f, -3f, t / 0.16f)
            : t <= 0.82f ? Mathf.Lerp(-3f, 2f, (t - 0.16f) / 0.66f)
            : Mathf.Lerp(2f, 0f, (t - 0.82f) / 0.18f);

        static float OpenScale(float t) =>
            t <= 0.72f ? Mathf.Lerp(0f, 1.04f, t / 0.72f)
            : Mathf.Lerp(1.04f, 1f, (t - 0.72f) / 0.28f);

        static float OpenRotation(float t) =>
            t <= 0.72f ? Mathf.Lerp(0f, 2f, t / 0.72f)
            : Mathf.Lerp(2f, 0f, (t - 0.72f) / 0.28f);

        // cubic-bezier(0.4, 0, 0.2, 1) - matches makeCubicBezier in game.js.
        static float Ease(float x)
        {
            const float x1 = 0.4f, y1 = 0f, x2 = 0.2f, y2 = 1f;
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float cx = 3f * x1;
            float bx = 3f * (x2 - x1) - cx;
            float ax = 1f - cx - bx;
            float cy = 3f * y1;
            float by = 3f * (y2 - y1) - cy;
            float ay = 1f - cy - by;

            float SampleX(float t) => ((ax * t + bx) * t + cx) * t;
            float SampleY(float t) => ((ay * t + by) * t + cy) * t;
            float SampleDX(float t) => (3f * ax * t + 2f * bx) * t + cx;

            float t = x;
            for (int i = 0; i < 6; i++)
            {
                float currentX = SampleX(t) - x;
                float d = SampleDX(t);
                if (Mathf.Abs(currentX) < 1e-5f || Mathf.Abs(d) < 1e-5f) break;
                t -= currentX / d;
            }

            if (t < 0f || t > 1f || float.IsNaN(t))
            {
                float low = 0f, high = 1f;
                t = x;
                for (int i = 0; i < 12; i++)
                {
                    float currentX = SampleX(t);
                    if (Mathf.Abs(currentX - x) < 1e-5f) break;
                    if (currentX < x) low = t; else high = t;
                    t = (low + high) * 0.5f;
                }
            }
            return SampleY(t);
        }

        IEnumerator Animate(float seconds, Action<float> frame)
        {
            float duration = seconds * Mathf.Max(0f, TimingScale);
            if (duration <= 0f) { frame(1f); yield break; }
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                frame(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            frame(1f);
        }

        IEnumerator Wait(float seconds)
        {
            float duration = seconds * Mathf.Max(0f, TimingScale);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime) yield return null;
        }

        void TransitionLog(string message)
        {
            if (Debug.isDebugBuild) Debug.Log($"[BOKS TRANSITION] {message}");
        }
    }
}
