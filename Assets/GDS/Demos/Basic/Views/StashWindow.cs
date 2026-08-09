using System.Linq;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine.UIElements;

namespace GDS.Demos.Basic {

    public class StashWindow : WindowView {

        public StashWindow(Stash bag, Store store) {
            Init(bag.Name, () => store.Bus.Publish(new CloseWindow(bag)));

            void onTabChange(int i) => bag.CurrentIndex.SetValue(i);
            VisualElement createButton(string text, int index) => Dom.Button("tab-button", text, () => onTabChange(index));

            var TabContent = Dom.Div();
            var TabButtons = bag.Tabs.Select((b, index) => createButton($"{b.Name}({index})", index)).ToArray();
            var TabBar = Dom.Div("row gap-v-10").Add(TabButtons);

            bag.CurrentIndex.Reset();
            Container.Add(TabBar, TabContent);

            this.Observe(bag.CurrentIndex, i => {
                TabContent.Clear();
                var view = new ListBagView().Init(bag.Current, 10, "");
                TabContent.Add(view);
                foreach (var btn in TabButtons) btn.WithoutClass("selected");
                TabButtons[i].WithClass("selected");
            });
        }
    }

}