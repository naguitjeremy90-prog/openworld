using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class Minimal_Controller : MonoBehaviour {

        [Required]
        public Store store;

        [Space(16)]
        public ListBag listBag = new() { Size = 20 };

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.AddManipulator(new TooltipManipulator());

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBag);
        }
    }

}