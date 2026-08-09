using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core;
using GDS.Core.Events;
using GDS.Common.Events;

namespace GDS.Demos.Basic {

    [CreateAssetMenu(menuName = "SO/Demos/Basic/Basic_Store")]
    public class Basic_Store : Store {

        public Basic_Store() {
            Bus.On<PickItem>(OnPickItem);
            Bus.On<PlaceItem>(OnPlaceItem);

            Bus.On<OpenWindow>(OnOpenWindow);
            Bus.On<CloseWindow>(OnCloseWindow);

            Bus.On<ToggleUI>(OnToggleUI);
            Bus.On<CloseUI>(OnCloseUI);

            Bus.On<CollectAll>(OnCollectAll);

            Bus.On<PickWorldItem>(OnPickWorldItem);
            Bus.On<DropWorldItem>(OnDropWorldItem);
        }

        public Observable<bool> UiOpen = new(false);
        public Observable<object> SideWindow = new(null);
        public Observable<int> PlayerGold = new(1000);
        public CharacterSheet CharacterSheet = new();

        public PlayerInventory PlayerInventory { get; private set; }
        Bag Main => PlayerInventory.Inventory;
        Bag Secondary => SideWindow.Value as Bag;

        EventModifiers MoveModifier = EventModifiers.Control;
        EventModifiers StackModifier = EventModifiers.Shift;

        public bool MoveFlagActive(IPointerEvent e) => e.modifiers.HasFlag(MoveModifier);
        public bool StackFlagActive(IPointerEvent e) => e.modifiers.HasFlag(StackModifier);
        public bool ShouldMove(PickItem e) => MoveFlagActive(e.PointerEvent);
        public bool ShouldSplit(PickItem e) => StackFlagActive(e.PointerEvent);
        public bool ShouldTransfer(PlaceItem e) => StackFlagActive(e.PointerEvent) && ItemExt.CanStack(e.Item, e.Slot.Item);
        public Bag GetTargetBag(Bag SourceBag) => SourceBag == Main ? Secondary : Main;

        public void Init(PlayerInventory playerInventory) {
            PlayerInventory = playerInventory;
            CharacterSheet.Init(playerInventory.Equipment);
        }

        public override void Reset() {
            base.Reset();
            UiOpen.Reset();
            PlayerGold.Reset();
            SideWindow.Reset();
        }

        public void ToggleCharacterSheet() {
            if (SideWindow.Value is CharacterSheet) SideWindow.SetValue(null);
            else SideWindow.SetValue(CharacterSheet);
            UiOpen.SetValue(true);
        }

        void OnPickItem(PickItem e) {
            Bag targetBag = GetTargetBag(e.Bag);
            Result result = true switch {
                _ when ShouldMove(e) => BagExt.MoveItem(e, targetBag),
                _ when ShouldSplit(e) => e.Bag.SplitHalf(e.Item),
                _ => e.Bag.Remove(e.Item)
            };
            UpdateGhost(result);

            // When moving an item from a Shop, change the result to a BuyItemSuccess event
            // When moving an item to a Shop, change the result to a SellItemSuccess event
            // When moving an item from a CraftingBench OutcomeSlot, change the result to CraftItemSuccess event
            result = result switch {
                PlaceItemSuccess r when e.Bag is Shop => result.MapTo(new BuyItemSuccess(r.Item)),
                PlaceItemSuccess r when targetBag is Shop => result.MapTo(new SellItemSuccess(r.Item, null)),
                PlaceItemSuccess r when e.Bag is CraftingBench && e.Slot is OutcomeSlot => result.MapTo(new CraftItemSuccess(r.Item)),
                _ => result
            };
            Bus.Publish(result);
        }

        void OnPlaceItem(PlaceItem e) {
            var result = ShouldTransfer(e)
                ? e.Bag.TransferOne(e.Item, e.Slot, e.Slot.Item)
                : e.Bag.AddAt(e.Slot, e.Item);
            UpdateGhost(result);
            Bus.Publish(result);
        }

        void OnOpenWindow(OpenWindow e) {
            LogUtil.LogEvent(e);
            SideWindow.SetValue(e.Handle);
            UiOpen.SetValue(true);
        }

        void OnCloseWindow(CloseWindow e) {
            LogUtil.LogEvent(e);
            SideWindow.SetValue(null);
            if (e.Handle is PlayerInventory) {
                UiOpen.SetValue(false);
            }
        }

        void OnToggleUI(ToggleUI e) {
            UiOpen.Toggle();
            if (UiOpen.Value == false) SideWindow.SetValue(null);
        }

        void OnCloseUI(CloseUI e) {
            UiOpen.SetValue(false);
            SideWindow.SetValue(null);
        }

        void OnCollectAll(CollectAll e) {
            LogUtil.LogEvent(e);
            var result = BagExt.MoveAllItems(e.Bag, Main);
            Bus.Publish(result);
        }

        void OnDropWorldItem(DropWorldItem e) {
            if (Ghost.Value == null) return;
            Bus.Publish(new DropWorldItemSuccess(Ghost.Value));
            Ghost.Reset();
        }

        void OnPickWorldItem(PickWorldItem e) {
            Result result = Main.Add(e.WorldItem.Item);
            if (result is Success) Bus.Publish(new PickWorldItemSuccess(e.WorldItem));
            else Bus.Publish(result);
        }

    }

}