using System.Collections.Generic;
using System.Linq;
using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif

    public partial class CraftingBenchView : VisualElement {

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<CraftingBenchView, UxmlTraits> { }
#endif

        CraftingBench bag;
        SlotView OutcomeSlotView;
        ListBagView ListBagView;

        public CraftingBenchView() {
            var asset = Resources.Load<VisualTreeAsset>("CraftingBenchAsset");
            Add(asset.CloneTree());

            OutcomeSlotView = this.Q<SlotView>("OutcomeSlot");
            ListBagView = this.Q<ListBagView>("ListBagView");
        }

        public void Init(CraftingBench bag) {
            this.bag = bag;
            Debug.Log(bag.Recipes.NewLineJoin());
            Add(RecipesListView(bag.Recipes));
            OutcomeSlotView.Init(bag, bag.OutcomeSlot.Value);
            ListBagView.Init(bag);
            this.Observe(bag.OutcomeSlot, _ => OutcomeSlotView.Render());
        }

        VisualElement RecipesListView(List<Recipe> recipes) => Dom.Div(recipes.Select(RecipePreview).ToArray());

        VisualElement RecipePreview(Recipe recipe) => Dom.Div("row mt-10",
            Dom.Div("row gap-h-5", recipe.Ingredients.Where(i => i != null).Select(b => Dom.Image("default-border", b.Icon)).ToArray()),
            new Label("￫") { style = { height = 32, fontSize = 24 } },
            Dom.Image("default-border ml-5", recipe.Outcome.Icon)
        );

    }

}