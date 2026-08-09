using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Core {
#if UNITY_6000_0_OR_NEWER
    [UxmlElement]    
#endif
    public partial class WindowView : VisualElement {

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<WindowView> { }
#endif

        protected Label TitleLabel;
        protected VisualElement Container;
        protected Button CloseButton;

        public WindowView() {
            const string path = "WindowView";
            var uxml = Resources.Load<VisualTreeAsset>(path) ?? throw new InvalidOperationException($"'{path}' not found in a Resources folder.");
            uxml.CloneTree(this);
            TitleLabel = this.Q<Label>(nameof(TitleLabel));
            Container = this.Q<VisualElement>(nameof(Container));
            CloseButton = this.Q<Button>(nameof(CloseButton));
        }

        public WindowView Init(string title, Action onClose) {
            SetTitle(title);
            CloseButton.RegisterCallback<ClickEvent>(_ => onClose());
            return this;
        }

        protected void SetTitle(string value) {
            TitleLabel.text = value;
            TitleLabel.SetVisible(!string.IsNullOrWhiteSpace(value));
        }
    }

}