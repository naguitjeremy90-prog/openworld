using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies a non-destructive locked tint and input state to one HUD system.</summary>
[DisallowMultipleComponent]
public sealed class GameplaySystemLockView : MonoBehaviour
{
    [SerializeField] private GameplaySystemId system;
    [SerializeField] private Graphic[] graphics = new Graphic[0];
    [SerializeField] private Selectable[] interactionsToBlock = new Selectable[0];
    [SerializeField] private GameObject[] hiddenWhenLocked = new GameObject[0];
    [SerializeField] private Color lockedTint = new Color(0.14f, 0.14f, 0.14f, 1f);

    private Color[] originalColors;
    private bool[] originalInteractableStates;
    private bool[] originalActiveStates;
    private bool capturedOriginalState;

    public GameplaySystemId System => system;

    private void Awake()
    {
        CaptureOriginalState();
    }

    private void OnEnable()
    {
        CaptureOriginalState();
        GameplaySystemState.UnlockChanged += HandleUnlockChanged;
        Refresh();
    }

    private void OnDisable()
    {
        GameplaySystemState.UnlockChanged -= HandleUnlockChanged;
    }

    public void Refresh()
    {
        CaptureOriginalState();
        ApplyUnlockedState(GameplaySystemState.IsUnlocked(system));
    }

    private void HandleUnlockChanged(GameplaySystemId changedSystem, bool unlocked)
    {
        if (changedSystem == system)
            ApplyUnlockedState(unlocked);
    }

    private void CaptureOriginalState()
    {
        if (capturedOriginalState)
            return;

        if (graphics == null || graphics.Length == 0)
            graphics = GetComponentsInChildren<Graphic>(true);
        if (interactionsToBlock == null || interactionsToBlock.Length == 0)
            interactionsToBlock = GetComponentsInChildren<Selectable>(true);

        originalColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                originalColors[i] = graphics[i].color;
        }

        originalInteractableStates = new bool[interactionsToBlock.Length];
        for (int i = 0; i < interactionsToBlock.Length; i++)
        {
            if (interactionsToBlock[i] != null)
                originalInteractableStates[i] = interactionsToBlock[i].interactable;
        }

        originalActiveStates = new bool[hiddenWhenLocked.Length];
        for (int i = 0; i < hiddenWhenLocked.Length; i++)
        {
            if (hiddenWhenLocked[i] != null)
                originalActiveStates[i] = hiddenWhenLocked[i].activeSelf;
        }

        capturedOriginalState = true;
    }

    private void ApplyUnlockedState(bool unlocked)
    {
        if (!capturedOriginalState)
            return;

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            Color original = originalColors[i];
            graphic.color = unlocked ? original : GetLockedColor(original);
        }

        for (int i = 0; i < interactionsToBlock.Length; i++)
        {
            Selectable interaction = interactionsToBlock[i];
            if (interaction != null)
            {
                interaction.interactable =
                    unlocked && originalInteractableStates[i];
            }
        }

        for (int i = 0; i < hiddenWhenLocked.Length; i++)
        {
            GameObject hiddenObject = hiddenWhenLocked[i];
            if (hiddenObject != null)
                hiddenObject.SetActive(unlocked && originalActiveStates[i]);
        }
    }

    private Color GetLockedColor(Color original)
    {
        // Use one neutral RGB tint for every locked system icon. The source
        // graphic's alpha is retained so its authored shape/transparency is
        // unchanged, while its original hue and brightness cannot bleed through.
        return new Color(lockedTint.r, lockedTint.g, lockedTint.b, original.a);
    }
}
