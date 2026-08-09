using System;
using UnityEngine;
using UnityEngine.UIElements;
using GDS.Core.Events;

namespace GDS.Core {
    public class DragDropManipulator : PointerManipulator {

        public int MinDragDistance = 32;

        protected EventBus bus;
        protected Observable<Item> ghost;
        protected VisualElement ghostView;

        Vector2 lastPos;
        IItemContext lastContext;
        Item lastContextItem;


        public DragDropManipulator(Store store, BaseItemView itemView = null) {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Control });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Command });
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse, modifiers = EventModifiers.Shift });

            bus = store.Bus;
            ghost = store.Ghost;

            itemView ??= new ItemView();
            ghostView = Dom.Div("ghost-item absolute", itemView).PickIgnore(true);
            ghostView.Observe(ghost, item => {
                itemView.Item = item;
                ghostView.SetVisible(item != null);
            });

        }

        protected override void RegisterCallbacksOnTarget() {
            target.Add(ghostView);
            target.RegisterCallback<PointerDownEvent>(TryGetValidDragTarget);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        protected override void UnregisterCallbacksFromTarget() {
            target.Remove(ghostView);
            target.UnregisterCallback<PointerDownEvent>(TryGetValidDragTarget);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        void TryGetValidDragTarget(PointerDownEvent e) {
            if (!CanStartManipulation(e)) return;
            if (ghost.Value != null) return;

            IItemContext context = (e.target as VisualElement).GetFirstOfType<IItemContext>();

            if (context == null) return;
            if (context.Item == null) return;

            lastPos = e.position;
            lastContext = context;
            lastContextItem = context.Item;
        }

        void OnPointerUp(PointerUpEvent e) {
            if (!CanStartManipulation(e)) return;
            // TODO: add a temporal threshold between picking and placing items (compare mouse down and up times)
            //       to fix a rare case when you click on the edge of one cell and release immediately in another cell
            //       resulting in item moving instead of being picked
            lastContext = null;

            var context = (e.target as VisualElement).GetFirstOfType<IItemContext>();
            if (context == null) return;

            if (ghost.Value == null && context.Item != null) {
                // Debug.Log($"should pick up item {context.Item.Name} from bag {context.Bag.name}");
                bus.Publish(new PickItem(context.Bag, context.Slot, context.Item, e));
                return;
            }
            if (ghost.Value != null && context.Slot != null) {
                // Debug.Log($"should place item {ghost.Value.Name} into bag {context.Bag.Name} at slot {context.Slot}");
                bus.Publish(new PlaceItem(context.Bag, context.Slot, ghost.Value, e));
                return;
            }
        }

        void OnPointerMove(PointerMoveEvent e) {
            ghostView.style.left = e.localPosition.x;
            ghostView.style.top = e.localPosition.y;

            if (ghost.Value != null) return;
            if (lastContext == null) return;
            if (Math.Abs(lastPos.x - e.position.x) < MinDragDistance && Math.Abs(lastPos.y - e.position.y) < MinDragDistance) return;

            bus.Publish(new PickItem(lastContext.Bag, lastContext.Slot, lastContextItem, e));
            lastContext = null;
        }

    }
}