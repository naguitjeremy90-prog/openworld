using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class CustomTooltip_Controller : MonoBehaviour {
        [Required]
        public Store store;
        [Required]
        public VisualTreeAsset TooltipViewAsset;
        [Space(12)]
        public ListBag listBag = new() { Size = 20 };

        // This is not required but can be used to preview the UI with actual data in Game panel.
        void OnValidate() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.Q<ListBagView>().Init(listBag);
        }

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            // TooltipManipulator accepts a custom TooltipView parameter
            root.AddManipulator(new TooltipManipulator(new CustomTooltipView(TooltipViewAsset)));

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBag);
        }

    }

}