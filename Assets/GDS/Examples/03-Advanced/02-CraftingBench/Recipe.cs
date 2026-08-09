using System.Collections;
using System.Collections.Generic;
using GDS.Core;
using UnityEngine;

namespace GDS.Examples {

    [CreateAssetMenu(menuName = "SO/Examples/Crafting/Recipe")]
    public class Recipe : ScriptableObject {
        public ItemBase Outcome;
        public List<ItemBase> Ingredients = new() { null, null, null };

        public override string ToString() {
            return $"Recipe: {Ingredients.CommaJoin()} -> {Outcome.Name}";
        }
    }
}