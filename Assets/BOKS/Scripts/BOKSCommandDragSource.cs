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
        Color sourceColor;

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
        void OnDisable() => CancelDrag();
        void OnDestroy() => CancelDrag();

        void CancelDrag()
        {
            if (controller != null && controller.OwnsCommandDrag(this)) controller.CancelCommandDrag();
            else ClearDragVisuals();
        }

        internal void ClearDragVisuals()
        {
            if (dragging && sourceImage != null) sourceImage.color = sourceColor;
            if (ghostLayer != null)
            {
                ghostLayer.SetActive(false);
                Destroy(ghostLayer);
            }
            ghost = null;
            ghostLayer = null;
            dragging = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (controller == null || canvas == null || sourceImage == null ||
                !controller.BeginCommandDrag(this, eventData.pointerId)) return;
            pressPosition = eventData.pressPosition;
            sourceColor = sourceImage.color;
            dragging = true;
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
            if (dragging && controller != null && controller.OwnsCommandDrag(this, eventData.pointerId)) MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging || controller == null || !controller.OwnsCommandDrag(this, eventData.pointerId)) return;
            controller.CompleteCommandDrag(this, eventData.pointerId, Vector2.Distance(pressPosition, eventData.position));
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
