using UnityEngine.UIElements;

namespace GDS.Core {

    public class BaseTooltipView : VisualElement {
        protected Label ItemName = new();
        virtual public void Render(IHoveredItemContext context) { }
    }

    public class TooltipView : BaseTooltipView {
        public TooltipView() {
            this.Add("tooltip", ItemName.WithClass("tooltip-item-name"));
        }

        override public void Render(IHoveredItemContext context) {
            ItemName.text = $"{context.Item.ItemNameWithQuant()}\nid: {context.Item.Id.Gray()}\ntype: " + context.Item.GetType().ToString().Gray();
        }
    }
}