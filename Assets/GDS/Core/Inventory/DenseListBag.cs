using System;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {
    [Serializable]
    public class DenseListBag : ListBag {
        public override Result AddAt(Slot slot, Item item) {
            return base.Add(item);
        }

        public override Result Remove(Item item) {
            if (FindSlot(item) is not ListSlot slot) { Debug.LogWarning($"couldn't find item {item} in {this}"); return Result.Fail; }
            for (var i = slot.Index; i < Size - 1; i++) Slots[i].Item = Slots[i + 1].Item;
            Slots[Size - 1].Item = null;
            NotifyReset();
            return new PickItemSuccess(item);
        }
    }
}