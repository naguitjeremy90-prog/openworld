using UnityEngine;
using GDS.Core;
using UnityEngine.EventSystems;
using System;


namespace GDS.Common.Scripts {


    /// <summary>
    /// A world item is created when you discard items from an inventory (behavior handled in another script). 
    /// It can be picked up by clicking on it.
    /// </summary>
    public class WorldItem : MonoBehaviour, IWorldItem, IPointerDownHandler/*, IPointerEnterHandler, IPointerExitHandler*/ {

        GameObject itemInstance;

        Item item;
        public Item Item { get => item; set => item = value; }

        public GameObject GameObject => gameObject;

        public Action<IWorldItem> OnClick = (_) => { };

        public void OnPointerDown(PointerEventData eventData) => OnClick.Invoke(this);

        public void AddItemPrefab(GameObject prefab) {
            itemInstance = Instantiate(prefab, transform);

            itemInstance.AddComponent<ScaleObjectOnHover>();

            var renderer = itemInstance.GetComponent<Renderer>();
            if (renderer is SpriteRenderer s) {
                itemInstance.AddComponent<HighlightSpriteOnHover>();
                s.sprite = item.Icon;
            } else {
                itemInstance.AddComponent<HighlightObjectOnHover>();
            }
        }
    }
}