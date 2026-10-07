using DialogueEditor;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Speeds up only the current Grasshop text reveal after a double-click on its text.</summary>
public sealed class DialogueTextAcceleration : MonoBehaviour, IPointerClickHandler
{
    private const float SpeedMultiplier = 4f;

    private ConversationManager manager;
    private TextMeshProUGUI dialogueText;
    private int previousVisibleCount = -1;
    private int lineVersion;
    private int firstClickLineVersion = -1;
    private float baseScrollSpeed;
    private bool accelerated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterForConversations()
    {
        ConversationManager.OnConversationStarted -= AttachToCurrentConversation;
        ConversationManager.OnConversationStarted += AttachToCurrentConversation;
    }

    private static void AttachToCurrentConversation()
    {
        ConversationManager current = ConversationManager.Instance;
        if (current == null || current.DialogueText == null)
            return;

        GameObject textObject = current.DialogueText.gameObject;
        DialogueTextAcceleration bridge = textObject.GetComponent<DialogueTextAcceleration>();
        if (bridge == null)
            textObject.AddComponent<DialogueTextAcceleration>();
        else
        {
            bridge.RestoreSpeed();
            bridge.ResetObservation();
        }
    }

    private void Awake()
    {
        dialogueText = GetComponent<TextMeshProUGUI>();
        manager = GetComponentInParent<ConversationManager>(true);
    }

    private void OnEnable()
    {
        ConversationManager.OnConversationEnded += HandleConversationEnded;
        ResetObservation();
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationEnded -= HandleConversationEnded;
        RestoreSpeed();
        ResetObservation();
    }

    private void OnDestroy()
    {
        RestoreSpeed();
    }

    private void LateUpdate()
    {
        ObserveReveal();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        ObserveReveal();
        if (!IsTextRevealing())
        {
            firstClickLineVersion = -1;
            return;
        }

        if (eventData.clickCount == 1)
        {
            firstClickLineVersion = lineVersion;
            return;
        }

        if (eventData.clickCount != 2 || firstClickLineVersion != lineVersion || accelerated)
            return;

        baseScrollSpeed = manager.ScrollSpeed;
        if (baseScrollSpeed <= 0f || float.IsNaN(baseScrollSpeed) || float.IsInfinity(baseScrollSpeed))
            return;

        manager.ScrollSpeed = baseScrollSpeed / SpeedMultiplier;
        accelerated = true;
        firstClickLineVersion = -1;
    }

    private void ObserveReveal()
    {
        if (manager == null || dialogueText == null)
        {
            RestoreSpeed();
            return;
        }

        int visibleCount = dialogueText.maxVisibleCharacters;
        // SetupSpeech resets this count even when consecutive lines have identical text.
        if (previousVisibleCount >= 0 && visibleCount < previousVisibleCount)
        {
            RestoreSpeed();
            lineVersion++;
            firstClickLineVersion = -1;
        }

        previousVisibleCount = visibleCount;
        if (accelerated && !IsTextRevealing())
            RestoreSpeed();
    }

    private bool IsTextRevealing()
    {
        if (manager == null || dialogueText == null ||
            ConversationManager.Instance != manager || !manager.IsConversationActive ||
            !manager.ScrollText || !dialogueText.gameObject.activeInHierarchy ||
            string.IsNullOrEmpty(dialogueText.text))
            return false;

        int visibleCount = dialogueText.maxVisibleCharacters;
        int targetCount = manager.m_targetScrollTextCount - 1;
        int glyphCount = dialogueText.textInfo.characterCount;

        // TMP excludes rich-text tags and text clipped by the dialogue box.
        // Once its last renderable glyph is visible, acceleration has no effect.
        return glyphCount > 0 && visibleCount >= 0 &&
               visibleCount < targetCount && visibleCount < glyphCount;
    }

    private void HandleConversationEnded()
    {
        if (ConversationManager.Instance != manager)
            return;

        RestoreSpeed();
        firstClickLineVersion = -1;
    }

    private void RestoreSpeed()
    {
        if (!accelerated)
            return;

        if (manager != null)
            manager.ScrollSpeed = baseScrollSpeed;
        accelerated = false;
    }

    private void ResetObservation()
    {
        previousVisibleCount = -1;
        firstClickLineVersion = -1;
        lineVersion = 0;
    }
}
