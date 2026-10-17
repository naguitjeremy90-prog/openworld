using System.Collections;
using DialogueEditor;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class TutorialStartDelay : MonoBehaviour
{
    public event System.Action TutorialCompleted;

    public bool IsCompletedAndHidden => completionReady &&
        SessionStoryState.GetFlag(CompletionFlag) && moveTextGroup != null &&
        moveTextGroup.alpha <= 0f && !moveTextGroup.gameObject.activeSelf;

    private const string CompletionFlag = "draftworld_movement_tutorial_complete";
    private const string RunCompletionFlag = "draftworld_run_tutorial_complete";
    private const string JumpCompletionFlag = "draftworld_jump_tutorial_complete";
    private const float MovementThreshold = 0.15f;
    [SerializeField] private TMP_Text prefixText;
    [SerializeField] private Image keyImage;
    [SerializeField] private TMP_Text suffixText;
    [SerializeField] private Sprite wasdSprite;
    [SerializeField] private Sprite runSprite;
    [SerializeField] private Sprite jumpSprite;
    [SerializeField] private float runImageHeight = 19f;
    [SerializeField] private float jumpImageHeight = 16f;

    private bool completionReady;
    private bool completionNotificationCancelled;
    private bool jumpStepActive;
    private bool spaceReleasedAfterReveal;
    private bool successfulJumpObserved;
    private bool observedRunStoppedAfterReveal;

    private LayoutElement keyLayout;
    private Supercyan.FreeSample.SimpleSampleCharacterControl playerController;
    private InventoryUI inventoryUI;
    private ReconstructionJournalManager journal;
    private AlaalaPauseMenuController pauseMenu;

    public CanvasGroup moveTextGroup;
    public Transform player;
    public float delay = 5f;
    public float fadeSpeed = 2f;

    private void Awake()
    {
        HidePrompt();
        completionReady = SessionStoryState.GetFlag(CompletionFlag);
        if (moveTextGroup != null)
        {
            moveTextGroup.interactable = false;
            moveTextGroup.blocksRaycasts = false;
            keyLayout = keyImage != null ? keyImage.GetComponent<LayoutElement>() : null;
        }

        if (player != null)
            playerController = player.GetComponent<Supercyan.FreeSample.SimpleSampleCharacterControl>();

        if (prefixText == null || keyImage == null || suffixText == null || keyLayout == null)
            Debug.LogError("MovementPrompt requires PrefixText, KeyImage with LayoutElement, and SuffixText references.", this);
        if (wasdSprite == null || runSprite == null || jumpSprite == null)
            Debug.LogError("MovementPrompt requires assigned WASD, run, and jump Sprites.", this);
        if (playerController == null)
            Debug.LogError("MovementPrompt could not find SimpleSampleCharacterControl on its player.", this);
    }

    private IEnumerator Start()
    {
        if (moveTextGroup == null || player == null || prefixText == null || keyImage == null ||
            suffixText == null || keyLayout == null || wasdSprite == null || runSprite == null || jumpSprite == null)
            yield break;

        // Let scene startup establish its story/dialogue ownership first.
        yield return null;

        if (!SessionStoryState.GetFlag(CompletionFlag))
        {
            yield return RunMovementTutorial();
            if (!SessionStoryState.GetFlag(CompletionFlag))
                yield break;
        }
        else
        {
            completionReady = true;
        }

        if (playerController == null || prefixText == null || keyImage == null || suffixText == null)
            yield break;

        if (!SessionStoryState.GetFlag(RunCompletionFlag))
            yield return RunTutorialStep("Pindutin ang", runSprite, "habang gumagalaw para tumakbo.", runImageHeight, RunCompletionFlag);

        if (!SessionStoryState.GetFlag(JumpCompletionFlag))
            yield return RunJumpTutorial();
    }

    private IEnumerator RunMovementTutorial()
    {
        float safeElapsed = 0f;
        while (!IsGameplaySafe() || safeElapsed < Mathf.Max(0f, delay))
        {
            safeElapsed = IsGameplaySafe() ? safeElapsed + Time.deltaTime : 0f;
            yield return null;
        }

        Vector3 startPosition = player.position;
        ShowPrompt("Pindutin ang", wasdSprite, "para gumalaw.", 36f);
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
                // Movement during a story/dialogue or modal UI must not finish onboarding.
                startPosition = player.position;
                ShowPrompt("Pindutin ang", wasdSprite, "para gumalaw.", 36f);
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

            FadePromptIn();
            yield return null;
        }

        bool fadeOutCompleted = moveTextGroup.alpha <= 0f;
        while (moveTextGroup.alpha > 0f)
        {
            if (!IsGameplaySafe())
            {
                HidePrompt();
                yield return WaitUntilGameplaySafe();
                fadeOutCompleted = true;
                break;
            }

            FadePromptOut();
            fadeOutCompleted = moveTextGroup.alpha <= 0f;
            yield return null;
        }

        HidePrompt();
        if (movementCompleted && fadeOutCompleted && IsGameplaySafe() &&
            isActiveAndEnabled && !completionNotificationCancelled)
        {
            completionReady = true;
            // Keep this notification at successful WASD completion so existing navigation
            // begins before the optional Run and Jump follow-up steps.
            NotifyTutorialCompleted();
        }
    }

    private IEnumerator RunTutorialStep(string prefix, Sprite sprite, string suffix, float imageHeight, string completionFlag)
    {
        yield return WaitUntilGameplaySafe();

        bool shiftReleasedAfterReveal = !Input.GetKey(KeyCode.LeftShift);
        observedRunStoppedAfterReveal = playerController == null || !playerController.IsRunning;
        ShowPrompt(prefix, sprite, suffix, imageHeight);

        bool waitingForSafety = false;
        while (!SessionStoryState.GetFlag(completionFlag) && isActiveAndEnabled)
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
                // Requiring a release after each interruption rejects held Shift input.
                shiftReleasedAfterReveal = !Input.GetKey(KeyCode.LeftShift);
                observedRunStoppedAfterReveal = playerController == null || !playerController.IsRunning;
                ShowPrompt(prefix, sprite, suffix, imageHeight);
                waitingForSafety = false;
            }

            if (!Input.GetKey(KeyCode.LeftShift))
                shiftReleasedAfterReveal = true;
            if (playerController != null && !playerController.IsRunning)
                observedRunStoppedAfterReveal = true;

            if (shiftReleasedAfterReveal && observedRunStoppedAfterReveal &&
                playerController != null && playerController.IsRunning)
            {
                SessionStoryState.SetFlag(completionFlag, true);
                break;
            }

            FadePromptIn();
            yield return null;
        }

        if (SessionStoryState.GetFlag(completionFlag))
            yield return FadePromptOutWhenSafe();
    }

    private IEnumerator RunJumpTutorial()
    {
        yield return WaitUntilGameplaySafe();

        bool waitingForSafety = false;
        while (!SessionStoryState.GetFlag(JumpCompletionFlag) && isActiveAndEnabled)
        {
            if (!IsGameplaySafe())
            {
                StopObservingJump();
                HidePrompt();
                waitingForSafety = true;
                yield return null;
                continue;
            }

            if (waitingForSafety)
            {
                // Resume this step, but only accept a fresh Space press after it is visible.
                BeginObservingJump();
                ShowPrompt("Pindutin ang", jumpSprite, "para tumalon.", 16f);
                waitingForSafety = false;
            }
            else if (!jumpStepActive)
            {
                BeginObservingJump();
                ShowPrompt("Pindutin ang", jumpSprite, "para tumalon.", jumpImageHeight);
            }

            if (successfulJumpObserved)
            {
                SessionStoryState.SetFlag(JumpCompletionFlag, true);
                break;
            }

            FadePromptIn();
            yield return null;
        }

        StopObservingJump();
        if (SessionStoryState.GetFlag(JumpCompletionFlag))
            yield return FadePromptOutWhenSafe();
    }

    private void BeginObservingJump()
    {
        successfulJumpObserved = false;
        spaceReleasedAfterReveal = !Input.GetKey(KeyCode.Space);
        jumpStepActive = true;
        if (playerController != null)
            playerController.JumpStarted += HandleJumpStarted;
    }

    private void StopObservingJump()
    {
        if (playerController != null)
            playerController.JumpStarted -= HandleJumpStarted;
        jumpStepActive = false;
    }

    private void HandleJumpStarted()
    {
        if (jumpStepActive && spaceReleasedAfterReveal && IsGameplaySafe())
            successfulJumpObserved = true;
    }

    private void Update()
    {
        if (jumpStepActive && !Input.GetKey(KeyCode.Space))
            spaceReleasedAfterReveal = true;
    }

    private IEnumerator FadePromptOutWhenSafe()
    {
        while (moveTextGroup != null && moveTextGroup.alpha > 0f)
        {
            if (!IsGameplaySafe())
            {
                HidePrompt();
                yield return WaitUntilGameplaySafe();
                break;
            }

            FadePromptOut();
            yield return null;
        }
        HidePrompt();
    }

    private IEnumerator WaitUntilGameplaySafe()
    {
        while (isActiveAndEnabled && !IsGameplaySafe())
        {
            HidePrompt();
            yield return null;
        }
    }

    private void ShowPrompt(string prefix, Sprite sprite, string suffix, float imageHeight)
    {
        prefixText.text = prefix;
        suffixText.text = suffix;
        keyImage.sprite = sprite;
        keyImage.preserveAspect = true;

        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        float imageWidth = imageHeight * aspect;
        keyImage.rectTransform.sizeDelta = new Vector2(imageWidth, imageHeight);
        keyLayout.preferredWidth = imageWidth;
        keyLayout.preferredHeight = imageHeight;

        moveTextGroup.alpha = 0f;
        moveTextGroup.gameObject.SetActive(true);
    }

    private void FadePromptIn()
    {
        moveTextGroup.alpha = Mathf.MoveTowards(moveTextGroup.alpha, 1f,
            Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
    }

    private void FadePromptOut()
    {
        moveTextGroup.alpha = Mathf.MoveTowards(moveTextGroup.alpha, 0f,
            Mathf.Max(0.01f, fadeSpeed) * Time.deltaTime);
    }

    private void OnEnable()
    {
        completionNotificationCancelled = false;
    }

    private void OnDisable()
    {
        // Disabling a MonoBehaviour need not stop its coroutine; never report that run as completed.
        completionNotificationCancelled = true;
        StopObservingJump();
    }

    private void OnDestroy()
    {
        StopObservingJump();
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

    private bool IsGameplaySafe()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || StorySequenceCoordinator.IsStorySequenceActive ||
            (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive))
            return false;

        if (inventoryUI == null)
            inventoryUI = FindAnyObjectByType<InventoryUI>();
        if (journal == null)
            journal = FindAnyObjectByType<ReconstructionJournalManager>();
        if (pauseMenu == null)
            pauseMenu = FindAnyObjectByType<AlaalaPauseMenuController>();

        return (inventoryUI == null || !inventoryUI.IsOpen) &&
               (journal == null || !journal.IsOpen) &&
               (pauseMenu == null || !pauseMenu.IsOpen);
    }

    private void HidePrompt()
    {
        if (moveTextGroup == null)
            return;

        moveTextGroup.alpha = 0f;
        moveTextGroup.gameObject.SetActive(false);
    }
}
