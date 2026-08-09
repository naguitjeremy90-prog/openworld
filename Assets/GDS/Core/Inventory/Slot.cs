using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GDS.Core {
    [Serializable]
    public class Slot {
        [SerializeReference]
        public Item Item;

        public List<Tag> Tags = new();
        public override string ToString() => $"Item = {Item}";
        public virtual bool Accepts(Item item) {
            if (item is not Item i) return true;
            if (Tags.Count > 0 && Tags.Intersect(i.Base.Tags).Count() == 0) return false;
            return true;
        }

        public bool Restricted => Tags.Count > 0;
    }

    public static class SlotExt {
        public static bool Empty(this Slot slot) => slot.Item == null;
        public static bool Full(this Slot slot) => slot.Item != null;
        public static void Clear(this Slot slot) => slot.Item = null;
    }
}