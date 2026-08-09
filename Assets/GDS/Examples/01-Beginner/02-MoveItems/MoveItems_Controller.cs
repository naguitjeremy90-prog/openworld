using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class MoveItems_Controller : MonoBehaviour {
        [Required]
        public MoveItems_Store store;
        [Space(12)]
        public ListBag bagLeft = new() { Size = 20 };
        [Space(12)]
        public ListBag bagRight = new() { Size = 20 };

        void OnEnable() {
            store.Main = bagRight;
            store.Secondary = bagLeft;

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            var listBagViewLeft = root.Q<ListBagView>("Left");
            listBagViewLeft.Init(bagLeft);

            var listBagViewRight = root.Q<ListBagView>("Right");
            listBagViewRight.Init(bagRight);
        }

    }

}