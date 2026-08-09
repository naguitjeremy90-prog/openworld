using GDS.Core;

namespace GDS.Demos.Basic {
    [System.Serializable]
    public class Equipment : SetBag {

        public SetSlot Helmet = new(nameof(Helmet));
        public SetSlot Gloves = new(nameof(Gloves));
        public SetSlot Boots = new(nameof(Boots));
        public SetSlot Body = new(nameof(Body));
        public SetSlot Weapon = new(nameof(Weapon));

        public Equipment() {
            Name = "Equipment";
            Slots = new() { Helmet, Gloves, Boots, Body, Weapon };
        }
    }
}
