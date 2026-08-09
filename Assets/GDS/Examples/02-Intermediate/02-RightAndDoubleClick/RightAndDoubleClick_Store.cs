using UnityEngine;
using GDS.Core;

namespace GDS.Examples {
    [CreateAssetMenu(menuName = "SO/Examples/RightAndDoubleClick_Store")]
    public class RightAndDoubleClick_Store : Store {

        public RightAndDoubleClick_Store() {
            Bus.On<CustomRightClickEvent>(OnRightClick);
            Bus.On<CustomDoubleClickEvent>(OnDoubleClick);
        }

        public ListBag Left { get; set; }
        public ListBag Right { get; set; }

        void OnRightClick(CustomRightClickEvent e) {
            Debug.Log(e);
            ListBag targetBag = e.Bag == Left ? Right : Left;
            BagExt.MoveItem(e.Bag, e.Item, targetBag);
        }

        void OnDoubleClick(CustomDoubleClickEvent e) {
            Debug.Log(e);
            ListBag targetBag = e.Bag == Left ? Right : Left;
            BagExt.MoveItem(e.Bag, e.Item, targetBag);
        }

    }
}