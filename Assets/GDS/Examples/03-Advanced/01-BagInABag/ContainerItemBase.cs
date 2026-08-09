using GDS.Core;
using UnityEngine;

namespace GDS.Examples {

    [CreateAssetMenu(menuName = "SO/Examples/ContainerItemBase")]
    public class ContainerItemBase : ItemBase {
        public int Capacity;

        public override Item CreateItem() {
            return new ContainerItem() { Base = this, Name = Name, Capacity = new ListBag() { Size = Capacity } };
        }
    }

    [System.Serializable]
    public class ContainerItem : Item {
        [Space(16), ShowField(nameof(ListBag.Slots))]
        public ListBag Capacity;
    }

}