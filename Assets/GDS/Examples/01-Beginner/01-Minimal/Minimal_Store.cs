using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Examples {

    [CreateAssetMenu(menuName = "SO/Examples/Minimal_Store")]
    public class Minimal_Store : Store {
        public Minimal_Store() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceItem>(OnPlaceItem);
        }

        void OnPickItem(PickItem e) {
            Result result = e.Bag.Remove(e.Item);
            UpdateGhost(result);
            Bus.Publish(result);
        }

        void OnPlaceItem(PlaceItem e) {
            Result result = e.Bag.AddAt(e.Slot, e.Item);
            UpdateGhost(result);
            Bus.Publish(result);
        }
    }

}
