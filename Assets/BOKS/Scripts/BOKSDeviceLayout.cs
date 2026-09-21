using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    public enum BOKSDeviceLayoutMode
    {
        PhonePortrait,
        TabletLandscape
    }

    /// <summary>One device-based decision point for mobile orientation and presentation.</summary>
    public static class BOKSDeviceLayout
    {
        const float MinimumTabletDiagonalInches = 7f;
        const float MinimumTabletSmallestWidthDp = 600f;
        const int MinimumTabletSmallestPixelsFallback = 1200;
        const int MinimumTabletLargestPixelsFallback = 1920;

        public static BOKSDeviceLayoutMode CurrentMode { get; private set; } = BOKSDeviceLayoutMode.PhonePortrait;
        public static float EstimatedDiagonalInches { get; private set; } = -1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public enum TestOverride
        {
            Auto,
            ForcePhonePortrait,
            ForceTabletLandscape
        }

        public static TestOverride LayoutTestOverride { get; private set; } = TestOverride.Auto;

        public static void SetLayoutTestOverride(TestOverride value)
        {
            LayoutTestOverride = value;
            BOKSCampaignResponsiveLayout.RefreshAll();
        }
#endif

        public static BOKSDeviceLayoutMode DetectAndApply()
        {
            CurrentMode = Classify(Application.platform, SystemInfo.deviceModel, Screen.width, Screen.height, Screen.dpi,
                out float diagonalInches, out float smallestWidthDp);
            EstimatedDiagonalInches = diagonalInches;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (LayoutTestOverride == TestOverride.ForcePhonePortrait)
            {
                CurrentMode = BOKSDeviceLayoutMode.PhonePortrait;
                Debug.Log("[BOKS DEVICE] Forced PhonePortrait");
            }
            else if (LayoutTestOverride == TestOverride.ForceTabletLandscape)
            {
                CurrentMode = BOKSDeviceLayoutMode.TabletLandscape;
                Debug.Log("[BOKS DEVICE] Forced TabletLandscape");
            }
#endif

            Screen.autorotateToPortrait = CurrentMode == BOKSDeviceLayoutMode.PhonePortrait;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = CurrentMode == BOKSDeviceLayoutMode.TabletLandscape;
            Screen.autorotateToLandscapeRight = CurrentMode == BOKSDeviceLayoutMode.TabletLandscape;
            Screen.orientation = CurrentMode == BOKSDeviceLayoutMode.PhonePortrait
                ? ScreenOrientation.Portrait
                : ScreenOrientation.AutoRotation;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            string diagonal = diagonalInches > 0f ? diagonalInches.ToString("0.00") + "in" : "unavailable";
            string smallestDp = smallestWidthDp > 0f ? smallestWidthDp.ToString("0") + "dp" : "unavailable";
            Debug.Log($"[BOKS DEVICE] {CurrentMode} resolution={Screen.width}x{Screen.height} dpi={Screen.dpi:0.##} diagonal={diagonal} smallestWidth={smallestDp} model={SystemInfo.deviceModel}");
#endif
            return CurrentMode;
        }

        public static BOKSDeviceLayoutMode Classify(RuntimePlatform platform, string model, int width, int height, float dpi,
            out float diagonalInches, out float smallestWidthDp)
        {
            diagonalInches = -1f;
            smallestWidthDp = -1f;
            string deviceModel = model ?? string.Empty;

            if (platform == RuntimePlatform.IPhonePlayer)
            {
                // Unity exposes the hardware family in deviceModel (for example "iPad14,5").
                return deviceModel.StartsWith("iPad", StringComparison.OrdinalIgnoreCase)
                    ? BOKSDeviceLayoutMode.TabletLandscape
                    : BOKSDeviceLayoutMode.PhonePortrait;
            }

            if (platform != RuntimePlatform.Android)
                return BOKSDeviceLayoutMode.PhonePortrait;

            int smallestPixels = Mathf.Min(width, height);
            int largestPixels = Mathf.Max(width, height);
            bool reliableDpi = dpi >= 80f && dpi <= 800f;
            if (reliableDpi)
            {
                diagonalInches = Mathf.Sqrt(width * width + height * height) / dpi;
                smallestWidthDp = smallestPixels * 160f / dpi;
            }

            bool tablet = diagonalInches >= MinimumTabletDiagonalInches ||
                smallestWidthDp >= MinimumTabletSmallestWidthDp ||
                (!reliableDpi && smallestPixels >= MinimumTabletSmallestPixelsFallback && largestPixels >= MinimumTabletLargestPixelsFallback);
            return tablet ? BOKSDeviceLayoutMode.TabletLandscape : BOKSDeviceLayoutMode.PhonePortrait;
        }
    }

    /// <summary>Reuses the single campaign hierarchy and only changes its presentation frame.</summary>
    public sealed class BOKSCampaignResponsiveLayout : MonoBehaviour
    {
        static readonly Vector2 PortraitReference = new Vector2(520f, 1000f);
        static readonly Vector2 TabletReference = new Vector2(1280f, 800f);

        readonly Dictionary<RectTransform, LayoutSnapshot> portrait = new Dictionary<RectTransform, LayoutSnapshot>();
        RectTransform presentation;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform header;
        RectTransform board;
        RectTransform palette;
        RectTransform program;
        RectTransform run;
        Rect lastSafeArea;
        Vector2 lastScreenSize;

        struct LayoutSnapshot
        {
            public Vector2 position;
            public Vector2 size;
            public Vector3 scale;
        }

        public static void Ensure(RectTransform campaignPresentation)
        {
            if (campaignPresentation == null || campaignPresentation.GetComponent<BOKSCampaignResponsiveLayout>() != null) return;
            campaignPresentation.gameObject.AddComponent<BOKSCampaignResponsiveLayout>();
        }

        public static void RefreshAll()
        {
            foreach (BOKSCampaignResponsiveLayout layout in UnityEngine.Object.FindObjectsByType<BOKSCampaignResponsiveLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                layout.ApplyLayout(true);
        }

        void Awake()
        {
            presentation = transform as RectTransform;
            canvas = GetComponentInParent<Canvas>();
            scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            header = FindRect("BOKS Gameplay Header");
            board = FindRect("BOKS Board - 6 x 6");
            palette = FindRect("BOKS Command Palette");
            program = FindRect("BOKS Program Area");
            run = FindRect("BOKS Run Button");
            CapturePortrait(header);
            CapturePortrait(board);
            CapturePortrait(palette);
            CapturePortrait(program);
            CapturePortrait(run);
            ApplyLayout(true);
        }

        void OnEnable() => ApplyLayout(true);

        void Start() => ApplyLayout(true);

        void Update()
        {
            if (lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height || lastSafeArea != Screen.safeArea)
                ApplyLayout(false);
        }

        void ApplyLayout(bool refreshDeviceMode)
        {
            if (presentation == null) return;
            if (refreshDeviceMode) BOKSDeviceLayout.DetectAndApply();
            bool tablet = BOKSDeviceLayout.CurrentMode == BOKSDeviceLayoutMode.TabletLandscape;
            if (scaler != null) scaler.referenceResolution = tablet ? TabletReference : PortraitReference;

            RestorePortraitTargets();
            if (tablet) ApplyTabletLayout();
            FitPresentationToSafeArea(tablet ? TabletReference : PortraitReference);
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2(Screen.width, Screen.height);
        }

        void ApplyTabletLayout()
        {
            // Web tablet-layout: two 460px panels, 48px separation, centered below a compact header.
            const float panelWidth = 460f;
            const float gap = 48f;
            float left = (TabletReference.x - (panelWidth * 2f + gap)) * .5f;
            float controlsLeft = left + panelWidth + gap;
            Set(header, (TabletReference.x - header.rect.width) * .5f, 18f);
            Set(board, left, 150f);
            Set(palette, controlsLeft, 150f);
            Set(program, controlsLeft, 246f); // 72px palette + 24px web gap.
            Set(run, controlsLeft + (panelWidth - run.rect.width) * .5f, 488f);
        }

        void FitPresentationToSafeArea(Vector2 reference)
        {
            if (canvas == null || presentation == null) return;
            float scale = Mathf.Max(.0001f, canvas.scaleFactor);
            Rect safe = Screen.safeArea;
            Vector2 safeSize = new Vector2(safe.width / scale, safe.height / scale);
            Vector2 safeCenter = new Vector2(
                (safe.center.x - Screen.width * .5f) / scale,
                (safe.center.y - Screen.height * .5f) / scale);
            float fit = Mathf.Min(1f, safeSize.x / reference.x, safeSize.y / reference.y);
            presentation.anchorMin = presentation.anchorMax = presentation.pivot = new Vector2(.5f, .5f);
            presentation.sizeDelta = reference;
            presentation.anchoredPosition = safeCenter;
            presentation.localScale = Vector3.one * fit;
        }

        void CapturePortrait(RectTransform target)
        {
            if (target == null || portrait.ContainsKey(target)) return;
            portrait[target] = new LayoutSnapshot { position = target.anchoredPosition, size = target.sizeDelta, scale = target.localScale };
        }

        void RestorePortraitTargets()
        {
            foreach (KeyValuePair<RectTransform, LayoutSnapshot> pair in portrait)
            {
                if (pair.Key == null) continue;
                pair.Key.anchoredPosition = pair.Value.position;
                pair.Key.sizeDelta = pair.Value.size;
                pair.Key.localScale = pair.Value.scale;
            }
        }

        static void Set(RectTransform target, float x, float y)
        {
            if (target != null) target.anchoredPosition = new Vector2(x, -y);
        }

        RectTransform FindRect(string objectName)
        {
            foreach (RectTransform rect in GetComponentsInChildren<RectTransform>(true))
                if (rect.name == objectName) return rect;
            return null;
        }
    }
}
