using System.Collections.Generic;
using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class CreateRandomItem_Controller : MonoBehaviour {

        [Required]
        public Store store;
        [Space(16)]
        public ListBag listBag = new() { Size = 20 };
        [Space(16), Tooltip("An item base will be selected randomly from this list")]
        public List<ItemBase> catalog;

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBag);

            var createItemButton = root.Q<Button>("CreateItem");
            createItemButton.RegisterCallback<ClickEvent>(_ => {
                if (catalog.Count == 0) { Debug.LogWarning("Item Catalog is empty!"); return; }
                if (listBag.Full) { Debug.Log("Bag is full!"); return; }

                var itemBase = catalog[Random.Range(0, catalog.Count)];
                if (itemBase == null) { Debug.Log("ItemBase cannot be null!"); return; }
                var item = itemBase.CreateItem();
                if (item.Stackable) item.StackSize = Random.Range(1, itemBase.MaxStackSize + 1);
                listBag.Add(item);
            });

            var backdrop = root.Q<VisualElement>("Backdrop");
            backdrop.RegisterCallback<PointerUpEvent>(_ => store.Ghost.SetValue(null));
        }
    }

}