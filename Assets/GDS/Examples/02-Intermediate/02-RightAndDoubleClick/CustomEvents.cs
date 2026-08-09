using UnityEngine.UIElements;
using GDS.Core;
using GDS.Core.Events;

namespace GDS.Examples {
    public class CustomRightClickEvent : ItemCommand {
        public CustomRightClickEvent(Bag bag, Slot slot, Item item, IPointerEvent pointerEvent) : base(bag, slot, item, pointerEvent) { }
    }

    public class CustomDoubleClickEvent : ItemCommand {
        public CustomDoubleClickEvent(Bag bag, Slot slot, Item item, IPointerEvent pointerEvent) : base(bag, slot, item, pointerEvent) { }
    }
}
