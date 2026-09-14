using UnityEngine;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>Level-1-only counterpart to the web firstLevelOnboarding overlay.</summary>
    public sealed class BOKSLevel1Onboarding : MonoBehaviour
    {
        const float RevealSeconds = 4.2f, DragLoopSeconds = 2.35f, TapLoopSeconds = 1.35f;
        static readonly Vector2 FingertipFromCentre = new Vector2(3f, 20.5f);
        BOKSLevel2Controller controller; RectTransform root; Canvas canvas; Button palette; RectTransform[] slots; Button play;
        RectTransform hand; int stage; float stageAt;

        public void Bind(BOKSLevel2Controller owner, RectTransform parent, Button commandPalette, RectTransform[] slotRoots, Button playButton)
        {
            controller = owner; root = parent; canvas = root.GetComponentInParent<Canvas>(); palette = commandPalette; slots = slotRoots; play = playButton;
            controller.MainCommandPlaced -= OnPlaced; controller.MainCommandPlaced += OnPlaced;
            controller.PlayPressed -= OnPlay; controller.PlayPressed += OnPlay;
            if (hand == null) CreateHand();
        }
        public void Begin(bool levelOne) { stage = levelOne ? 1 : 0; stageAt = Time.unscaledTime; if (hand != null) hand.gameObject.SetActive(false); }
        void OnDestroy() { if (controller != null) { controller.MainCommandPlaced -= OnPlaced; controller.PlayPressed -= OnPlay; } }
        void OnPlaced(int slot)
        {
            // The web promotes directly to Play even if a fast placement happens during its reveal delay.
            if (stage == 1 || stage == 2) { stage = 3; stageAt = Time.unscaledTime; hand.gameObject.SetActive(true); }
        }
        void OnPlay() { if (stage == 3) { stage = 0; hand.gameObject.SetActive(false); } }
        void CreateHand()
        {
            GameObject go = new GameObject("Level 1 Onboarding Hand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hand = go.GetComponent<RectTransform>(); hand.SetParent(root, false); hand.anchorMin = hand.anchorMax = hand.pivot = new Vector2(.5f, .5f); hand.sizeDelta = new Vector2(72, 72);
            // The source PNG is imported as a multiple-sprite sheet. Load its named slice rather
            // than Resources.Load<Sprite>, which returns null for a multi-sprite texture.
            Sprite[] sprites = Resources.LoadAll<Sprite>("BOKS/hand_drag");
            Image image = go.GetComponent<Image>(); image.sprite = sprites != null && sprites.Length > 0 ? sprites[0] : null; image.preserveAspect = true; image.raycastTarget = false;
            hand.gameObject.SetActive(false);
        }
        void Update()
        {
            if (stage == 1 && Time.unscaledTime - stageAt >= RevealSeconds) { stage = 2; hand.gameObject.SetActive(true); }
            if (stage == 1 || hand == null || !hand.gameObject.activeSelf) return;
            if (stage == 2) Drag(); else if (stage == 3) Tap();
        }
        void Drag()
        {
            RectTransform target = null; foreach (RectTransform slot in slots) if (slot.gameObject.activeInHierarchy) { target = slot; break; }
            if (palette == null || target == null) return;
            float t = Mathf.Repeat(Time.unscaledTime - stageAt - RevealSeconds, DragLoopSeconds) / DragLoopSeconds;
            Vector2 a = Center(palette.transform as RectTransform), b = Center(target);
            float p = t < .14f ? 0f : t < .56f ? Ease((t - .14f) / .42f) : 1f;
            hand.anchoredPosition = Vector2.Lerp(a, b, p);
            hand.localRotation = Quaternion.Euler(0, 0, -8f); hand.localScale = Vector3.one * (t < .14f || t > .82f ? .95f : 1f);
        }
        void Tap()
        {
            if (play == null) return; float t = Mathf.Repeat(Time.unscaledTime - stageAt, TapLoopSeconds) / TapLoopSeconds;
            float y = t < .35f ? Mathf.Lerp(0, 4, Ease(t / .35f)) : t < .60f ? Mathf.Lerp(4, 0, Ease((t - .35f) / .25f)) : 0;
            hand.anchoredPosition = Center(play.transform as RectTransform) + new Vector2(0f, y); hand.localRotation = Quaternion.Euler(0, 0, -10f); hand.localScale = Vector3.one;
        }
        Vector2 Center(RectTransform target)
        {
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, target.TransformPoint(target.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, camera, out Vector2 local);
            // The artwork's fingertip is slightly above/right of its centre; align that point to
            // the actual block/slot/button centre without introducing a screen-space shift.
            return local - FingertipFromCentre;
        }
        static float Ease(float t) => BOKSLevel2Controller.CubicBezierYForX(Mathf.Clamp01(t), .42f, 0f, .58f, 1f);
    }
}
