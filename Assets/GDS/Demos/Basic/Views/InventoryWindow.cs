using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Demos.Basic {

    public class InventoryWindow : WindowView {

        public InventoryWindow(PlayerInventory bag, Basic_Store store) {
            Init("Inventory", () => store.Bus.Publish(new CloseWindow(bag)));

            this.WithClass("inventory-window");

            var goldAsset = Resources.Load<VisualTreeAsset>("PlayerGold");
            var goldView = goldAsset.Instantiate().WithClass("absolute");
            goldView.style.right = 16;
            var goldLabel = goldView.Q<Label>("GoldLabel");

            var equipmentView = new SetBagView().Init(bag.Equipment, bag.Equipment.Name);
            var inventoryView = new ListBagView().Init(bag.Inventory, 10, bag.Inventory.Name);
            inventoryView.WithClass("mt-16");

            Container.Add(equipmentView, inventoryView, goldView);

            this.Observe(store.PlayerGold, value => {
                goldLabel.text = value.ToString();
            });
        }

    }

}