using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class JournalTabHover : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Tab Movement")]
    [SerializeField] private RectTransform tab;
    [SerializeField] private float raisedYOffset = 35f;
    [SerializeField] private float speed = 8f;

    private Vector2 normalPosition;
    private Vector2 raisedPosition;

    private Coroutine moveRoutine;
    private bool isSelected = false;

    private static JournalTabHover currentlySelected;

    private void Awake()
    {
        if (tab == null)
            tab = GetComponent<RectTransform>();

        normalPosition = tab.anchoredPosition;
        raisedPosition = normalPosition + new Vector2(0f, raisedYOffset);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Temporarily raise the tab when hovered.
        MoveTo(raisedPosition);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Selected tab stays raised.
        // Unselected tabs return to their normal position.
        if (!isSelected)
            MoveTo(normalPosition);
    }

    public void SelectTab()
    {
        // Lower the previously selected tab.
        if (currentlySelected != null && currentlySelected != this)
        {
            currentlySelected.isSelected = false;
            currentlySelected.MoveTo(currentlySelected.normalPosition);
        }

        // Make this the selected tab.
        currentlySelected = this;
        isSelected = true;

        MoveTo(raisedPosition);
    }

    private void MoveTo(Vector2 target)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveTab(target));
    }

    private IEnumerator MoveTab(Vector2 target)
    {
        while (Vector2.Distance(tab.anchoredPosition, target) > 0.1f)
        {
            tab.anchoredPosition = Vector2.Lerp(
                tab.anchoredPosition,
                target,
                Time.unscaledDeltaTime * speed
            );

            yield return null;
        }

        tab.anchoredPosition = target;
        moveRoutine = null;
    }
}
