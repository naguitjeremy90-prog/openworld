using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class ToggleUI_Controller : MonoBehaviour {
        [Required]
        public Store Store;

        [Space(16)]
        public ListBag listBag = new() { Size = 20 };

        // Root element
        VisualElement root;
        // UI visibility flag
        bool rootVisible = true;

        void OnEnable() {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(Store));
            root.Q<ListBagView>().Init(listBag);
        }

        // Required by PlayerInput component
        public void OnToggleUI() {
            rootVisible = !rootVisible;
            root.SetVisible(rootVisible);
        }
        public void OnCloseUI() {
            rootVisible = false;
            root.SetVisible(rootVisible);
        }


    }

}