using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace GDS.Core {
    public class TooltipManipulator : PointerManipulator {

        public int PollingInterval = 50;

        BaseTooltipView Tooltip;
        Vector2 lastTooltipPos = new(0, 0);

        IHoveredItemContext context;
        IVisualElementScheduledItem scheduleId;

        string lastItemId;
        int lastItemStackSize;

        public TooltipManipulator(BaseTooltipView tooltipView = null) {

            Tooltip = tooltipView ?? new TooltipView();
            Tooltip.style.position = Position.Absolute;
            Tooltip.PickIgnore(true);
            Tooltip.Hide();
            Tooltip.RegisterCallback<GeometryChangedEvent>(_ => TryRepositionTooltip());
        }

        protected override void RegisterCallbacksOnTarget() {
            target.Add(Tooltip);
            target.RegisterCallback<GeometryChangedEvent>(_ => TryToggle());

            scheduleId = target.schedule.Execute(OnTick);
            scheduleId.Every(PollingInterval);
            scheduleId.Pause();
        }

        protected override void UnregisterCallbacksFromTarget() {
            target.UnregisterCallback<GeometryChangedEvent>(_ => TryToggle());

        }

        void TryToggle() {
            if (target.resolvedStyle.display == DisplayStyle.None) scheduleId.Pause();
            else scheduleId.Resume();
        }

        void OnTick() {
            var screenPos = Pointer.current.position.ReadValue();
            screenPos.y = Screen.height - screenPos.y;
            var panelPos = RuntimePanelUtils.ScreenToPanel(target.panel, screenPos);

            context = target.panel.Pick(panelPos)?.GetFirstOfType<IHoveredItemContext>();

            if (context?.Item == null) {
                HideTooltip();
            } else {
                ShowTooltip();
            }
        }

        void ShowTooltip() {
            // TODO: replace this with proper hash based comparison
            if (context.Item.Id == lastItemId && context.Item.StackSize == lastItemStackSize) return;

            lastItemId = context.Item.Id;
            lastItemStackSize = context.Item.StackSize;

            Tooltip.Render(context);
            lastTooltipPos = DefaultPosition(context.WorldBound, Tooltip.worldBound, target.worldBound);
            PositionTooltip(lastTooltipPos);
            Tooltip.Show();
        }

        void HideTooltip() {
            if (lastItemId == null) return;
            lastItemId = null;
            lastItemStackSize = -1;
            Tooltip.Hide();
        }

        void PositionTooltip(Vector2 pos) {
            Tooltip.Translate((int)pos.x, (int)pos.y);
        }

        void TryRepositionTooltip() {
            if (Tooltip.worldBound.width == 0) return;

            Vector2 newPos = DefaultPosition(context.WorldBound, Tooltip.worldBound, target.worldBound);
            if (newPos == lastTooltipPos) return;

            PositionTooltip(newPos);
        }

        public static Vector2 DefaultPosition(Rect item, Rect tooltip, Rect root) {
            float left, top;
            top = item.yMin - tooltip.height;
            left = item.center.x - tooltip.width / 2;

            // TODO: turn into switch statement
            if (top < 0) {
                top = 0;
                left = item.xMin - tooltip.width;
                if (left < 0) {
                    left = item.xMax;
                }
            } else {
                if (left < 0) {
                    left = 0;
                }
                if (left + tooltip.width > root.width) {
                    left = root.width - tooltip.width;
                }
            }

            return new Vector2(left, top);
        }


    }
}