using UnityEngine;
using UnityEngine.EventSystems;

public class ItemHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject descriptionText;

    void Start()
    {
        if (descriptionText != null)
            descriptionText.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (descriptionText != null)
            descriptionText.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (descriptionText != null)
            descriptionText.SetActive(false);
    }
}