using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core {

#if UNITY_6000_0_OR_NEWER
    [UxmlElement]    
#endif

    public partial class SlotView : VisualElement, IItemContext, IHoveredItemContext {

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<SlotView> { }
#endif

        public Bag Bag => bag;
        public Slot Slot => slot;
        public Item Item => Slot?.Item;
        public Rect WorldBound => worldBound;

        protected Bag bag;
        protected Slot slot;
        protected ItemView itemView = new();
        protected VisualElement overlay = Dom.Div("overlay slot-overlay").WithName("Overlay");
        protected Label debug = Dom.Label("absolute debug-label", "debug");

        public SlotView() => this.Add("slot", itemView, overlay, debug);
        public SlotView(Bag b, Slot s) : this() => Init(b, s);
        public SlotView Init(Bag b, Slot s) {
            bag = b;
            slot = s;
            debug.text = DebugText(s);
            Render();
            return this;
        }

        public virtual void Render() {
            itemView.Item = Item;
            itemView.SetVisible(slot.Full());
            EnableInClassList("empty", slot.Empty());
        }

        string DebugText(Slot slot) => slot switch {
            ListSlot s => s.Index.ToString(),
            SetSlot s => s.Key,
            _ => ""
        };

    }
}