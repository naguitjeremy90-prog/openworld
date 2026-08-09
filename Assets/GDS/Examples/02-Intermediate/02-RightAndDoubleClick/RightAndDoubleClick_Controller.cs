using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core;

namespace GDS.Examples {
    [RequireComponent(typeof(UIDocument))]
    public class RightAndDoubleClick_Controller : MonoBehaviour {
        [Required, InlineEditor]
        public RightAndDoubleClick_Store store;
        [Space(12)]
        public ListBag bagLeft = new() { Size = 20 };
        [Space(12)]
        public ListBag bagRight = new() { Size = 20 };

        void OnEnable() {
            store.Left = bagLeft;
            store.Right = bagRight;

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new RightClickManipulator(store));
            root.AddManipulator(new DoubleClickManipulator(store));

            var listBagViewLeft = root.Q<ListBagView>("Left");
            listBagViewLeft.Init(bagLeft);

            var listBagViewRight = root.Q<ListBagView>("Right");
            listBagViewRight.Init(bagRight);
        }

    }
}