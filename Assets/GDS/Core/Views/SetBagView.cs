using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace GDS.Core {

#if UNITY_6000_0_OR_NEWER
    [UxmlElement]    
#endif

    public partial class SetBagView : VisualElement {

#if UNITY_6000_0_OR_NEWER
        [UxmlAttribute] public string Title { get => title; set => SetTitle(value); }
        [UxmlAttribute] public int PreviewSlots { get => previewSlots; set => SetPreviewSlots(value); }
        [UxmlAttribute] public bool ShowPreviewItems { get => showPreviewItems; set => SetShowPreviewItems(value); }
#endif

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<SetBagView, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits {
            UxmlStringAttributeDescription Title = new() { name = "title", defaultValue = DefaultTitle };
            UxmlIntAttributeDescription PreviewSlots = new() { name = "preview-slots", defaultValue = DefaultPreviewSlots };
            UxmlBoolAttributeDescription ShowPreviewItems = new() { name = "show-preview-items", defaultValue = DefaultShowPreviewItems };
            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc) {
                base.Init(ve, bag, cc);
                ((SetBagView)ve).SetTitle(Title.GetValueFromBag(bag, cc));
                ((SetBagView)ve).SetPreviewSlots(PreviewSlots.GetValueFromBag(bag, cc));
                ((SetBagView)ve).SetShowPreviewItems(ShowPreviewItems.GetValueFromBag(bag, cc));
            }
        }

        public string Title { get => title; set => SetTitle(value); }
        public int PreviewSlots { get => previewSlots; set => SetPreviewSlots(value); }
        public bool ShowPreviewItems { get => showPreviewItems; set => SetShowPreviewItems(value); }
#endif

        public const string DefaultTitle = "";
        public const int DefaultPreviewSlots = 5;
        public const bool DefaultShowPreviewItems = true;

        string title = DefaultTitle;
        int previewSlots = DefaultPreviewSlots;
        bool showPreviewItems = DefaultShowPreviewItems;

        Label titleLabel = Dom.Label("bag-title", "").WithName("Title");
        VisualElement slotContainer = Dom.Div("slot-container").WithName("SlotContainer");
        SlotView[] slotViews;
        Dictionary<string, SlotView> slotViewsDict;

        Func<SlotView> CreateSlotView = () => new SlotView();
        SetBag bag;

        public SetBagView() {
            this.Add("set-bag", titleLabel, slotContainer);

            // Workaround for Unity 6 reattaching the document when selecting UI Document in hierarchy
            RegisterCallback<DetachFromPanelEvent>(_ => UnregisterEvents());
            RegisterCallback<AttachToPanelEvent>(_ => RegisterEvents());
            Render();
        }


        public void SetBagValue(SetBag value) {
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

        public void SetPreviewSlots(int value) {
            previewSlots = Math.Clamp(value, 1, 20);
            Render();
        }

        public SetBagView Init(SetBag bag, string title = "", Func<SlotView> createSlotViewFn = null) {
            CreateSlotView = createSlotViewFn ?? CreateSlotView;
            SetBagValue(bag);
            if (title != "") SetTitle(title);
            return this;
        }

        void Build() {
            slotViews = bag.Slots.Select(s => CreateSlotView().Init(bag, s).WithClass(s.Key)).ToArray();
            slotViewsDict = slotViews.ToDictionary(v => (v.Slot as SetSlot).Key);
        }

        void Render() {
            if (bag == null) { RenderPreview(); return; }
            slotContainer.Clear();
            slotContainer.Add(slotViews);
        }

        void RenderPreview() {
            var views = Enumerable.Range(0, previewSlots).Select(PreviewSlot).ToArray();
            slotContainer.Clear();
            slotContainer.Add(views);
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

        void OnItemChanged(SetSlot slot) {
            slotViewsDict[slot.Key].Render();
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