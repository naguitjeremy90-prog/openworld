using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [CreateAssetMenu(menuName = "SO/Examples/StackItems_Store")]
    public class SplitItemStack_Store : Store {
        public SplitItemStack_Store() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceItem>(OnPlaceItem);
        }

        public EventModifiers StackModifier = EventModifiers.Shift;

        bool ShouldStack(IPointerEvent e) => e.modifiers.HasFlag(StackModifier);

        void OnPickItem(PickItem e) {
            Result result = ShouldStack(e.PointerEvent)
                ? e.Bag.SplitHalf(e.Item)
                : e.Bag.Remove(e.Item);

            UpdateGhost(result);
            Bus.Publish(result);
        }

        void OnPlaceItem(PlaceItem e) {
            Result result = ShouldStack(e.PointerEvent) && ItemExt.CanStack(e.Item, e.Slot.Item)
                ? e.Bag.TransferOne(e.Item, e.Slot, e.Slot.Item)
                : e.Bag.AddAt(e.Slot, e.Item);

            UpdateGhost(result);
            Bus.Publish(result);
        }

    }

}
