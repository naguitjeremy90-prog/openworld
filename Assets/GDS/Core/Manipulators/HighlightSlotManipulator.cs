using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GDS.Core {

    public class HighlightSlotManipulator : PointerManipulator {
        Store store;
        SlotView oldSlot;
        SlotView newSlot;

        public int PollingInterval = 50;
        IVisualElementScheduledItem scheduleId;

        public HighlightSlotManipulator(Store store) {
            this.store = store;
        }

        protected override void RegisterCallbacksOnTarget() {
            scheduleId = target.schedule.Execute(OnTick);
            scheduleId.Every(PollingInterval);
            scheduleId.Pause();

            target.Observe(store.Ghost, value => {
                if (value == null) Disable(); else Enable();
            });
        }

        protected override void UnregisterCallbacksFromTarget() {
            scheduleId.Pause();
        }

        void Enable() {
            scheduleId.Resume();
        }

        void Disable() {
            scheduleId.Pause();
            HideHighlight();
        }

        void OnTick() {
            var screenPos = Pointer.current.position.ReadValue();
            screenPos.y = Screen.height - screenPos.y;
            var panelPos = RuntimePanelUtils.ScreenToPanel(target.panel, screenPos);

            newSlot = target.panel.Pick(panelPos)?.GetFirstOfType<SlotView>();

            if (newSlot is null) {
                HideHighlight();
            } else {
                ShowHighlight(newSlot, store.Ghost.Value);
            }
        }

        void ShowHighlight(SlotView newSlot, Item ghost) {
            if (newSlot == oldSlot) return;
            if (oldSlot != null) {
                oldSlot.RemoveFromClassList("valid");
                oldSlot.RemoveFromClassList("invalid");
            }
            if (newSlot.Slot == null) {
                Debug.LogWarning("Slot cannot be null!");
                return;
            }

            newSlot.WithClass(GetClass(newSlot.Bag, newSlot.Slot, ghost));
            oldSlot = newSlot;
        }

        void HideHighlight() {
            if (oldSlot == null) return;
            oldSlot.RemoveFromClassList("valid");
            oldSlot.RemoveFromClassList("invalid");
            oldSlot = null;
        }

        string GetClass(Bag bag, Slot slot, Item item) => bag.Accepts(item) && slot.Accepts(item) ? "valid" : "invalid";


    }

}