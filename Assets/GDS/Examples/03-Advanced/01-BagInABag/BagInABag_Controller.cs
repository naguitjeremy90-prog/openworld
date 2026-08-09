using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class BagInABag_Controller : MonoBehaviour {
        [Required]
        public Store store;
        [Space(16)]
        public ListBag bag = new() { Size = 4, Name = "ContainerBag" };

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.Q<ContainerBagView>().Init(bag);
        }
    }

}