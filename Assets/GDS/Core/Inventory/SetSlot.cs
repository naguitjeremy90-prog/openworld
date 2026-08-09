using System;

namespace GDS.Core {
    [Serializable]
    public class SetSlot : Slot {
        public string Key;

        public SetSlot() : base() { }
        public SetSlot(string key) : this() => Key = key;

        public override string ToString() => Item == null
        ? $"[{Key}] <empty>"
        : $"[{Key}] {Item.Name} ({Item.Id})";
    }
}