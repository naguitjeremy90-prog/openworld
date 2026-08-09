using UnityEngine;
using GDS.Core;

namespace GDS.Demos.Basic {

    [CreateAssetMenu(menuName = "SO/Demos/Basic/Basic_ItemBase")]
    public class Basic_ItemBase : ItemBase {
        public int Weight = 1;
        public int Cost = 1;
        public override Item CreateItem() => new Basic_Item() { Base = this, Name = Name, StackSize = MaxStackSize };
    }

    [System.Serializable]
    public class Basic_Item : Item {
        public Rarity Rarity;
    }

    public enum Rarity { NoRarity, Common, Magic, Rare, Unique }

    public static class Basic_ItemExt {
        public static Rarity Rarity(this Item item) => item is Basic_Item i ? i.Rarity : Basic.Rarity.NoRarity;
        public static int Weight(this Item item) => item.Base is Basic_ItemBase b ? b.Weight : 1;
        public static int Cost(this Item item) => item.Base switch {
            Basic_ItemBase { Stackable: true } b => b.Cost * item.StackSize,
            Basic_ItemBase b => b.Cost,
            _ => 1
        };
    }

}