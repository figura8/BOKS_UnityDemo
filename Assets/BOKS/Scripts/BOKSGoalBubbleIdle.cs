using UnityEngine;

namespace BOKS.Demo
{
    /// <summary>
    /// Source-faithful goal-bubble idle (MicroAnimationSpec.md section 1).
    ///
    /// The web build redraws #goalIdleCanvas every frame from pure sine drivers
    /// (js/core/game.js drawGoalIdleCanvas):
    ///   driftY  = sin(t * 1.4) * 4.5 px      driftX = cos(t * 0.9) * 1.8 px
    ///   shellRx = 28 + sin(t * 1.2) * 1.8    shellRy = 31 + cos(t * 1.05) * 2.6
    ///   wobble  = sin(t * 2.1) * 0.08 rad    glossShift = sin(t * 1.7) * 3.2 px
    /// Canvas pixels map 1:1 onto the board: the goal root is 108x108, "Bubble Shell" is 56x62
    /// (2 * 28 by 2 * 31) and "Pale Glow Outer" is 108 (radius 54), exactly like the canvas.
    ///
    /// The canvas glow is static, so the glow pulse uses the CSS fallback idle instead
    /// (styles/app.css @keyframes goalBubbleGlowPulse: 3.1 s ease-in-out infinite,
    /// scale 0.98 -&gt; 1.04 and opacity 0.76 -&gt; 0.98). Drift, shell pulse and wobble are canvas-only.
    /// The idle pauses while the pop VFX has faded the bubble out (goal.idleHiddenAfterPopMs).
    /// </summary>
    public sealed class BOKSGoalBubbleIdle : MonoBehaviour
    {
        // js/core/game.js drawGoalIdleCanvas
        const float DriftXAmplitude = 1.8f;
        const float DriftXFrequency = 0.9f;
        const float DriftYAmplitude = 4.5f;
        const float DriftYFrequency = 1.4f;
        const float ShellRadiusX = 28f;
        const float ShellRadiusY = 31f;
        const float ShellPulseXAmplitude = 1.8f;
        const float ShellPulseXFrequency = 1.2f;
        const float ShellPulseYAmplitude = 2.6f;
        const float ShellPulseYFrequency = 1.05f;
        const float WobbleAmplitude = 0.08f;
        const float WobbleFrequency = 2.1f;
        const float GlossShiftAmplitude = 3.2f;
        const float GlossShiftFrequency = 1.7f;

        // styles/app.css @keyframes goalBubbleGlowPulse (animation: goalBubbleGlowPulse 3.1s ease-in-out infinite)
        const float GlowPulsePeriod = 3.1f;
        const float GlowScaleMin = 0.98f;
        const float GlowScaleMax = 1.04f;
        const float GlowAlphaMin = 0.76f;
        const float GlowAlphaMax = 0.98f;
        const float EaseInOutX1 = .42f;
        const float EaseInOutX2 = .58f;

        static readonly string[] GlowNames = { "Pale Glow Outer", "Pale Glow Middle", "Warm Glow Core" };

        RectTransform goal;
        RectTransform shell;
        RectTransform gloss;
        RectTransform[] glows = new RectTransform[0];
        Color[] glowColors = new Color[0];
        CanvasGroup fade;
        Vector2 goalRest;
        Vector2 shellRest;
        Vector2 glossRest;
        Vector2 goalCentre;
        Vector2 shellCentre;
        Vector2[] glowCentres = new Vector2[0];
        Vector2[] glowRests = new Vector2[0];
        float[] glowAppliedAlpha = new float[0];
        bool captured;
        bool atRest;

        /// <summary>True once the animated rest pose has been cached.</summary>
        public bool RestPoseCaptured => captured;

        void Awake() => CaptureRestPose();

        /// <summary>
        /// Caches the layout the idle animates around. Must be called again whenever the goal root
        /// is repositioned (campaign level change), otherwise the drift would build on a stale pose.
        /// </summary>
        public void CaptureRestPose()
        {
            goal = (RectTransform)transform;
            goalRest = goal.anchoredPosition;
            goalCentre = PivotCentre(goal);

            shell = Find("Bubble Shell");
            if (shell != null)
            {
                shellRest = shell.anchoredPosition;
                shellCentre = PivotCentre(shell);
            }

            gloss = Find("Upper Gloss");
            if (gloss != null) glossRest = gloss.anchoredPosition;

            glows = new RectTransform[GlowNames.Length];
            glowColors = new Color[GlowNames.Length];
            glowCentres = new Vector2[GlowNames.Length];
            glowRests = new Vector2[GlowNames.Length];
            glowAppliedAlpha = new float[GlowNames.Length];
            for (int i = 0; i < GlowNames.Length; i++)
            {
                glows[i] = Find(GlowNames[i]);
                if (glows[i] == null) continue;
                glowCentres[i] = PivotCentre(glows[i]);
                glowRests[i] = glows[i].anchoredPosition;
                BOKSShapeGraphic shape = glows[i].GetComponent<BOKSShapeGraphic>();
                glowColors[i] = shape != null ? shape.topColor : Color.clear;
                glowAppliedAlpha[i] = glowColors[i].a;
            }

            fade = goal.GetComponent<CanvasGroup>();
            captured = true;
        }

