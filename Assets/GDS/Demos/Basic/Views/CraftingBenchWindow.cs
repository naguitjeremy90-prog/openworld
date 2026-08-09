using System.Collections.Generic;
using System.Linq;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Demos.Basic {

    public class CraftingBenchWindow : WindowView {

        public CraftingBenchWindow(CraftingBench bag, Basic_Store store) {

            Init(bag.Name, () => store.Bus.Publish(new CloseWindow(bag)));

            var vta = Resources.Load<VisualTreeAsset>("CraftingBenchView");
            Container.Add(vta.Instantiate());
            Container.Add(RecipesListView(bag.Recipes));

            var OutcomeSlotView = this.Q<SlotView>("OutcomeSlot");
            var ListBagView = this.Q<ListBagView>("ListBagView");

            ListBagView.Init(bag);
            OutcomeSlotView.Init(bag, bag.OutcomeSlot.Value);
            this.Observe(bag.OutcomeSlot, _ => OutcomeSlotView.Render());
        }

        VisualElement RecipesListView(List<Recipe> recipes) => Dom.Div(recipes.Where(x => x != null).Select(RecipePreview).ToArray());

        VisualElement RecipePreview(Recipe recipe) => Dom.Div("row mt-10",
            Dom.Div("row gap-h-5", recipe.Ingredients.Where(i => i != null).Select(b => Dom.Image("default-border", b.Icon)).ToArray()),
            new Label("￫") { style = { height = 32, fontSize = 24 } },
            Dom.Image("default-border ml-5", recipe.Outcome.Icon)
        );

    }

}