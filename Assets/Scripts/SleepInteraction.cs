using UnityEngine;
using System.Collections;
using TMPro;
using Supercyan.FreeSample;

public class SleepInteraction : MonoBehaviour
{
    public event System.Action SleepAccepted;
    public bool HasSleepBeenAccepted => hasSlept || sleepSequenceRunning;
    // Excludes interrupted sleeps observed by this implementation from the legacy fallback.
    public static bool HasObservedSleepAttempt { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSleepObservation() => HasObservedSleepAttempt = false;

    public static void ResetForNewGame() => ResetSleepObservation();

    [SerializeField] private GameObject interactText;
    [SerializeField] private TMP_Text sleepText;

    [Header("Dream")]
    [SerializeField] private DreamWhisperController dreamWhisperController;

    [Header("Player Control")]
    [SerializeField] private SimpleSampleCharacterControl playerMovement;
    [SerializeField] private Animator playerAnimator;

    [Header("Wake Reaction")]
    [SerializeField] private CharacterReactionController wakeReaction;

    [Header("Wake Dialogue")]
    [SerializeField] private SelfDialogueTrigger wakeSelfDialogue;

    private bool playerNear = false;  
    private bool hasSlept = false;
    private bool sleepSequenceRunning;
    private bool wakeDialogueCompletedNormally;
    private System.Action<DialogueEditor.NPCConversation, bool> wakeClosedListener;
    private StorySequenceToken storySequenceToken;

    private void Awake()
    {
        GameplayHUDTarget.AttachTo(interactText);
    }

    private void Start()
    {
        if(interactText != null)
            interactText.SetActive(false);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasSlept)
        {
            playerNear = true;
            if (interactText != null)
                interactText.SetActive(true);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
            if (interactText != null)
                interactText.SetActive(false);
        }
    }
    private void Update()
    {
        if (!StorySequenceCoordinator.IsStorySequenceActive &&
            playerNear && Input.GetKeyDown(KeyCode.E) && !hasSlept &&
            !sleepSequenceRunning)
        {
            sleepSequenceRunning = true;
            StartCoroutine(Sleep());
        }
    }
    private IEnumerator Sleep()
    {
        AcquireStorySequence();

        try
        {
            HasObservedSleepAttempt = true;
            hasSlept = true;
            NotifySleepAccepted();
            bool shouldPlayDream = !GameFlags.isMorning;
            bool movementWasEnabled = playerMovement != null && playerMovement.enabled;

            SetPlayerMovementLocked(true);

            if (interactText != null)
                interactText.SetActive(false);

            sleepText.gameObject.SetActive(false);

            FadeController fadeController = FindAnyObjectByType<FadeController>();

            if(fadeController != null)
            {
                yield return StartCoroutine(fadeController.FadeToBlack());

                yield return new WaitForSeconds(0.7f);

                sleepText.gameObject.SetActive(true);

                sleepText.text = "z";
                yield return new
                WaitForSeconds(1f);

                sleepText.text = "zZ";
                yield return new
                WaitForSeconds(1f);

                sleepText.text = "zZz";
                yield return new
                WaitForSeconds(2f);

                sleepText.gameObject.SetActive(false);

                if (shouldPlayDream && dreamWhisperController != null)
                    yield return dreamWhisperController.PlaySequence();
            }

            GameFlags.isMorning = true;

            if (fadeController != null)
                yield return StartCoroutine(fadeController.FadeFromBlack());

            if (wakeReaction != null)
                yield return wakeReaction.PlayReaction();

            if (wakeSelfDialogue != null)
                yield return PlayWakeDialogue();

            RestorePlayerMovement(movementWasEnabled);

            if (ReconstructionJournalManager.Instance != null)
            {
                ReconstructionJournalManager.Instance.UnlockJournalForFirstDream();
                // false from the unlock API can mean already unlocked, so verify its state.
                // This is observational and deliberately outside interruption cleanup.
                if (wakeDialogueCompletedNormally && isActiveAndEnabled &&
                    GameplaySystemState.IsUnlocked(GameplaySystemId.Journal))
                    SessionStoryState.SetFlag("posada_first_sleep_completed", true);
            }
        }
        finally
        {
            sleepSequenceRunning = false;
            ReleaseStorySequence();
        }
    }

    private void NotifySleepAccepted()
    {
        var listeners = SleepAccepted;
        if (listeners == null)
            return;
        foreach (System.Action listener in listeners.GetInvocationList())
        {
            try { listener(); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
        }
    }

    private IEnumerator PlayWakeDialogue()
    {
        wakeDialogueCompletedNormally = false;
        DialogueEditor.NPCConversation wakeConversation = null;
        System.Action<DialogueEditor.NPCConversation, bool> onClosed = (conversation, normal) =>
        {
            if (conversation == wakeConversation)
                wakeDialogueCompletedNormally = normal;
        };
        bool conversationFinished = false;
        System.Action onConversationFinished = () => conversationFinished = true;

        wakeSelfDialogue.ConversationFinished += onConversationFinished;
        wakeClosedListener = onClosed;
        DialogueEditor.ConversationManager.ConversationClosed += onClosed;

        try
        {
            wakeSelfDialogue.StartSelfDialogue();
            if (DialogueEditor.ConversationManager.Instance != null)
                wakeConversation = DialogueEditor.ConversationManager.Instance.ActiveConversation;

            while (!conversationFinished)
                yield return null;

            while (DialogueEditor.ConversationManager.Instance != null &&
                   DialogueEditor.ConversationManager.Instance.IsConversationActive)
            {
                yield return null;
            }
        }
        finally
        {
            wakeSelfDialogue.ConversationFinished -= onConversationFinished;
            DialogueEditor.ConversationManager.ConversationClosed -= onClosed;
            if (wakeClosedListener == onClosed) wakeClosedListener = null;
        }
    }

    private void SetPlayerMovementLocked(bool locked)
    {
        if (playerMovement != null)
            playerMovement.enabled = !locked;

        if (locked && playerAnimator != null)
            playerAnimator.SetFloat("MoveSpeed", 0f);
    }

    private void RestorePlayerMovement(bool movementWasEnabled)
    {
        if (playerMovement != null)
            playerMovement.enabled = movementWasEnabled;
    }

    private void AcquireStorySequence()
    {
        if (storySequenceToken == null || !storySequenceToken.IsValid)
            storySequenceToken = StorySequenceCoordinator.Acquire(this);
    }

    private void ReleaseStorySequence()
    {
        if (storySequenceToken == null)
            return;

        storySequenceToken.Release();
        storySequenceToken = null;
    }

    private void OnDisable()
    {
        DialogueEditor.ConversationManager.ConversationClosed -= wakeClosedListener;
        wakeClosedListener = null;
        wakeDialogueCompletedNormally = false;
        ReleaseStorySequence();
    }

    private void OnDestroy()
    {
        DialogueEditor.ConversationManager.ConversationClosed -= wakeClosedListener;
        wakeClosedListener = null;
        ReleaseStorySequence();
    }
}
