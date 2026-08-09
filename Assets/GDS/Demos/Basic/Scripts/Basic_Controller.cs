using GDS.Common.Events;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Demos.Basic {

    [RequireComponent(typeof(UIDocument))]
    public class Basic_Controller : MonoBehaviour {
        [Required, InlineEditor]
        public Basic_Store Store;
        [Space(12), Required]
        public VisualTreeAsset TooltipViewAsset;
        [Space(12)]
        public PlayerInventory playerInventory;

        private void OnEnable() {
            Store.Init(playerInventory);

            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(Store));
            root.AddManipulator(new HighlightSlotManipulator(Store));
            root.AddManipulator(new TooltipManipulator(new BasicTooltipView(TooltipViewAsset)));

            var left = root.Q<VisualElement>("Left").PickIgnore();
            var right = root.Q<VisualElement>("Right").PickIgnore();
            var backdrop = root.Q<VisualElement>("Backdrop");


            right.Add(new InventoryWindow(Store.PlayerInventory, Store));

            root.Observe(Store.UiOpen, value => root.SetVisible(value));
            root.Observe(Store.SideWindow, value => {
                left.Clear();
                if (value == null) return;
                left.Add(CreateSideWindowView(value, Store));
            });

            backdrop.RegisterCallback<PointerUpEvent>(_ => {
                if (Store.Ghost.Value == null) return;
                Store.Bus.Publish(new DropWorldItem(Store.Ghost.Value));
            });


        }

        //  Creates a side window 
        VisualElement CreateSideWindowView(object handle, Basic_Store store) => handle switch {
            CharacterSheet b => new CharacterSheetWindow(b, store),
            Chest b => new ChestWindow(b, store),
            Stash b => new StashWindow(b, store),
            Shop b => new ShopWindow(b, store),
            CraftingBench b => new CraftingBenchWindow(b, store),
            _ => Dom.Div()
        };

        // These methods are called by the PlayerInput component
        public void OnCloseUi() => Store.Bus.Publish(new CloseUI());
        public void OnToggleInventory() => Store.Bus.Publish(new ToggleUI());
        public void OnToggleCharacterSheet() => Store.ToggleCharacterSheet();
    }

}