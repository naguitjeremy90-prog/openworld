using UnityEngine.UIElements;
using GDS.Core;

namespace GDS.Examples {
    public class RightClickManipulator : PointerManipulator {

        Store store;
        public RightClickManipulator(Store store) {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.RightMouse });
            this.store = store;
        }

        protected override void RegisterCallbacksOnTarget() => target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        protected override void UnregisterCallbacksFromTarget() => target.UnregisterCallback<PointerDownEvent>(OnPointerDown);

        void OnPointerDown(PointerDownEvent e) {
            if (!CanStartManipulation(e)) return;

            IItemContext context = (e.target as VisualElement).GetFirstOfType<IItemContext>();
            if (context?.Item == null) return;

            store.Bus.Publish(new CustomRightClickEvent(context.Bag, context.Slot, context.Item, e));
        }

    }
}