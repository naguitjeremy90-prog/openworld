using UnityEngine.UIElements;
using GDS.Core;

namespace GDS.Examples {
    public class DoubleClickManipulator : PointerManipulator {

        Store store;
        IItemContext lastContext;

        public DoubleClickManipulator(Store store) {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, clickCount = 2 });
            this.store = store;
        }

        protected override void RegisterCallbacksOnTarget() => target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        protected override void UnregisterCallbacksFromTarget() => target.UnregisterCallback<PointerDownEvent>(OnPointerDown);

        void OnPointerDown(PointerDownEvent e) {
            IItemContext context = (e.target as VisualElement).GetFirstOfType<IItemContext>();
            if (e.clickCount % 2 == 1) { lastContext = context; return; }

            if (!CanStartManipulation(e)) return;
            if (context?.Item == null) return;
            if (context != lastContext) return;

            lastContext = null;
            store.Bus.Publish(new CustomDoubleClickEvent(context.Bag, context.Slot, context.Item, e));
        }

    }
}