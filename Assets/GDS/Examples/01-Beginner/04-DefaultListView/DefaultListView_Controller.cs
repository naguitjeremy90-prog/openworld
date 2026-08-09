using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class DefaultListView_Controller : MonoBehaviour {

        [Required]
        public MoveItems_Store store;
        [Space(12)]
        public ListBag list1 = new() { Size = 10 };
        [Space(12)]
        public ListBag list2 = new() { Size = 20 };

        private void OnEnable() {
            store.Main = list1;
            store.Secondary = list2;

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.AddManipulator(new TooltipManipulator());

            var listView = root.Q<ListView>();
            listView.itemsSource = list1.Slots;
            // The CustomSlot needs to implement IItemContext interface (required by DragDrop behavior)            
            listView.makeItem = () => new CustomSlot();
            listView.bindItem = (el, i) => {
                var slot = el as CustomSlot;
                slot.Bag = list1;
                slot.Slot = list1.Slots[i];
                slot.Item = list1.Slots[i].Item;
                slot.text = ItemExt.ToPrettyString(slot.Slot.Item);
            };

            list1.CollectionChanged += listView.RefreshItems;
            listView.RegisterCallback<DetachFromPanelEvent>(_ => list1.CollectionChanged -= listView.RefreshItems);

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(list2);
        }
    }

}