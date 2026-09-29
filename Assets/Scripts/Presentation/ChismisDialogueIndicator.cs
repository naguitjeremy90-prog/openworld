using DialogueEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows one story sign only while one of the three overheard dialogue panels is visible.</summary>
[DefaultExecutionOrder(10000)]
public sealed class ChismisDialogueIndicator : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField] private RectTransform indicatorRoot;
    [SerializeField] private RectTransform talkingVisual;
    [SerializeField] private CanvasGroup indicatorGroup;
    [SerializeField] private Image signImage;
    [SerializeField] private Canvas presentationCanvas;
    [SerializeField] private Camera mainCamera;

    [Header("Church dialogues")]
    [SerializeField] private OverheardConversationSequence firstDialogue;
    [SerializeField] private OverheardConversationSequence secondDialogue;
    [SerializeField] private OverheardConversationSequence thirdDialogue;
    [SerializeField] private Transform doorSection;

    [Header("Positions")]
    [Tooltip("X is the left edge inset of the visual; Y is its screen height position.")]
    [SerializeField] private Vector2 firstDialogueViewportPosition = new Vector2(0.025f, 0.62f);
    [SerializeField] private Vector2 doorScreenOffset = new Vector2(0f, 40f);

    [Header("Talking motion (Canvas units)")]
    [SerializeField, Min(0f)] private float pulseDistance = 8f;
    [SerializeField, Min(0.01f)] private float forwardDuration = 0.35f;
    [SerializeField, Min(0f)] private float forwardPause = 1f;
    [SerializeField, Min(0.01f)] private float returnDuration = 0.35f;
    [SerializeField, Min(0f)] private float restPause = 1f;

    private int activeDialogue;
    private float pulseTime;
    private Vector2 visualRestPosition;

    private void Awake()
    {
        if (talkingVisual != null)
            visualRestPosition = talkingVisual.anchoredPosition;
        Hide();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void LateUpdate()
    {
        ConversationManager manager = ConversationManager.Instance;
        int dialogue = CurrentOverheardDialogue();
        bool visible = dialogue != 0 && manager != null && manager.isActiveAndEnabled &&
                       manager.IsConversationActive && manager.DialoguePanel != null &&
                       manager.DialoguePanel.gameObject.activeInHierarchy &&
                       manager.DialogueBackground != null;

        float dialogueAlpha = visible ? manager.DialogueBackground.color.a : 0f;
        if (dialogueAlpha <= 0f || !TryPlace(dialogue))
        {
            Hide();
            return;
        }

        if (activeDialogue != dialogue)
        {
            activeDialogue = dialogue;
            pulseTime = 0f;
            ResetVisual();
        }

        indicatorGroup.alpha = dialogueAlpha;
        if (!signImage.gameObject.activeSelf)
            signImage.gameObject.SetActive(true);

        AdvanceTalkingMotion(Time.deltaTime);
    }

    private int CurrentOverheardDialogue()
    {
        if (firstDialogue != null && firstDialogue.IsOverheardPresentationPending) return 1;
        if (secondDialogue != null && secondDialogue.IsOverheardPresentationPending) return 2;
        if (thirdDialogue != null && thirdDialogue.IsOverheardPresentationPending) return 3;
        return 0;
    }

    private bool TryPlace(int dialogue)
    {
        if (indicatorRoot == null || presentationCanvas == null)
            return false;

        RectTransform canvasRect = presentationCanvas.transform as RectTransform;
        if (canvasRect == null)
            return false;

        Vector2 screenPoint;
        if (dialogue == 1)
        {
            screenPoint = new Vector2(Screen.width * firstDialogueViewportPosition.x,
                                      Screen.height * firstDialogueViewportPosition.y);
        }
        else
        {
            if (doorSection == null || mainCamera == null)
                return false;
            Vector3 projected = mainCamera.WorldToScreenPoint(doorSection.position);
            if (projected.z <= 0f)
                return false;
            screenPoint = new Vector2(projected.x, projected.y);
        }

        Camera uiCamera = presentationCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : presentationCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            return false;

        if (dialogue == 1)
        {
            indicatorRoot.anchorMin = indicatorRoot.anchorMax = new Vector2(0f, 0.5f);
            float halfVisualWidth = talkingVisual == null ? 0f :
                talkingVisual.rect.width * Mathf.Abs(talkingVisual.localScale.x) * 0.5f;
            indicatorRoot.anchoredPosition = new Vector2(
                localPoint.x - canvasRect.rect.xMin + halfVisualWidth, localPoint.y);
        }
        else
        {
            indicatorRoot.anchorMin = indicatorRoot.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRoot.anchoredPosition = localPoint + doorScreenOffset;
        }
        return true;
    }

    private void AdvanceTalkingMotion(float deltaTime)
    {
        if (talkingVisual == null)
            return;

        float cycle = forwardDuration + forwardPause + returnDuration + restPause;
        pulseTime = Mathf.Repeat(pulseTime + deltaTime, cycle);
        float amount;
        if (pulseTime < forwardDuration)
            amount = Smooth(pulseTime / forwardDuration);
        else if (pulseTime < forwardDuration + forwardPause)
            amount = 1f;
        else if (pulseTime < forwardDuration + forwardPause + returnDuration)
            amount = 1f - Smooth((pulseTime - forwardDuration - forwardPause) / returnDuration);
        else
            amount = 0f;

        talkingVisual.anchoredPosition = visualRestPosition + Vector2.right * (pulseDistance * amount);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void ResetVisual()
    {
        if (talkingVisual != null)
            talkingVisual.anchoredPosition = visualRestPosition;
    }

    private void Hide()
    {
        activeDialogue = 0;
        pulseTime = 0f;
        ResetVisual();
        if (indicatorGroup != null)
            indicatorGroup.alpha = 0f;
        if (signImage != null && signImage.gameObject.activeSelf)
            signImage.gameObject.SetActive(false);
    }
}
