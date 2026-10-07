using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored tutorial visuals and layout defaults; contains no tutorial state.</summary>
[DisallowMultipleComponent]
public sealed class GameplaySystemTutorialVisual : MonoBehaviour
{
    [Header("Overlay")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private CanvasGroup presentationGroup;

    [Header("System Callout")]
    [SerializeField] private RectTransform initialCallout;
    [SerializeField] private TMP_Text initialTitle;
    [SerializeField] private TMP_Text initialBody;
    [SerializeField] private TMP_Text initialPointer;

    [Header("Shared Journal / Inventory Guided Callout")]
    [SerializeField] private RectTransform guidedCallout;
    [SerializeField] private TMP_Text guidedTitle;
    [SerializeField] private TMP_Text guidedBody;
    [SerializeField] private TMP_Text guidedPointer;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image nextImage;
    [SerializeField] private TMP_Text nextLabel;

    [Header("Target-relative Placement (Canvas Units)")]
    [Header("JOURNAL")]
    [InspectorName("Journal Initial Target Offset")]
    [Tooltip("Position adjustment for the initial Journal HUD tutorial and Journal closed-window fallback, in Canvas units. +X = Right, -X = Left, +Y = Up, -Y = Down. Screen clamping may limit movement.")]
    [SerializeField] private Vector2 initialTargetOffset = new Vector2(0f, -100f);
    [InspectorName("Journal Guided Offset")]
    [Tooltip("Guided Journal steps 2-5: offset from the live tab target, in Canvas units. +X = Right, -X = Left, +Y = Up, -Y = Down. Screen clamping may limit movement.")]
    [SerializeField] private Vector2 journalGuidedOffset = new Vector2(0f, -105f);
    [InspectorName("Journal Overview Offset")]
    [Tooltip("Journal overview (step 1): offset from the live window target, in Canvas units. +X = Right, -X = Left, +Y = Up, -Y = Down. Screen clamping may limit movement.")]
    [SerializeField] private Vector2 journalOverviewOffset = new Vector2(0f, -180f);

    [Header("INVENTORY")]
    [InspectorName("Inventory Initial Target Offset")]
    [Tooltip("Position adjustment for the initial Inventory HUD tutorial, second HUD prompt (step 2), and closed-window fallback, in Canvas units. +X = Right, -X = Left, +Y = Up, -Y = Down. Screen clamping may limit movement.")]
    [SerializeField] private Vector2 inventoryInitialTargetOffset = new Vector2(0f, -200f);
    [InspectorName("Inventory General Offset")]
    [Tooltip("Later Inventory explanations (steps 3-4): offset from the live HUD target, in Canvas units. Does not control initial HUD prompts or item-specific placement. +X = Right, -X = Left, +Y = Up, -Y = Down. Screen clamping may limit movement.")]
    [SerializeField] private Vector2 inventoryTutorialOffset = new Vector2(0f, -180f);
    [InspectorName("Inventory Item Details Offset")]
    [Tooltip("Additional X/Y displacement for the 'Mga Detalye ng Gamit' item-relative tutorial (step 1) after its automatic above/below position is calculated, in Canvas units. +X = Right, -X = Left, +Y = Up, -Y = Down. Inventory edge clamping may limit movement.")]
    [SerializeField] private Vector2 inventoryItemDetailsOffset = Vector2.zero;

    [Header("PAGSUSURI")]
    [InspectorName("Pagsusuri Initial Target Offset")]
    [Tooltip("Position adjustment for ordinary Pagsusuri (Clarity step 0) from the live HUD target, in Canvas units. +X = Right, -X = Left, +Y = Up, -Y = Down. Screen clamping may limit movement.")]
    [SerializeField] private Vector2 pagsusuriTutorialOffset = new Vector2(0f, -200f);
    [InspectorName("Pagsusuri Document Offset")]
    [Tooltip("Position adjustment for the 'Pagsusuri sa mga Kasulatan' instruction (Clarity step 1) from the live HUD target, in Canvas units. +X = Right, -X = Left, +Y = Up, -Y = Down. Screen clamping may limit movement.")]
    [SerializeField] private Vector2 pagsusuriDocumentOffset = new Vector2(0f, -200f);

    [Header("Screen Clamping")]
    [SerializeField, Min(0f)] private float screenClampMargin = 12f;

    [Header("Inventory Item Placement (Canvas Units)")]
    [SerializeField, Min(0f)] private float inventoryItemGap = 12f;
    [SerializeField, Min(0f)] private float inventoryEdgeMargin = 12f;
    [Tooltip("Pointer offset from the panel's top edge when the panel is below the item.")]
    [SerializeField] private Vector2 pointerBelowItemOffset = new Vector2(0f, 4f);
    [Tooltip("Pointer offset from the panel's bottom edge when the panel is above the item.")]
    [SerializeField] private Vector2 pointerAboveItemOffset = new Vector2(0f, -4f);

    public Canvas RootCanvas => rootCanvas;
    public CanvasGroup PresentationGroup => presentationGroup;
    public RectTransform InitialCallout => initialCallout;
    public TMP_Text InitialTitle => initialTitle;
    public TMP_Text InitialBody => initialBody;
    public TMP_Text InitialPointer => initialPointer;
    public RectTransform GuidedCallout => guidedCallout;
    public TMP_Text GuidedTitle => guidedTitle;
    public TMP_Text GuidedBody => guidedBody;
    public TMP_Text GuidedPointer => guidedPointer;
    public Button NextButton => nextButton;
    public Image NextImage => nextImage;
    public TMP_Text NextLabel => nextLabel;
    public Vector2 JournalInitialTargetOffset => initialTargetOffset;
    public Vector2 JournalGuidedOffset => journalGuidedOffset;
    public Vector2 JournalOverviewOffset => journalOverviewOffset;
    public Vector2 InventoryInitialTargetOffset => inventoryInitialTargetOffset;
    public Vector2 InventoryGeneralOffset => inventoryTutorialOffset;
    public Vector2 InventoryItemDetailsOffset => inventoryItemDetailsOffset;
    public Vector2 PagsusuriInitialTargetOffset => pagsusuriTutorialOffset;
    public Vector2 PagsusuriDocumentOffset => pagsusuriDocumentOffset;
    public float ScreenClampMargin => screenClampMargin;
    public float InventoryItemGap => inventoryItemGap;
    public float InventoryEdgeMargin => inventoryEdgeMargin;
    public Vector2 PointerBelowItemOffset => pointerBelowItemOffset;
    public Vector2 PointerAboveItemOffset => pointerAboveItemOffset;

    public bool ValidateReferences(out string error)
    {
        if (rootCanvas == null || presentationGroup == null || initialCallout == null ||
            initialTitle == null || initialBody == null || initialPointer == null ||
            guidedCallout == null || guidedTitle == null || guidedBody == null ||
            guidedPointer == null || nextButton == null || nextImage == null || nextLabel == null)
        {
            error = "A required tutorial visual reference is unassigned.";
            return false;
        }

        if (rootCanvas.gameObject != gameObject || presentationGroup.gameObject != gameObject ||
            !(transform is RectTransform) || GetComponent<CanvasScaler>() == null ||
            GetComponent<GraphicRaycaster>() == null || initialCallout == guidedCallout ||
            initialCallout.parent != transform || guidedCallout.parent != transform ||
            initialTitle.transform.parent != initialCallout || initialBody.transform.parent != initialCallout ||
            initialPointer.transform.parent != initialCallout || guidedTitle.transform.parent != guidedCallout ||
            guidedBody.transform.parent != guidedCallout || guidedPointer.transform.parent != guidedCallout ||
            nextButton.transform.parent != guidedCallout || nextImage.gameObject != nextButton.gameObject ||
            nextLabel.transform.parent != nextButton.transform)
        {
            error = "Tutorial visual references do not match the required overlay/callout hierarchy.";
            return false;
        }

        error = null;
        return true;
    }
}
