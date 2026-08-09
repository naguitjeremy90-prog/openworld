using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    public class EquipItems_Controller : MonoBehaviour {
        [Required]
        public Store Store;

        public SetBag Equipment = new() { Size = 5 };
        public ListBag Inventory = new() { Size = 20 };

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(Store));
            root.AddManipulator(new HighlightSlotManipulator(Store));

            var equipmentView = root.Q<SetBagView>();
            equipmentView.Init(Equipment);

            var inventoryView = root.Q<ListBagView>();
            inventoryView.Init(Inventory);
        }
    }
}
