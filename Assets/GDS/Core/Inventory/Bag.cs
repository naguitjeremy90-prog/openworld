using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {
    [Serializable]
    public abstract class Bag {
        public string Name;
        public virtual IEnumerable<Item> Items { get => Enumerable.Empty<Item>(); }

        public virtual Result CanAdd(Item item) => Result.Success;
        public virtual Result CanRemove(Item item) => Result.Success;
        public virtual Result Add(Item item) => Result.Fail;
        public virtual Result AddAt(Slot slot, Item item) => Result.Fail;
        public virtual Result AddRange(IEnumerable<Item> items) => Result.Fail;
        public virtual Result Remove(Item item) => Result.Fail;
        public virtual Result TransferAll(Item fromItem, Slot toSlot, Item toItem) => Result.Fail;
        public virtual Result TransferOne(Item fromItem, Slot toSlot, Item toItem) => Result.Fail;
        public virtual Result SplitHalf(Item item) => Result.Fail;

        public virtual void Clear() { }
        public virtual bool Accepts(Item item) => true;
        public virtual bool AllowStacking() => true;
        public virtual Slot FindSlot(Item item) => null;
    }

    public static class BagExt {
        public static Result MoveItem(PickItem e, Bag toBag) => MoveItem(e.Bag, e.Item, toBag);
        public static Result MoveItem(Bag bag, Item item, Bag toBag) {
            // Debug.Log($"should move item {item} from {bag} to {toBag}");
            if (toBag == null) return Result.Fail;
            if (!toBag.Accepts(item)) return Result.Fail;

            Result result = toBag.CanAdd(item);
            if (result is Fail) { Debug.LogWarning($"can't add {item} to {toBag}"); return result; }

            result = bag.CanRemove(item);
            if (result is Fail) { Debug.LogWarning($"Can't remove {item} from {bag}"); return result; }

            result = bag.Remove(item);
            if (result is Fail) { Debug.LogWarning($"tried removing but failed {bag}, {item}"); return result; }

            result = toBag.Add(item);
            if (result is Fail) { Debug.LogWarning($"tried adding but failed {toBag}, {item}"); return result; }

            return new PlaceItemSuccess(item, null);
        }

        public static Result MoveAllItems(Bag fromBag, Bag toBag) {
            List<Item> remaining = new();
            foreach (var i in fromBag.Items) {
                var result = toBag.Add(i);
                if (result is Fail) remaining.Add(i);
            }

            fromBag.Clear();
            fromBag.AddRange(remaining);

            if (remaining.Count > 0) return Result.Fail;
            return new PlaceItemSuccess(null, null);
        }
    }

}