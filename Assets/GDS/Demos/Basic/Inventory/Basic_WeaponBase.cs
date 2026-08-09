using GDS.Core;
using UnityEngine;

namespace GDS.Demos.Basic {

    [CreateAssetMenu(menuName = "SO/Demos/Basic/Basic_WeaponBase")]
    public class Basic_WeaponBase : Basic_ItemBase {
        public float AttackSpeed;
        public IntRange DamageRange;
        public float Dps => DamageRange.Avg * AttackSpeed;

        public override Item CreateItem() => new Basic_Weapon { Base = this, Name = Name, AttackDamage = DamageRange.Roll() };
    }

    [System.Serializable]
    public class Basic_Weapon : Basic_Item {
        public int AttackDamage;
        public float AttackSpeed => (Base as Basic_WeaponBase).AttackSpeed;
        public float Dps => AttackDamage * AttackSpeed;
    }

}