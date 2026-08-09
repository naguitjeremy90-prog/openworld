using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [CreateAssetMenu(menuName = "SO/Examples/MoveItems_Store")]
    public class MoveItems_Store : Store {
        public MoveItems_Store() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceItem>(OnPlaceItem);
        }

        public EventModifiers MoveModifier = EventModifiers.Control;
        public bool ShouldMove(IPointerEvent e) => e.modifiers.HasFlag(MoveModifier);


        public Bag Main { get; set; }
        public Bag Secondary { get; set; }

        public void OnPickItem(PickItem e) {
            Result result;
            if (ShouldMove(e.PointerEvent)) {
                Bag targetBag = e.Bag == Main ? Secondary : Main;
                result = BagExt.MoveItem(e, targetBag);
            } else {
                result = e.Bag.Remove(e.Item);
                UpdateGhost(result);
            }
            Bus.Publish(result);
        }

        public void OnPlaceItem(PlaceItem e) {
            Result result = e.Bag.AddAt(e.Slot, Ghost.Value);
            UpdateGhost(result);
            Bus.Publish(result);
        }

    }

}