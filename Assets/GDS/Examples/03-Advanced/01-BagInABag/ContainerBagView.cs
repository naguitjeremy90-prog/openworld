using System.Collections.Generic;
using System.Linq;
using GDS.Core;
using UnityEngine.UIElements;

namespace GDS.Examples {

#if UNITY_6000_0_OR_NEWER
    [UxmlElement]    
#endif

    public partial class ContainerBagView : VisualElement {

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<ContainerBagView> { }
#endif

        ListBag Bag;
        ContainerSlotView[] slotViews;

        public ContainerBagView() {
            AddToClassList("container-list-bag");
            RenderPreview();
        }

        public void Init(ListBag bag) {
            Bag = bag;
            Bag.ItemChanged += OnItemChanged;
            Bag.CollectionReset += Render;

            slotViews = Bag.Slots.Select(s => new ContainerSlotView(Bag, s)).ToArray();

            Render();
        }

        void OnItemChanged(ListSlot slot) {
            slotViews[slot.Index].Render(slot);
        }

        void Render() {
            slotViews = Bag.Slots.Select(s => new ContainerSlotView(Bag, s)).ToArray();

            Clear();
            Add(Dom.Div("slot-container", slotViews));
        }

        void RenderPreview() {
            Clear();
            Add(Dom.Div("slot-container",
                SlotPreview(),
                SlotPreview()
            ));
        }

        VisualElement SlotPreview() {
            var bagPreview = new ListBagView();
            bagPreview.SetPreviewSlots(4);
            var slotView = Dom.Div("slot", Dom.Div("preview-image2"));
            var el = Dom.Div("container-slot",
                Dom.Div("row", slotView, new Label("Preview")),
                Dom.Div("capacity-container", bagPreview)
            );
            return el;
        }



    }

}