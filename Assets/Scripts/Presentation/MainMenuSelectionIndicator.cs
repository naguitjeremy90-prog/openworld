using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Shows a main-menu button's diamond while hovered or selected for navigation.</summary>
public sealed class MainMenuSelectionIndicator : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private GameObject indicator;

    private bool pointerInside;
    private bool selected;

    private void Awake()
    {
        if (indicator == null)
        {
            Transform child = transform.Find("SelectionDiamond");
            if (child != null)
                indicator = child.gameObject;
        }

        SetIndicator(false);
    }

    private void OnDisable()
    {
        pointerInside = false;
        selected = false;
        SetIndicator(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;

        // Mouse hover owns the indicator until keyboard/controller navigation resumes.
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
            eventSystem.SetSelectedGameObject(null, eventData);

        SetIndicator(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        SetIndicator(selected);
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        SetIndicator(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        SetIndicator(pointerInside);
    }

    private void SetIndicator(bool visible)
    {
        if (indicator != null && indicator.activeSelf != visible)
            indicator.SetActive(visible);
    }
}