        void Update()
        {
            if (!captured) CaptureRestPose();

            // The pop VFX fades the bubble out through the root CanvasGroup; hold the rest pose while hidden.
            if (fade != null && fade.alpha <= .01f)
            {
                Rest();
                return;
            }
            atRest = false;

            float t = Time.unscaledTime;

            float driftX = Mathf.Cos(t * DriftXFrequency) * DriftXAmplitude;
            float driftY = Mathf.Sin(t * DriftYFrequency) * DriftYAmplitude;
            float shellRx = ShellRadiusX + Mathf.Sin(t * ShellPulseXFrequency) * ShellPulseXAmplitude;
            float shellRy = ShellRadiusY + Mathf.Cos(t * ShellPulseYFrequency) * ShellPulseYAmplitude;
            float wobble = Mathf.Sin(t * WobbleFrequency) * WobbleAmplitude;   // canvas radians
            float glossShift = Mathf.Sin(t * GlossShiftFrequency) * GlossShiftAmplitude;

            // The canvas rotates clockwise for positive radians; Unity Z rotation is counter-clockwise.
            Quaternion rotation = Quaternion.Euler(0f, 0f, -wobble * Mathf.Rad2Deg);
            goal.localRotation = rotation;
            goal.anchoredPosition = goalRest + new Vector2(driftX, -driftY) + AboutCentre(goalCentre, rotation, Vector2.one);

            if (shell != null)
            {
                Vector2 pulse = new Vector2(shellRx / ShellRadiusX, shellRy / ShellRadiusY);
                shell.localScale = new Vector3(pulse.x, pulse.y, 1f);
                shell.anchoredPosition = shellRest + AboutCentre(shellCentre, Quaternion.identity, pulse);
            }

            if (gloss != null)
                gloss.anchoredPosition = glossRest + Vector2.right * glossShift;

            float glowPhase = Mathf.Repeat(t, GlowPulsePeriod) / GlowPulsePeriod;
            float glowHalf = glowPhase <= .5f
                ? Mathf.InverseLerp(0f, .5f, glowPhase)
                : Mathf.InverseLerp(1f, .5f, glowPhase);
            float glowEased = BOKSLevel2Controller.CubicBezierYForX(glowHalf, EaseInOutX1, 0f, EaseInOutX2, 1f);
            float glowScale = Mathf.Lerp(GlowScaleMin, GlowScaleMax, glowEased);
            float glowAlpha = Mathf.Lerp(GlowAlphaMin, GlowAlphaMax, glowEased);
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] == null) continue;
                glows[i].localScale = new Vector3(glowScale, glowScale, 1f);
                glows[i].anchoredPosition = glowRests[i] + AboutCentre(glowCentres[i], Quaternion.identity, Vector2.one * glowScale);
                ApplyGlowAlpha(i, glowAlpha);
            }
        }

        /// <summary>Returns the anchoredPosition correction that keeps the given local point fixed.</summary>
        static Vector2 AboutCentre(Vector2 localPoint, Quaternion rotation, Vector2 scale)
        {
            Vector2 transformed = rotation * new Vector2(localPoint.x * scale.x, localPoint.y * scale.y);
            return localPoint - transformed;
        }

        /// <summary>Rect centre expressed in the rect's own local space (pivot at the origin).</summary>
        static Vector2 PivotCentre(RectTransform rect) =>
            new Vector2((.5f - rect.pivot.x) * rect.rect.width, (.5f - rect.pivot.y) * rect.rect.height);

        void ApplyGlowAlpha(int index, float multiplier)
        {
            float alpha = glowColors[index].a * multiplier;
            if (Mathf.Abs(glowAppliedAlpha[index] - alpha) < .002f) return;
            glowAppliedAlpha[index] = alpha;
            BOKSShapeGraphic shape = glows[index] != null ? glows[index].GetComponent<BOKSShapeGraphic>() : null;
            if (shape == null) return;
            Color color = glowColors[index];
            color.a = alpha;
            shape.topColor = color;
            shape.bottomColor = color;
            shape.SetVerticesDirty();
        }

        void Rest()
        {
            if (atRest) return;
            atRest = true;
            goal.localRotation = Quaternion.identity;
            goal.anchoredPosition = goalRest;
            if (shell != null)
            {
                shell.localScale = Vector3.one;
                shell.anchoredPosition = shellRest;
            }
            if (gloss != null) gloss.anchoredPosition = glossRest;
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] != null)
                {
                    glows[i].localScale = Vector3.one;
                    glows[i].anchoredPosition = glowRests[i];
                }
                ApplyGlowAlpha(i, 1f);
            }
        }

        RectTransform Find(string childName) => transform.Find(childName) as RectTransform;
    }
}
