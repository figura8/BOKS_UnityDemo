using UnityEngine;
using UnityEngine.EventSystems;

namespace BOKS.Demo
{
    /// <summary>Reports hover and drop targets; the controller owns all program state.</summary>
    public sealed class BOKSProgramDropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] BOKSLevel2Controller controller;
        [SerializeField] int slotIndex;
        [SerializeField] bool enabledSlot;

        public void Configure(BOKSLevel2Controller owner, int index, bool enabled)
        {
            controller = owner;
            slotIndex = index;
            enabledSlot = enabled;
        }

        public void OnDrop(PointerEventData eventData) => controller.RecordDropTarget(slotIndex, enabledSlot);
        public void OnPointerEnter(PointerEventData eventData) => controller.SetDropHover(slotIndex, enabledSlot, true, eventData.pointerId < 0);
        public void OnPointerExit(PointerEventData eventData) => controller.SetDropHover(slotIndex, enabledSlot, false);
    }
}
