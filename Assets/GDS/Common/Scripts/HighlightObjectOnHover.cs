using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Common.Scripts {

    /// <summary>
    /// Highlights an object on mouse over (changes object material).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class HighlightObjectOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {
        Renderer Renderer;
        Color InitialColor;
        [SerializeField] Color Color = new Color(1, 0.75f, 0, 0.75f);

        void Awake() {
            Renderer = GetComponent<Renderer>();
            InitialColor = Renderer.material.color;
        }

        public void OnPointerEnter(PointerEventData eventData) => Renderer.material.color = Color;
        public void OnPointerExit(PointerEventData eventData) => Renderer.material.color = InitialColor;
    }
}