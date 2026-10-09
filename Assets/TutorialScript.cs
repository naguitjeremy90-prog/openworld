using UnityEngine;
using System.Collections;
using DialogueEditor;

public class TutorialStartDelay : MonoBehaviour
{
    public event System.Action TutorialCompleted;

    public bool IsCompletedAndHidden => completionReady &&
        SessionStoryState.GetFlag(CompletionFlag) && moveTextGroup != null &&
        moveTextGroup.alpha <= 0f && !moveTextGroup.gameObject.activeSelf;

    private bool completionReady;
    private bool completionNotificationCancelled;

    public CanvasGroup moveTextGroup;
    public Transform player;
    public float delay = 5f;
    public float fadeSpeed = 2f;

    private const string CompletionFlag = "draftworld_movement_tutorial_complete";
    private const float MovementThreshold = 0.15f;

    private void Awake()
    {
        HidePrompt();
        completionReady = SessionStoryState.GetFlag(CompletionFlag);
        if (moveTextGroup != null)
        {
            moveTextGroup.interactable = false;
            moveTextGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator Start()
    {
        if (moveTextGroup == null || player == null || SessionStoryState.GetFlag(CompletionFlag))
            yield break;

        // Let scene startup establish its story/dialogue ownership first.
        yield return null;

        float safeElapsed = 0f;
        while (!IsGameplaySafe() || safeElapsed < Mathf.Max(0f, delay))
        {
            safeElapsed = IsGameplaySafe() ? safeElapsed + Time.deltaTime : 0f;
            yield return null;
        }

        Vector3 startPosition = player.position;
        moveTextGroup.gameObject.SetActive(true);
        bool waitingForSafety = false;
        bool movementCompleted = false;

        while (player != null)
        {
            if (!IsGameplaySafe())
            {
                HidePrompt();
                waitingForSafety = true;
                yield return null;
                continue;
            }

            if (waitingForSafety)
            {
                // Movement during a story/dialogue must not finish onboarding.
                startPosition = player.position;
                moveTextGroup.gameObject.SetActive(true);
                waitingForSafety = false;
            }

            Vector3 displacement = player.position - startPosition;
            displacement.y = 0f;
            if (displacement.sqrMagnitude >= MovementThreshold * MovementThreshold)
            {
                movementCompleted = true;
                SessionStoryState.SetFlag(CompletionFlag, true);
                break;
            }

            moveTextGroup.alpha = Mathf.MoveTowards(moveTextGroup.alpha, 1f,
                Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
            yield return null;
        }

        bool fadeOutCompleted = moveTextGroup.alpha <= 0f;
        while (moveTextGroup.alpha > 0f)
        {
            if (!IsGameplaySafe())
                break;

            moveTextGroup.alpha = Mathf.MoveTowards(moveTextGroup.alpha, 0f,
                Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
            fadeOutCompleted = moveTextGroup.alpha <= 0f;
            yield return null;
        }

        HidePrompt();
        if (movementCompleted && fadeOutCompleted && IsGameplaySafe() &&
            isActiveAndEnabled && !completionNotificationCancelled)
        {
            completionReady = true;
            NotifyTutorialCompleted();
        }
    }

    private void OnDisable()
    {
        // Disabling a MonoBehaviour need not stop its coroutine; never report that run as completed.
        completionNotificationCancelled = true;
    }

    private void NotifyTutorialCompleted()
    {
        var listeners = TutorialCompleted;
        if (listeners == null)
            return;
        foreach (System.Action listener in listeners.GetInvocationList())
        {
            try { listener(); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
        }
    }

    private static bool IsGameplaySafe()
    {
        return !StorySequenceCoordinator.IsStorySequenceActive &&
               (ConversationManager.Instance == null || !ConversationManager.Instance.IsConversationActive);
    }

    private void HidePrompt()
    {
        if (moveTextGroup == null)
            return;

        moveTextGroup.alpha = 0f;
        moveTextGroup.gameObject.SetActive(false);
    }
}
