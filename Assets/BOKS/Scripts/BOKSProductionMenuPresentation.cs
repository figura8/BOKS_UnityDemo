using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>Production-only logo and original goal UI. No experimental renderer or pop.</summary>
    public sealed class BOKSProductionMenuPresentation : MonoBehaviour
    {
        Canvas canvas;
        RectTransform composition;
        Vector2 lastSize;
        Rect lastSafe;

        public void Build(GameObject goalPrefab, Sprite logo, out CanvasGroup group, out Button button)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(520, 1000);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();

            var background = NewRect("White Background", transform, Vector2.zero, Vector2.zero);
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            var white = background.gameObject.AddComponent<Image>();
            white.color = Color.white; white.raycastTarget = false;

            composition = NewRect("Logo and Original Goal", transform, Vector2.zero, new Vector2(400, 420));
            var title = NewRect("BÖKS Logo", composition, new Vector2(0, 110), new Vector2(320, 147));
            var titleImage = title.gameObject.AddComponent<Image>();
            titleImage.sprite = logo; titleImage.preserveAspect = true; titleImage.raycastTarget = false;

            // Scale a wrapper: keep every authored goal coordinate and pivot unchanged.
            var holder = NewRect("Goal Bubble Holder", composition, new Vector2(0, -95), new Vector2(108, 108));
            holder.localScale = Vector3.one * 2.6f;
            var goal = Instantiate(goalPrefab, holder, false).GetComponent<RectTransform>();
            goal.name = "Original Flower Goal";
            goal.anchorMin = goal.anchorMax = new Vector2(.5f, .5f);
            goal.anchoredPosition = new Vector2(-54, 54);
            goal.localScale = Vector3.one;
            goal.localRotation = Quaternion.identity;
            goal.gameObject.AddComponent<BOKSGoalBubbleIdle>().CaptureRestPose();
            foreach (Graphic graphic in goal.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var shell = goal.Find("Bubble Shell/Bubble Fill").GetComponent<BOKSShapeGraphic>();
            shell.raycastTarget = true;
            button = shell.gameObject.AddComponent<Button>();
            button.targetGraphic = shell;
            button.transition = Selectable.Transition.None;
            RefreshLayout();
        }

        void LateUpdate()
        {
            if (lastSize != new Vector2(Screen.width, Screen.height) || lastSafe != Screen.safeArea)
                RefreshLayout();
        }

        void RefreshLayout()
        {
            if (composition == null || canvas == null) return;
            // Calculate the same scaler factor without depending on Canvas update order.
            float scale = Mathf.Sqrt((Screen.width / 520f) * (Screen.height / 1000f));
            scale = Mathf.Max(.0001f, scale);
            Rect safe = Screen.safeArea;
            composition.anchoredPosition = (safe.center - new Vector2(Screen.width, Screen.height) * .5f) / scale;
            float fit = Mathf.Min(1f, Mathf.Min(safe.width / scale / 430f, safe.height / scale / 450f));
            composition.localScale = Vector3.one * fit;
            lastSize = new Vector2(Screen.width, Screen.height);
            lastSafe = safe;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
    }
}
