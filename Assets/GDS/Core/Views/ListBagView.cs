using System;
using System.Linq;
using UnityEngine.UIElements;

namespace GDS.Core {

#if UNITY_6000_0_OR_NEWER
    [UxmlElement]    
#endif

    public partial class ListBagView : VisualElement {

#if UNITY_6000_0_OR_NEWER
        [UxmlAttribute] public string Title { get => title; set => SetTitle(value); }
        [UxmlAttribute] public int ItemsPerRow { get => itemsPerRow; set => SetItemsPerRow(value); }
        [UxmlAttribute] public int PreviewSlots { get => previewSlots; set => SetPreviewSlots(value); }
        [UxmlAttribute] public bool ShowPreviewItems { get => showPreviewItems; set => SetShowPreviewItems(value); }
#endif

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<ListBagView, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits {
            UxmlStringAttributeDescription Title = new() { name = "title", defaultValue = DefaultTitle };
            UxmlIntAttributeDescription ItemsPerRow = new() { name = "items-per-row", defaultValue = DefaultItemsPerRow };
            UxmlIntAttributeDescription PreviewSlots = new() { name = "preview-slots", defaultValue = DefaultPreviewSlots };
            UxmlBoolAttributeDescription ShowPreviewItems = new() { name = "show-preview-items", defaultValue = DefaultShowPreviewItems };
            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
                base.Init(ve, bag, cc);
                ((ListBagView)ve).SetTitle(Title.GetValueFromBag(bag, cc));
                ((ListBagView)ve).SetShowPreviewItems(ShowPreviewItems.GetValueFromBag(bag, cc));
                ((ListBagView)ve).SetItemsPerRow(ItemsPerRow.GetValueFromBag(bag, cc));
                ((ListBagView)ve).SetPreviewSlots(PreviewSlots.GetValueFromBag(bag, cc));
            }
        }

        public string Title { get => title; set => SetTitle(value); }
        public int ItemsPerRow { get => itemsPerRow; set => SetItemsPerRow(value); }
        public int PreviewSlots { get => previewSlots; set => SetPreviewSlots(value); }
        public bool ShowPreviewItems { get => showPreviewItems; set => SetShowPreviewItems(value); }
#endif

        public const string DefaultTitle = "";
        public const int DefaultItemsPerRow = 5;
        public const int DefaultPreviewSlots = 20;
        public const bool DefaultShowPreviewItems = true;

        string title = DefaultTitle;
        int itemsPerRow = DefaultItemsPerRow;
        int previewSlots = DefaultPreviewSlots;
        bool showPreviewItems = DefaultShowPreviewItems;

        Label titleLabel = Dom.Label("bag-title", "").WithName("Title").Hide();
        VisualElement slotContainer = Dom.Div("slot-container").WithName("SlotContainer");
        SlotView[] slotViews;

        public ListBagView() {
            this.Add("list-bag",
                titleLabel,
                slotContainer
            );

            // Workaround for Unity 6 reattaching the document when selecting UI Document in hierarchy
            RegisterCallback<DetachFromPanelEvent>(_ => UnregisterEvents());
            RegisterCallback<AttachToPanelEvent>(_ => RegisterEvents());
            Render();
        }

        Func<SlotView> CreateSlotView = () => new SlotView();
        ListBag bag;


        public void SetBagValue(ListBag value) {
            UnregisterEvents();
            bag = value;

            Build();
            Render();
            RegisterEvents();
        }

        public void SetTitle(string value) {
            title = value;
            titleLabel.text = value;
            titleLabel.SetVisible(!string.IsNullOrEmpty(titleLabel.text));
        }

        public void SetShowPreviewItems(bool value) {
            showPreviewItems = value;
            Render();
        }

        public void SetItemsPerRow(int value) {
            itemsPerRow = Math.Clamp(value, 0, 100);
            Render();
        }

        public void SetPreviewSlots(int value) {
            previewSlots = Math.Clamp(value, 1, 20);
            Render();
        }

        public ListBagView Init(ListBag bag) => Init(bag, itemsPerRow);
        public ListBagView Init(ListBag bag, int itemsPerRow, string title = "", Func<SlotView> createSlotViewFn = null) {
            this.itemsPerRow = itemsPerRow;
            CreateSlotView = createSlotViewFn ?? CreateSlotView;
            if (title != "") SetTitle(title);
            SetBagValue(bag);
            return this;
        }

        void Build() {
            slotViews = bag.Slots.Select(s => CreateSlotView().Init(bag, s)).ToArray();
        }

        void Render() {
            if (bag == null) { RenderPreview(); return; }

            slotContainer.Clear();
            if (itemsPerRow <= 0)
                slotContainer.Add(Dom.Div("slot-container-row").Add(slotViews));
            else
                slotContainer.Add(slotViews.Batch(itemsPerRow).Select(batch => Dom.Div("slot-container-row").Add(batch.ToArray())).ToArray());
        }

        void RenderPreview() {
            var views = Enumerable.Range(0, previewSlots).Select(PreviewSlot).ToArray();
            slotContainer.Clear();
            if (itemsPerRow <= 0)
                slotContainer.Add(Dom.Div("slot-container-row").Add(views));
            else
                slotContainer.Add(views.Batch(itemsPerRow).Select(batch => Dom.Div("slot-container-row").Add(batch.ToArray())).ToArray());
        }

        void RegisterEvents() {
            if (bag == null) return;
            UnregisterEvents();
            bag.ItemChanged += OnItemChanged;
            bag.CollectionReset += OnCollectionReset;
        }

        void UnregisterEvents() {
            if (bag == null) return;
            bag.ItemChanged -= OnItemChanged;
            bag.CollectionReset -= OnCollectionReset;
        }

        void OnItemChanged(ListSlot slot) {
            slotViews[slot.Index].Render();
        }

        void OnCollectionReset() {
            Build();
            Render();
        }

        VisualElement PreviewSlot(int index) => Dom.Div("slot",
            Dom.Div("preview-image").SetVisible(showPreviewItems),
            Dom.Label("index-label", (index + 1).ToString())
        );

    }

}