using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {
    [Serializable]
    public class SetBag : Bag {
        /// <summary>
        /// Event trigerred when an item has been added, removed or changed.
        /// </summary>
        public event Action<SetSlot> ItemChanged;

        /// <summary>
        /// Event trigerred when collection has been changed.
        /// </summary>
        public event Action CollectionChanged;

        /// <summary>
        /// Event trigerred when the collection has changed substantially. Typically requires a full redraw.
        /// </summary>
        public event Action CollectionReset;

        [SerializeReference]
        public List<SetSlot> Slots = new();

        public int Size { get => Slots.Count; set => Slots = CreateSlots(value); }

        public override IEnumerable<Item> Items => Slots.Where(s => s.Full()).Select(s => s.Item);
        public override Slot FindSlot(Item item) => Slots.Find(s => s.Item == item);

        protected SetSlot GetSlot(string key) => Slots.Find(s => s.Key == key);
        protected SetSlot CloneSlot(SetSlot slot) => new SetSlot() { Key = slot.Key, Tags = slot.Tags.Select(t => t).ToList(), Item = slot == null ? null : ItemExt.Clone(slot.Item, true) };
        protected List<SetSlot> CreateSlots(int size) => Enumerable.Range(0, size).Select(i => new SetSlot { Key = "slot" + i }).ToList();

        public void NotifyChanged(SetSlot slot) {
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
        /// Clears the bag
        /// </summary>
        public override void Clear() {
            Slots.ForEach(s => s.Clear());
            NotifyReset();
        }

        /// <summary>
        /// Adds an item to the specified slot. Can replace an existing item or move a stack to target slot.
        /// </summary>
        /// <param name="slot">Target slot</param>
        /// <param name="item">Item to add</param>
        /// <returns>PlaceItemSuccess or Fail</returns>
        public override Result AddAt(Slot slot, Item item) {
            if (slot is not SetSlot s) return Result.WrongSlotType;
            if (!Accepts(item)) return Result.ItemNotAccepted;
            if (!slot.Accepts(item)) return Result.ItemNotAccepted;
            if (AllowStacking() && slot.Item != null && ItemExt.CanStack(item, slot.Item)) return TransferAll(item, slot, slot.Item);
            var replaced = s.Item;
            s.Item = item;
            NotifyChanged(s);
            return new PlaceItemSuccess(item, replaced);
        }

        /// <summary>
        /// Removes the item from the bag
        /// </summary>
        /// <param name="item">Item to remove</param>
        /// <returns>PickItemSuccess or Fail</returns>
        public override Result Remove(Item item) {
            if (FindSlot(item) is not SetSlot slot) return Result.Fail;
            slot.Item = null;
            NotifyChanged(slot);
            return new PickItemSuccess(item);
        }

        /// <summary>
        /// Transfers the whole stack from source item to target slot (up to max stack) 
        /// </summary>
        /// <returns>PlaceItemSuccess or Fail</returns>
        public override Result TransferAll(Item fromItem, Slot toSlot, Item _) {
            if (toSlot is not SetSlot s) return Result.WrongSlotType;
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
            if (toSlot is not SetSlot s) return Result.WrongSlotType;
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
            if (FindSlot(item) is not SetSlot slot) return Result.WrongSlotType;
            if (!AllowStacking()) return Result.StackingNotAllowed;
            var (newFromItem, newToItem) = slot.Item.SplitHalf();
            slot.Item = newFromItem;
            NotifyChanged(slot);
            return new PickItemSuccess(newToItem);
        }

    }
}