using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Demos.Basic {

    public class OpenChestOnClick : MonoBehaviour, IPointerClickHandler {

        [Required]
        public Store Store;

        [Space(12)]
        public Chest Bag;

        public void OnPointerClick(PointerEventData eventData) {
            Store.Bus.Publish(new OpenWindow(Bag));
        }
    }

}