using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;
using GDS.Common.Events;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class DiscardItem_Controller : MonoBehaviour {

        [Required]
        public DiscardItem_Store Store;

        [Space(12)]
        public ListBag Bag = new() { Size = 20 };

        bool uiOpen = true;
        VisualElement root;

        void OnEnable() {
            Store.Bag = Bag;

            root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(Store));

            var listBagView = root.Q<ListBagView>().Init(Bag);
            var backdrop = root.Q<VisualElement>(name = "Backdrop");
            backdrop.RegisterCallback<PointerUpEvent>(e => {
                if (Store.Ghost.Value == null) return;
                Store.Bus.Publish(new DropWorldItem(Store.Ghost.Value));
            });
        }

        // Required by Input Actions
        public void OnToggleInventory() {
            uiOpen = !uiOpen;
            root.SetVisible(uiOpen);
        }

    }

}