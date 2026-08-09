using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Demos.Basic {
    public class OutcomeSlot : Slot {
        public override bool Accepts(Item item) => false;
    }

    [Serializable]
    public class CraftingBench : ListBag {
        public List<Recipe> Recipes = new();
        public Observable<OutcomeSlot> OutcomeSlot = new(new());
        public Observable<Item> OutcomeItem = new(null);

        Recipe MatchingRecipe;
        public CraftingBench() {
            Size = 3;
            CollectionChanged += OnCollectionChanged;
        }

        // When slots change, try to find a matching recipe and populate the outcome slot.
        // Picking the item from Outcome slot will create a new item and consume ingredients.
        // After consuming ingredients the recipe might not be valid any more which will clear the outcome slot.
        void OnCollectionChanged() {
            MatchingRecipe = Recipes.FirstOrDefault(r => r.Ingredients.SequenceEqual(Slots.Select(s => s.Item?.Base)));

            if (MatchingRecipe == null) {
                OutcomeSlot.Value.Item = null;
                OutcomeSlot.Notify();
            } else {
                Item item = MatchingRecipe.Outcome.CreateItem();
                OutcomeSlot.Value.Item = item;
                OutcomeSlot.Notify();
            }

            Debug.Log($"MatchingRecipe: {MatchingRecipe}");
        }

        public override Result Remove(Item item) {
            if (OutcomeSlot.Value.Item == item) return CraftItem(item.Base);
            return base.Remove(item);
        }

        public Result CraftItem(ItemBase itemBase) {
            if (MatchingRecipe == null) return Result.Fail;

            var newItem = itemBase.CreateItem();
            Slots.ForEach(Decrement);
            NotifyChanged();
            NotifyReset();

            return new CraftItemSuccess(newItem);
        }

        void Decrement(Slot slot) {
            if (slot.Item == null) return;
            if (!slot.Item.Stackable) return;
            slot.Item.StackSize -= 1;
            if (slot.Item.StackSize <= 0) slot.Item = null;
        }
    }
}
