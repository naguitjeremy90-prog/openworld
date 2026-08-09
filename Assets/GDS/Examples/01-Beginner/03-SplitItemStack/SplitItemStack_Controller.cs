using System.Linq;
using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class SplitItemStack_Controller : MonoBehaviour {

        [Required, InlineEditor]
        public Store store;
        [Space(16)]
        public ListBag listBag = new() { Size = 20 };
        [Space(16)]
        public SetBag setBag = new() { Size = 5 };

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            root.Q<ListBagView>().Init(listBag);
            root.Q<SetBagView>().Init(setBag);
        }
    }

}