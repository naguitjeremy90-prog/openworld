using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {
    [RequireComponent(typeof(UIDocument))]
    public class AddSounds_Controller : MonoBehaviour {
        [Required]
        public Store store;
        [Space(12)]
        public ListBag listBag = new() { Size = 20 };

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));

            var listBagView = root.Q<ListBagView>();
            listBagView.Init(listBag);

            var backdrop = root.Q<VisualElement>("Backdrop");
            backdrop.RegisterCallback<PointerUpEvent>(_ => store.Bus.Publish(Result.Fail));
        }
    }

}