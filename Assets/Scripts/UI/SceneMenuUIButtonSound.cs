using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class SceneMenuUIButtonSound : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{

    [SerializeField] private SceneMenuUISoundPlayer soundPlayer;
    [SerializeField] private bool persistClickThroughSceneLoad;
    [SerializeField] private bool selectWhenEnabled;
    [SerializeField] private bool suppressInitialSelectionSound;


    private Button button;
    private bool pointerInside;
    private bool selected;
    private bool suppressNextSelectionSound;
    private int lastHoverFrame = -1;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            // Keep this listener single even if OnEnable is called again after a panel toggle.
            button.onClick.RemoveListener(HandleButtonActivated);
            button.onClick.AddListener(HandleButtonActivated);
        }

        pointerInside = false;
        selected = false;
        suppressNextSelectionSound = suppressInitialSelectionSound;
        lastHoverFrame = -1;


        if (!selectWhenEnabled || button == null || !button.IsInteractable())
            return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        GameObject current = eventSystem.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy)
            return;

        // Settings opens with Back focused so keyboard/controller navigation can continue
        // through the existing automatic navigation graph. This programmatic focus is silent.
        suppressNextSelectionSound = true;
        eventSystem.SetSelectedGameObject(button.gameObject);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleButtonActivated);

        pointerInside = false;
        selected = false;
        suppressNextSelectionSound = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanInteract())
            return;

        pointerInside = true;
        PlayHoverUnlessDuplicate();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (selected)
            return;

        selected = true;

        if (suppressNextSelectionSound)
        {
            suppressNextSelectionSound = false;
            return;
        }

        // A pointer-enter already provides feedback for the same hovered button.
        if (!CanInteract() || pointerInside)
            return;

        PlayHoverUnlessDuplicate();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
    }

    private void HandleButtonActivated()
    {
        // Button only raises onClick after a valid activation. The menu's earlier callback may
        // hide this button before this listener runs, so rechecking its active state here would
        // incorrectly silence repeated activations after a panel is reopened.
        SceneMenuUISoundPlayer player = ResolvePlayer();
        if (player != null)
            player.PlayClick(persistClickThroughSceneLoad);
    }

    private void PlayHoverUnlessDuplicate()
    {
        // Pointer and selection events for one interaction can arrive in the same frame.
        if (Time.frameCount == lastHoverFrame)
            return;

        SceneMenuUISoundPlayer player = ResolvePlayer();
        if (player == null)
            return;

        lastHoverFrame = Time.frameCount;
        player.PlayHover();
    }

    private bool CanInteract()
    {
        return isActiveAndEnabled && button != null && button.isActiveAndEnabled &&
               button.IsInteractable();
    }

    private SceneMenuUISoundPlayer ResolvePlayer()
    {
        return soundPlayer != null ? soundPlayer : SceneMenuUISoundPlayer.Instance;
    }
}