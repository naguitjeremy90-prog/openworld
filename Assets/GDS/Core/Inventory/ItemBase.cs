using System.Collections.Generic;
using UnityEngine;

namespace GDS.Core {

    [CreateAssetMenu(menuName = "SO/Core/ItemBase")]
    public class ItemBase : ScriptableObject {
        public string Name;
        public Sprite Icon;
        public bool Stackable = false;
        public int MaxStackSize = 100;
        public List<Tag> Tags = new();

        public void OnEnable() => Name ??= name;
        virtual public Item CreateItem() => new Item() { Base = this, Name = Name, StackSize = Stackable ? MaxStackSize : 1 };
    }
}