using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {
    public class CustomSlot : Label, IItemContext, IHoveredItemContext {
        public Bag Bag { get; set; }
        public Slot Slot { get; set; }
        public Item Item { get; set; }
        public Rect WorldBound => worldBound;
    }
}