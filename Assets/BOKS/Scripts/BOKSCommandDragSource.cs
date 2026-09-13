using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BOKS.Demo
{
    /// <summary>Drag source shared by the palette prototype and placed commands.</summary>
    public sealed class BOKSCommandDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] BOKSLevel2Controller controller;
        [SerializeField] bool paletteSource;
        [SerializeField] int slotIndex = -1;
        [SerializeField] BOKSCommandType commandType = BOKSCommandType.Forward;

        Image sourceImage;
        GameObject ghostLayer;
        RectTransform ghost;
        Vector2 pressPosition;
        bool dragging;

        public bool IsPaletteSource => paletteSource;
        public int SlotIndex => slotIndex;
        public BOKSCommandType CommandType => commandType;

        public void Configure(BOKSLevel2Controller owner, bool fromPalette, int sourceSlot, BOKSCommandType command = BOKSCommandType.Forward)
        {
            controller = owner;
            paletteSource = fromPalette;
            slotIndex = sourceSlot;
            commandType = command;
        }

        void Awake() => sourceImage = GetComponent<Image>();

        public void OnBeginDrag(PointerEventData eventData)
        {
            pressPosition = eventData.pressPosition;
            dragging = controller.BeginCommandDrag(this);
            if (!dragging) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            ghostLayer = new GameObject("Forward Drag Layer", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            RectTransform layerRect = ghostLayer.GetComponent<RectTransform>();
            layerRect.SetParent(canvas.transform, false);
            layerRect.anchorMin = Vector2.zero;
            layerRect.anchorMax = Vector2.one;
            layerRect.offsetMin = layerRect.offsetMax = Vector2.zero;
            Canvas ghostCanvas = ghostLayer.GetComponent<Canvas>();
            ghostCanvas.overrideSorting = true;
            ghostCanvas.sortingOrder = canvas.sortingOrder + 100;
            CanvasGroup group = ghostLayer.GetComponent<CanvasGroup>();
            group.alpha = .9f;
            group.blocksRaycasts = false;

            GameObject ghostObject = new GameObject("Forward Drag Ghost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ghost = ghostObject.GetComponent<RectTransform>();
            ghost.SetParent(layerRect, false);
            ghost.anchorMin = ghost.anchorMax = ghost.pivot = new Vector2(.5f, .5f);
            ghost.sizeDelta = ((RectTransform)transform).rect.size;
            Image ghostImage = ghostObject.GetComponent<Image>();
            ghostImage.sprite = sourceImage.sprite;
            ghostImage.preserveAspect = true;
            ghostImage.raycastTarget = false;

            if (!paletteSource) sourceImage.color = new Color(1, 1, 1, .55f);
            MoveGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging) MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            controller.CompleteCommandDrag(this, Vector2.Distance(pressPosition, eventData.position));
            sourceImage.color = Color.white;
            if (ghostLayer != null) Destroy(ghostLayer);
            ghost = null;
            ghostLayer = null;
            dragging = false;
        }

        void MoveGhost(PointerEventData eventData)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = (RectTransform)canvas.transform;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, camera, out Vector2 local))
                ghost.anchoredPosition = local;
        }
    }
}
