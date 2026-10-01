using System.Collections;
using DialogueEditor;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Plays the Pili establishing Timeline once per session.</summary>
[DefaultExecutionOrder(-10000)]
[RequireComponent(typeof(PlayableDirector))]
public sealed class IntroMakamisaCutsceneController : MonoBehaviour
{
    public const string IntroSeenFlag = "makamisa_pili_intro_seen";

    [SerializeField] private PlayableDirector introDirector;
    [SerializeField] private CinemachineCamera introCamera;
    [SerializeField] private SelfDialogueTrigger openingSelfDialogue;

    private StorySequenceToken storyToken;
    private Coroutine openingDialogueRoutine;
    private bool started;
    private bool cleaningUp;

    private void Awake()
    {
        if (introDirector == null)
            introDirector = GetComponent<PlayableDirector>();

        // Play On Awake is also disabled in the scene so no Timeline frame can
        // evaluate before this session-state check runs.
        if (introDirector != null)
            introDirector.playOnAwake = false;

        if (SessionStoryState.GetFlag(IntroSeenFlag))
        {
            DisableIntroCamera();
            enabled = false;
            return;
        }

        if (introDirector == null || introDirector.playableAsset == null || introCamera == null)
        {
            Debug.LogError("Pili intro is missing its Timeline or cinematic camera.", this);
            DisableIntroCamera();
            enabled = false;
            return;
        }

        introDirector.stopped += HandleIntroStopped;
        storyToken = StorySequenceCoordinator.Acquire(this);
        if (storyToken == null)
        {
            Debug.LogError("Pili intro could not acquire story sequence ownership.", this);
            DisableIntroCamera();
            enabled = false;
            return;
        }

        started = true;
        introDirector.Play();
    }

    private void HandleIntroStopped(PlayableDirector stoppedDirector)
    {
        if (!started || cleaningUp || !isActiveAndEnabled || stoppedDirector != introDirector)
            return;

        // This director uses Wrap Mode None. Teardown cancels the sequence and
        // unsubscribes before stopping it, so this is its natural end.
        started = false;
        introDirector.stopped -= HandleIntroStopped;
        DisableIntroCamera();

        ConversationManager manager = ConversationManager.Instance;
        Debug.Log(
            $"[PiliIntroDiagnostic] selfDialogueNull={openingSelfDialogue == null}, " +
            $"selfDialogueName={(openingSelfDialogue != null ? openingSelfDialogue.gameObject.name : "<null>")}, " +
            $"selfDialogueActive={openingSelfDialogue != null && openingSelfDialogue.gameObject.activeInHierarchy}, " +
            $"selfDialogueEnabled={openingSelfDialogue != null && openingSelfDialogue.enabled}, " +
            $"conversationManagerNull={manager == null}, " +
            $"conversationManagerActive={manager != null && manager.gameObject.activeInHierarchy}, " +
            $"conversationManagerEnabled={manager != null && manager.enabled}, " +
            $"conversationActive={manager != null && manager.IsConversationActive}", this);
        if (openingSelfDialogue == null || !openingSelfDialogue.isActiveAndEnabled ||
            manager == null || !manager.isActiveAndEnabled || manager.IsConversationActive)
        {
            Debug.LogError("Pili opening self-dialogue is unavailable after the Timeline.", this);
            ReleaseStoryToken();
            return;
        }

        bool dialogueStarted = false;
        ConversationManager.ConversationStartEvent onStarted = () => dialogueStarted = true;
        ConversationManager.OnConversationStarted += onStarted;
        try
        {
            openingSelfDialogue.StartSelfDialogue();
        }
        finally
        {
            ConversationManager.OnConversationStarted -= onStarted;
        }

        if (!dialogueStarted || !manager.IsConversationActive)
        {
            Debug.LogError("Pili opening self-dialogue did not start.", this);
            ReleaseStoryToken();
            return;
        }

        openingDialogueRoutine = StartCoroutine(WaitForOpeningDialogue(manager));
    }

    private IEnumerator WaitForOpeningDialogue(ConversationManager manager)
    {
        // The end event fires when the closing transition begins. Keep the
        // intro token until ConversationManager has fully turned its UI off.
        while (manager != null && manager.isActiveAndEnabled &&
               manager.IsConversationActive)
            yield return null;

        openingDialogueRoutine = null;
        if (manager != null && manager.isActiveAndEnabled)
            SessionStoryState.SetFlag(IntroSeenFlag, true);
        else
            Debug.LogWarning("Pili opening self-dialogue closed unexpectedly.", this);

        ReleaseStoryToken();
    }

    private void OnDisable()
    {
        CleanupInterruptedSequence();
    }

    private void OnDestroy()
    {
        CleanupInterruptedSequence();
    }

    private void CleanupInterruptedSequence()
    {
        if (cleaningUp)
            return;

        cleaningUp = true;
        started = false;
        if (openingDialogueRoutine != null)
        {
            StopCoroutine(openingDialogueRoutine);
            openingDialogueRoutine = null;
        }
        if (introDirector != null)
        {
            introDirector.stopped -= HandleIntroStopped;
            if (introDirector.state == PlayState.Playing)
                introDirector.Stop();
        }

        DisableIntroCamera();
        ReleaseStoryToken();
        cleaningUp = false;
    }

    private void DisableIntroCamera()
    {
        if (introCamera != null)
            introCamera.enabled = false;
    }

    private void ReleaseStoryToken()
    {
        storyToken?.Release();
        storyToken = null;
    }
}
