using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Demos.Basic {

    public class OpenShopOnClick : MonoBehaviour, IPointerClickHandler {

        [Required]
        public Basic_Store Store;

        [Space(12)]
        public Shop Bag = new();

        void Awake() {
            Bag.Init(Store.PlayerGold);
        }

        public void OnPointerClick(PointerEventData eventData) {
            Store.Bus.Publish(new OpenWindow(Bag));
        }
    }

}