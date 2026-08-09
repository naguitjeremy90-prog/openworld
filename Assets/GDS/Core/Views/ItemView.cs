using UnityEngine.UIElements;

namespace GDS.Core {

    public abstract class BaseItemView : VisualElement {
        protected Image image = new Image().WithClass("item-image");
        protected Label quant = new Label().WithClass("item-quant");
        protected Item item;

        public virtual Item Item {
            get { return item; }
            set { item = value; Render(); }
        }

        public virtual void Render() { }
    }

    public class ItemView : BaseItemView {
        public ItemView() { this.Add("item-view", image, quant); }
        override public void Render() {
            if (item == null) { return; }

            image.sprite = item.Icon;
            quant.text = item.StackSize.ToString();
            quant.SetVisible(item.Stackable);
        }
    }
}