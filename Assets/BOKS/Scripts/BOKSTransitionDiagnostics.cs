using System;
using UnityEngine;
using Unity.Profiling;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !BOKS_DISABLE_TRANSITION_DIAGNOSTICS
using System.Diagnostics;
using System.Text;
using UnityEngine.UI;
#endif

namespace BOKS.Demo
{
    // Define BOKS_DISABLE_TRANSITION_DIAGNOSTICS to remove standalone collection/UI.
    // Profiler markers remain available independently of the standalone diagnostic.
    public sealed class BOKSTransitionDiagnostics : MonoBehaviour
    {
        public enum Phase { Clone, PreviewApply, PreviewActivate, ProxySetup, LiveApply, Cleanup, GoalPopEnsureAssets, ScrollStep }
        static readonly ProfilerMarker[] Markers = {
            new ProfilerMarker("BOKS.Transition.Clone"), new ProfilerMarker("BOKS.Transition.PreviewApply"),
            new ProfilerMarker("BOKS.Transition.PreviewActivate"), new ProfilerMarker("BOKS.Transition.ProxySetup"),
            new ProfilerMarker("BOKS.Transition.LiveApply"), new ProfilerMarker("BOKS.Transition.Cleanup"),
            new ProfilerMarker("BOKS.GoalPop.EnsureAssets"), new ProfilerMarker("BOKS.Transition.ScrollStep")
        };

        public struct Sample : IDisposable
        {
            readonly Phase phase;
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !BOKS_DISABLE_TRANSITION_DIAGNOSTICS
            readonly long start;
#endif
            public Sample(Phase phase)
            {
                this.phase = phase;
                Markers[(int)phase].Begin();
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !BOKS_DISABLE_TRANSITION_DIAGNOSTICS
                start = Stopwatch.GetTimestamp();
#endif
            }
            public void Dispose()
            {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !BOKS_DISABLE_TRANSITION_DIAGNOSTICS
                double ms = (Stopwatch.GetTimestamp() - start) * (1000.0 / Stopwatch.Frequency);
                if (phase == Phase.GoalPopEnsureAssets) pendingGoalPop = ms;
                else if (instance != null && instance.collecting)
                {
                    instance.times[(int)phase] += ms;
                    if (phase == Phase.ScrollStep)
                    {
                        instance.scrollFrames++;
                        instance.scrollMax = Math.Max(instance.scrollMax, ms);
                    }
                }
#endif
                Markers[(int)phase].End();
            }
        }

        public static Sample Measure(Phase phase) => new Sample(phase);

        public static void Prepare()
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !BOKS_DISABLE_TRANSITION_DIAGNOSTICS
            if (instance == null)
            {
                var go = new GameObject("BOKS Temporary Transition Diagnostics");
                instance = go.AddComponent<BOKSTransitionDiagnostics>();
                instance.CreatePanel();
            }
#endif
        }
        public static void Begin(int from, int to)
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !BOKS_DISABLE_TRANSITION_DIAGNOSTICS
            Prepare();
            instance.panelCanvas.enabled = false;
            Array.Clear(instance.times, 0, instance.times.Length);
            instance.times[(int)Phase.GoalPopEnsureAssets] = pendingGoalPop;
            pendingGoalPop = 0;
            instance.from = from; instance.to = to;
            instance.startFrame = Time.frameCount;
            instance.finishFrame = -1;
            instance.scrollFrames = instance.frameCount = 0;
            instance.scrollMax = instance.frameSum = instance.frameMax = 0;
            instance.collecting = true;
#endif
        }
        public static void Finish()
        {
#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !BOKS_DISABLE_TRANSITION_DIAGNOSTICS
            if (instance != null && instance.collecting) instance.finishFrame = Time.frameCount;
#endif
        }

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !BOKS_DISABLE_TRANSITION_DIAGNOSTICS
        static BOKSTransitionDiagnostics instance;
        static double pendingGoalPop;
        readonly double[] times = new double[8];
        readonly StringBuilder report = new StringBuilder(640);
        Canvas panelCanvas;
        RectTransform panel;
        Text label;
        bool collecting;
        int from, to, startFrame, finishFrame, lastFrame = -1, scrollFrames, frameCount;
        double scrollMax, frameSum, frameMax;
        float hideAt;

        void CreatePanel()
        {
            var canvasObject = new GameObject("Diagnostic Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            panelCanvas = canvasObject.GetComponent<Canvas>();
            panelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            panelCanvas.sortingOrder = 10000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(520, 1000);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var panelObject = new GameObject("Report", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
            panel.sizeDelta = new Vector2(350, 365);
            var background = panelObject.GetComponent<Image>();
            background.color = new Color(.04f, .05f, .07f, .94f);
            background.raycastTarget = false;
            var textObject = new GameObject("Measurements", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12, 10); rect.offsetMax = new Vector2(-12, -10);
            label = textObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20; label.color = Color.white;
            label.raycastTarget = false;
            label.supportRichText = false;
            panelCanvas.enabled = false;
        }

        void LateUpdate()
        {
            if (collecting && Time.frameCount > startFrame && lastFrame != Time.frameCount)
            {
                lastFrame = Time.frameCount;
                double ms = Time.unscaledDeltaTime * 1000.0;
                frameSum += ms; frameMax = Math.Max(frameMax, ms); frameCount++;
                // One extra frame observes the frame containing deferred Destroy/render work.
                if (finishFrame >= 0 && Time.frameCount > finishFrame)
                {
                    collecting = false;
                    ShowReport();
                }
            }
            if (panelCanvas.enabled && Time.unscaledTime >= hideAt) panelCanvas.enabled = false;
        }

        void ShowReport()
        {
            report.Clear();
            report.Append("Transition ").Append(from).Append(" -> ").Append(to).Append('\n');
            Line("Clone", times[0]); Line("PreviewApply", times[1]); Line("PreviewActivate", times[2]);
            Line("ProxySetup", times[3]);
            Line("Scroll avg", scrollFrames > 0 ? times[7] / scrollFrames : 0);
            Line("Scroll max", scrollMax);
            report.Append("Scroll frames: ").Append(scrollFrames).Append('\n');
            Line("LiveApply", times[4]); Line("Cleanup*", times[5]);
            Line("GoalPop.EnsureAssets", times[6]);
            Line("Frame avg", frameCount > 0 ? frameSum / frameCount : 0);
            Line("Frame max", frameMax);
            report.Append("*Destroy scheduling only");
            label.text = report.ToString();
            Rect safe = Screen.safeArea;
            float scale = Mathf.Max(.0001f, panelCanvas.scaleFactor);
            panel.anchoredPosition = new Vector2(safe.xMin / scale + 10, -(Screen.height - safe.yMax) / scale - 10);
            hideAt = Time.unscaledTime + 5f;
            panelCanvas.enabled = true;
        }
        void Line(string name, double value) => report.Append(name).Append(": ").Append(value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(" ms\n");
        void OnDestroy() { if (instance == this) { instance = null; pendingGoalPop = 0; } }
#endif
    }
}
