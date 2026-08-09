using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

    [RequireComponent(typeof(UIDocument))]
    public class SOBag_Controller : MonoBehaviour {
        [Required]
        public Store store;
        [Space(12), InlineEditor]
        public ListBagSO listBagSO;

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBagSO.Value);
        }
    }

}