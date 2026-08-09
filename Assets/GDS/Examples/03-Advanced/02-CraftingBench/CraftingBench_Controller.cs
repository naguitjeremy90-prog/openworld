using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    public class CraftingBench_Controller : MonoBehaviour {
        [Required]
        public MoveItems_Store Store;

        [Space(12)]
        public CraftingBench Bench = new();

        [Space(12)]
        public ListBag Inventory = new();

        void OnEnable() {
            Store.Main = Inventory;
            Store.Secondary = Bench;

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(Store));
            root.AddManipulator(new HighlightSlotManipulator(Store));

            var craftingBenchView = root.Q<CraftingBenchView>("CraftingBenchView");
            craftingBenchView.Init(Bench);

            var inventoryView = root.Q<ListBagView>("InventoryView");
            inventoryView.Init(Inventory);
        }

    }

}