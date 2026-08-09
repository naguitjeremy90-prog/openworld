using GDS.Common.Events;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Examples {

    [CreateAssetMenu(menuName = "SO/Examples/DiscardItem_Store")]
    public class DiscardItem_Store : Store {
        public DiscardItem_Store() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceItem>(OnPlaceItem);
            Bus.On<PickWorldItem>(OnPickWorldItem);
            Bus.On<DropWorldItem>(OnDropWorldItem);
        }

        [HideInInspector]
        public ListBag Bag;

        void OnPickItem(PickItem e) {
            Result result = e.Bag.Remove(e.Item);
            UpdateGhost(result);
        }

        void OnPlaceItem(PlaceItem e) {
            Result result = e.Bag.AddAt(e.Slot, Ghost.Value);
            UpdateGhost(result);
        }

        void OnDropWorldItem(DropWorldItem e) {
            if (Ghost.Value == null) return;
            Bus.Publish(new DropWorldItemSuccess(Ghost.Value));
            Ghost.Reset();
        }

        void OnPickWorldItem(PickWorldItem e) {
            Result result = Bag.Add(e.WorldItem.Item);
            if (result is Success) Bus.Publish(new PickWorldItemSuccess(e.WorldItem));
            else Bus.Publish(result);
        }
    }

}