using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {
    [Serializable]
    public class ListBag : Bag {

        /// <summary>
        /// Event trigerred when an item has been added, removed or changed.
        /// </summary>
        public event Action<ListSlot> ItemChanged;

        /// <summary>
        /// Event trigerred when collection has been changed.
        /// </summary>
        public event Action CollectionChanged;

        /// <summary>
        /// Event trigerred when the collection has changed substantially. Typically requires a full redraw.
        /// </summary>
        public event Action CollectionReset;

        [SerializeReference]
        public List<ListSlot> Slots = new();

        public int Size { get => Slots.Count; set => Slots = CreateSlots(value); }
        public bool Full => Slots.Count(s => s.Empty()) == 0;

        public override IEnumerable<Item> Items => Slots.Where(s => s.Full()).Select(s => s.Item);
        public override Slot FindSlot(Item item) => Slots.Find(s => s.Item == item);

        protected List<Item> CreateItems(int size) => Enumerable.Range(0, size).Select<int, Item>(i => null).ToList();
        protected List<ListSlot> CreateSlots(int size) => Enumerable.Range(0, size).Select(i => new ListSlot { Index = i }).ToList();

        public void NotifyChanged(ListSlot slot) {
            ItemChanged?.Invoke(slot);
            NotifyChanged();
        }

        public void NotifyChanged() {
            CollectionChanged?.Invoke();
        }

        public void NotifyReset() {
            CollectionReset?.Invoke();
        }

        /// <summary>
        /// Clears the bag.
        /// </summary>
        public override void Clear() {
            Slots.ForEach(s => s.Clear());
            NotifyReset();
        }

        /// <summary>
        /// Checks whether the bag can accept the item and has available capacity.
        /// </summary>
        /// <param name="item">The item to check</param>
        /// <returns>ItemNotAccepted if bag does not accept the item. ItemCannotFit if the bag doesn't have enough capacity. Success if the item can be added.</returns>
        public override Result CanAdd(Item item) {
            if (!Accepts(item)) return Result.ItemNotAccepted;
            if (Full) return Result.ItemCannotFit;
            return Result.Success;
        }

        /// <summary>
        /// Adds an item to the bag (without notifying subscribers)
        /// </summary>
        /// <param name="item">The item to add</param>
        /// <returns>Item index in the list if it was added; -1 otherwise.</returns>
        public int AddNoNotify(Item item) {
            var index = Slots.FindIndex(SlotExt.Empty);
            if (index == -1) return index;
            var slot = Slots[index];
            slot.Item = item;
            return index;
        }

        /// <summary>
        /// Adds an item to the bag
        /// </summary>
        /// <param name="item">The item to add</param>
        /// <returns>ItemNotAccepted if bag does not accept the item. ItemCannotFit if the bag doesn't have enough capacity. PlaceItemSuccess if the item was added.</returns>
        public override Result Add(Item item) {
            if (!Accepts(item)) return Result.ItemNotAccepted;
            var index = AddNoNotify(item);
            if (index == -1) return Result.ItemCannotFit;
            NotifyChanged(Slots[index]);
            return new PlaceItemSuccess(item, null);
        }

        /// <summary>
        /// Adds an item to the specified slot. Can replace an existing item or move a stack to target slot.
        /// </summary>
        /// <param name="slot">Target slot</param>
        /// <param name="item">Item to add</param>
        /// <returns>PlaceItemSuccess or Fail</returns>
        public override Result AddAt(Slot slot, Item item) {
            if (slot is not ListSlot s) return Result.WrongSlotType;
            if (!Accepts(item)) return Result.ItemNotAccepted;
            if (!s.Accepts(item)) return Result.ItemNotAccepted;
            if (AllowStacking() && slot.Item != null && item.CanStack(slot.Item)) return TransferAll(item, slot, slot.Item);
            var replaced = s.Item;
            s.Item = item;
            NotifyChanged(s);
            return new PlaceItemSuccess(item, replaced);
        }

        /// <summary>
        /// Adds a collection of items to the bag
        /// </summary>
        /// <param name="items">Items to add</param>
        /// <returns>Success if all items where added; Fail otherwise</returns>
        public override Result AddRange(IEnumerable<Item> items) {
            bool success = true;
            foreach (var item in items) { if (AddNoNotify(item) == -1) success = false; }
            NotifyReset();
            return success == true ? Result.Success : Result.Fail;
        }

        /// <summary>
        /// Removes the item from the bag
        /// </summary>
        /// <param name="item">Item to remove</param>
        /// <returns>PickItemSuccess or Fail</returns>
        public override Result Remove(Item item) {
            if (FindSlot(item) is not ListSlot slot) { Debug.LogWarning($"Could not find item {item} in {this}!"); return Result.Fail; }
            slot.Item = null;
            NotifyChanged(slot);
            return new PickItemSuccess(item);
        }

        /// <summary>
        /// Transfers the whole stack from source item to target slot (up to max stack) 
        /// </summary>
        /// <returns>PlaceItemSuccess or Fail</returns>
        public override Result TransferAll(Item fromItem, Slot toSlot, Item _) {
            if (toSlot is not ListSlot s) return Result.WrongSlotType;
            if (!Accepts(fromItem)) return Result.ItemNotAccepted;
            if (!AllowStacking()) return Result.StackingNotAllowed;
            var (newFromItem, newToitem) = fromItem.TransferAll(toSlot.Item);
            toSlot.Item = newToitem;
            NotifyChanged(s);
            return new PlaceItemSuccess(newToitem, newFromItem);
        }

        /// <summary>
        /// Transfers one from a source item to target slot
        /// </summary>
        /// <returns>PlaceItemSuccess or Fail</returns>
        public override Result TransferOne(Item fromItem, Slot toSlot, Item _) {
            if (toSlot is not ListSlot s) return Result.WrongSlotType;
            if (!Accepts(fromItem)) return Result.ItemNotAccepted;
            if (!AllowStacking()) return Result.StackingNotAllowed;
            var (newFromItem, newToItem) = fromItem.TransferOne(toSlot.Item);
            toSlot.Item = newToItem;
            NotifyChanged(s);
            return new PlaceItemSuccess(newToItem, newFromItem);
        }

        /// <summary>
        /// Splits a stack of items in half
        /// </summary>
        /// <param name="item">The item to split</param>
        /// <returns>PickItemSuccess or Fail</returns>
        public override Result SplitHalf(Item item) {
            if (FindSlot(item) is not ListSlot slot) return Result.WrongSlotType;
            if (!AllowStacking()) return Result.StackingNotAllowed;
            var (newFromItem, newToItem) = slot.Item.SplitHalf();
            slot.Item = newFromItem;
            NotifyChanged(slot);
            return new PickItemSuccess(newToItem);
        }

    }
}