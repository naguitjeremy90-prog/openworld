using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Common.Scripts {

    /// <summary>
    /// Scales an object on mouse over.
    /// </summary>
    public class ScaleObjectOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

        [SerializeField] float ScaleFactor = 1.1f;
        Vector3 initialScale;

        void Awake() {
            initialScale = transform.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData) => transform.localScale = initialScale * ScaleFactor;
        public void OnPointerExit(PointerEventData eventData) => transform.localScale = initialScale;
    }
}