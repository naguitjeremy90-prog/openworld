using GDS.Core;
using UnityEngine.UIElements;

namespace GDS.Examples {

    public class ContainerSlotView : VisualElement {
        Label slotLabel = Dom.Label("slot-label");
        SlotView slotView = new SlotView();
        VisualElement container = Dom.Div("capacity-container");

        public ContainerSlotView(Bag bag, Slot slot) {
            this.Add("container-slot",
                Dom.Div("row",
                    slotView,
                    slotLabel
                ),
                container
            );

            slotView.Init(bag, slot);
            Render(slot);
        }

        public void Render(Slot slot) {
            slotView.Render();
            slotLabel.text = slot.Item?.Name;
            slotLabel.SetVisible(slot.Full());
            container.Clear();
            if (slot.Empty()) return;
            if (slot.Item is not ContainerItem i) return;
            container.Add(new ListBagView().Init(i.Capacity, 5));
        }
    }

}