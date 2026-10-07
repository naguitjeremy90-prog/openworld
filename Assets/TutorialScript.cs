using UnityEngine;
using System.Collections;
using DialogueEditor;

public class TutorialStartDelay : MonoBehaviour
{
    public CanvasGroup moveTextGroup;
    public Transform player;
    public float delay = 5f;
    public float fadeSpeed = 2f;

    private const string CompletionFlag = "draftworld_movement_tutorial_complete";
    private const float MovementThreshold = 0.15f;

    private void Awake()
    {
        HidePrompt();
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
                SessionStoryState.SetFlag(CompletionFlag, true);
                break;
            }

            moveTextGroup.alpha = Mathf.MoveTowards(moveTextGroup.alpha, 1f,
                Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
            yield return null;
        }

        while (moveTextGroup.alpha > 0f)
        {
            if (!IsGameplaySafe())
                break;

            moveTextGroup.alpha = Mathf.MoveTowards(moveTextGroup.alpha, 0f,
                Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
            yield return null;
        }

        HidePrompt();
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
