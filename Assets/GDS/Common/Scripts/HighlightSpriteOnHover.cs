using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Common.Scripts {

    /// <summary>
    /// Highlights an object on mouse over (changes object material).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class HighlightSpriteOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        SpriteRenderer Renderer;
        Color InitialColor;
        [SerializeField] Color Color = new Color(1, 1, 0, 0.75f);

        void Awake() {
            Renderer = GetComponent<SpriteRenderer>();
            InitialColor = Renderer.color;
        }

        public void OnPointerEnter(PointerEventData eventData) => Renderer.color = Color;
        public void OnPointerExit(PointerEventData eventData) => Renderer.color = InitialColor;
    }
}