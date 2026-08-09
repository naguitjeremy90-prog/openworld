using GDS.Core;
using UnityEngine;

namespace GDS.Demos.Basic {

    [CreateAssetMenu(menuName = "SO/Demos/Basic/Basic_ArmorBase")]
    public class Basic_ArmorBase : Basic_ItemBase {
        public IntRange Defense;
        public override Item CreateItem() => new Basic_Armor { Base = this, Name = Name, Defense = Defense.Roll() };
    }

    [System.Serializable]
    public class Basic_Armor : Basic_Item {
        public int Defense;
    }

}