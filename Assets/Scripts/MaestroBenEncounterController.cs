using DialogueEditor;
using Supercyan.FreeSample;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System.Collections.Generic;

[DefaultExecutionOrder(-10000)]
public sealed class MaestroBenEncounterController : MonoBehaviour
{
    public const string IntroSeenFlag = "maestro_ben_intro_seen";
    public const string CompletedFlag = "maestro_ben_completed";

    private const string MainTaskId = "main_investigate_pili";
    private const string ChurchStageId = "investigate_church_document";
    private const string MaestroBenObservationId = "ang_hindi_natapos_na_akda";

    [SerializeField] private PlayableDirector entranceTimeline;
    [SerializeField] private SelfDialogueTrigger introConversation;
    [SerializeField] private NPCConversationTrigger interaction;
    [SerializeField] private SimpleSampleCharacterControl playerMovement;

    [Header("Development Testing")]
    [Tooltip("Testing only. Skips the entrance Timeline without changing story state or progression.")]
    [SerializeField] private bool skipIntroTimelineForTesting = false;

    private StorySequenceToken storySequenceToken;
    private bool introTimelineManaged, introTimelineActive;
    private bool introDialogueStarted, introDialogueDismissed, introPausedForDialogue;
    private NPCConversation timelineConversation;
    private PlayableAsset ownedTimelineAsset;
    private DirectorWrapMode originalWrapMode;
    private Animator introVisualAnimator;
    private RuntimeAnimatorController introOriginalAnimatorController;
    private AnimatorOverrideController introVisualIdleController;

    private void Awake()
    {
        // Established before destination loading, before this controller can play its Timeline.
        // Do not fabricate IntroSeen/Completed history for a direct Part 3 test.
        if (NewAlaalaPostCreditsEpilogue.IsVerifiedHouseReturn(gameObject.scene))
        {
            if (entranceTimeline != null) { entranceTimeline.playOnAwake = false; entranceTimeline.Stop(); }
            if (introConversation != null) introConversation.enabled = false;
            if (interaction != null) interaction.enabled = false;
            return;
        }

        if (introConversation != null)
            introConversation.ConversationFinished += HandleTimelineConversationFinished;

        if (interaction != null)
            interaction.OnFirstConversationFinished.AddListener(HandleManualFirstConversationFinished);

        if (entranceTimeline != null)
            entranceTimeline.playOnAwake = false;

        if (skipIntroTimelineForTesting)
        {
            entranceTimeline?.Stop();

            if (playerMovement != null)
                playerMovement.enabled = true;

            if (interaction != null)
                interaction.enabled = true;

            return;
        }

        bool completed = SessionStoryState.GetFlag(CompletedFlag);
        bool introSeen = SessionStoryState.GetFlag(IntroSeenFlag);

        // Keep older/in-progress session state internally consistent.
        if (completed && !introSeen)
        {
            SessionStoryState.SetFlag(IntroSeenFlag, true);
            introSeen = true;
        }

        // Manual interaction stays unavailable during the first Timeline so its
        // signal remains the sole owner of the first automatic conversation.
        if (interaction != null)
            interaction.enabled = introSeen;

        if (introSeen)
        {
            entranceTimeline?.Stop();

            // The skipped Timeline cannot own movement state on a return visit.
            if (playerMovement != null)
                playerMovement.enabled = true;

            return;
        }

        if (entranceTimeline == null || entranceTimeline.playableAsset == null)
        {
            Debug.LogWarning(
                "Maestro Ben entrance Timeline is missing; the intro was not marked as seen.",
                this);
            return;
        }

        // The visit is committed immediately before starting the existing asset.
        SessionStoryState.SetFlag(IntroSeenFlag, true);
        storySequenceToken = StorySequenceCoordinator.Acquire(this);
        BeginTimelineOwnedIntro();
        entranceTimeline.Play();
    }

    private void OnDestroy()
    {
        if (introTimelineActive) CancelTimelineOwnedIntro("Encounter destroyed/scene unloaded.");
        ReleaseIntroVisualIdle();
        DetachTimelineCallbacks();
        if (introConversation != null)
            introConversation.ConversationFinished -= HandleTimelineConversationFinished;

        if (interaction != null)
            interaction.OnFirstConversationFinished.RemoveListener(HandleManualFirstConversationFinished);

        if (storySequenceToken != null)
        {
            storySequenceToken.Release();
            storySequenceToken = null;
        }
    }

    private void HandleTimelineConversationFinished()
    {
        // This legacy event fires when UI closure begins, including cancellation.
        // The Timeline-owned intro uses the exact, normally closed conversation below.
        if (introTimelineManaged) return;
        if (SessionStoryState.GetFlag(CompletedFlag))
            return;

        CompleteFirstConversation();
    }

    private void HandleManualFirstConversationFinished()
    {
        if (introTimelineActive) return;
        CompleteFirstConversation();
    }

    private void BeginTimelineOwnedIntro()
    {
        introTimelineManaged = introTimelineActive = true;
        timelineConversation = introConversation != null ? introConversation.GetComponent<NPCConversation>() : null;
        ownedTimelineAsset = entranceTimeline.playableAsset;
        originalWrapMode = entranceTimeline.extrapolationMode;
        // None clears time before stopped listeners can distinguish interruption from
        // completion. Hold only at the authored endpoint; Update then releases the graph.
        // The serialized Wrap Mode, Timeline duration and clips remain untouched.
        entranceTimeline.extrapolationMode = DirectorWrapMode.Hold;
        entranceTimeline.stopped += HandleIntroDirectorStopped;
        ConversationManager.OnConversationStarted += HandleIntroConversationStarted;
        ConversationManager.ConversationClosed += HandleIntroConversationClosed;
    }

    private void HandleIntroConversationStarted()
    {
        var manager = ConversationManager.Instance;
        if (!introTimelineActive || introDialogueStarted || manager == null ||
            timelineConversation == null || manager.ActiveConversation != timelineConversation) return;
        if (entranceTimeline == null || entranceTimeline.playableAsset != ownedTimelineAsset ||
            entranceTimeline.state != PlayState.Playing || !entranceTimeline.playableGraph.IsValid())
        {
            CancelTimelineOwnedIntro("Intro conversation started without the original playing Timeline graph.");
            return;
        }
        introDialogueStarted = introPausedForDialogue = true;
        // The existing signal still calls StartSelfDialogue and the native dialogue lock.
        // Keep the authored graph/bindings. The local clip substitution below also
        // covers a stale Midair state underneath that graph, without changing Grounded.
        entranceTimeline.Pause();
        try
        {
            if (!TryBeginIntroVisualIdle(out string reason))
                CancelTimelineOwnedIntro("Intro visual Idle could not acquire presentation: " + reason);
        }
        catch (System.Exception exception)
        {
            // Do not abort the native StartConversation call/UI initialization.
            CancelTimelineOwnedIntro("Intro visual Idle initialization failed: " + exception.Message);
        }
    }

    private void HandleIntroConversationClosed(NPCConversation conversation, bool normal)
    {
        if (!introTimelineActive || !introDialogueStarted || introDialogueDismissed ||
            conversation != timelineConversation) return;
        if (!normal)
        {
            CancelTimelineOwnedIntro("Intro conversation was interrupted rather than normally dismissed.");
            return;
        }
        if (!introPausedForDialogue || entranceTimeline == null ||
            entranceTimeline.playableAsset != ownedTimelineAsset || entranceTimeline.state != PlayState.Paused ||
            !entranceTimeline.playableGraph.IsValid() || storySequenceToken == null || !storySequenceToken.IsValid)
        {
            CancelTimelineOwnedIntro("The paused intro animation graph was lost before dialogue closure.");
            return;
        }
        introDialogueDismissed = true;
        introPausedForDialogue = false;
        ReleaseIntroVisualIdle();
        // Native dialogue closure has restored the controller. Our existing Story
        // Sequence token still blocks movement/jump input while grounding can update.
        entranceTimeline.Resume(); // Continue at the same time; never Play/restart or replay signals.
    }

    private void Update()
    {
        if (!introTimelineActive) return;
        if (entranceTimeline == null || entranceTimeline.playableAsset != ownedTimelineAsset ||
            !entranceTimeline.playableGraph.IsValid() || storySequenceToken == null || !storySequenceToken.IsValid)
        {
            CancelTimelineOwnedIntro("The owned intro Timeline graph or Story Sequence token was lost.");
            return;
        }
        if (introPausedForDialogue)
        {
            if (introVisualIdleController == null || introVisualAnimator == null ||
                introVisualAnimator.runtimeAnimatorController != introVisualIdleController ||
                !IntroVisualContextValid() || HasCompetingIntroAnimationOwner())
            {
                CancelTimelineOwnedIntro("Intro visual animation ownership changed during dialogue.");
                return;
            }
            if (entranceTimeline.state != PlayState.Paused)
                CancelTimelineOwnedIntro("The intro Timeline was externally resumed during dialogue.");
            return;
        }
        if (entranceTimeline.time < entranceTimeline.duration) return;
        if (!introDialogueDismissed)
        {
            CancelTimelineOwnedIntro("The authored Timeline endpoint was reached without normal intro dialogue completion.");
            return;
        }
        introTimelineActive = false;
        DetachTimelineCallbacks();
        entranceTimeline.Stop(); // Actual authored endpoint, after manual dialogue completion.
        entranceTimeline.extrapolationMode = originalWrapMode;
        CompleteFirstConversation(); // Existing flags, task/tutorial progression and token release.
    }

    private void HandleIntroDirectorStopped(PlayableDirector director)
    {
        if (introTimelineActive && director == entranceTimeline)
            CancelTimelineOwnedIntro("The intro Timeline was stopped before its verified endpoint.");
    }

    private void DetachTimelineCallbacks()
    {
        ConversationManager.OnConversationStarted -= HandleIntroConversationStarted;
        ConversationManager.ConversationClosed -= HandleIntroConversationClosed;
        if (entranceTimeline != null) entranceTimeline.stopped -= HandleIntroDirectorStopped;
    }

    private void CancelTimelineOwnedIntro(string reason)
    {
        if (!introTimelineActive) return;
        introTimelineActive = false;
        ReleaseIntroVisualIdle();
        DetachTimelineCallbacks();
        if (entranceTimeline != null)
        {
            if (entranceTimeline.playableAsset == ownedTimelineAsset) entranceTimeline.Stop();
            entranceTimeline.extrapolationMode = originalWrapMode;
        }
        if (!SessionStoryState.GetFlag(CompletedFlag)) SessionStoryState.SetFlag(IntroSeenFlag, false);
        ReleaseStorySequence(); // Release only this encounter; native dialogue retains any active lock.
        Debug.LogWarning("Maestro Ben intro cinematic cancelled: " + reason, this);
    }

    private void OnDisable()
    {
        if (introTimelineActive) CancelTimelineOwnedIntro("Encounter component disabled.");
        ReleaseIntroVisualIdle();
        DetachTimelineCallbacks();
    }

    private bool IntroVisualContextValid()
    {
        var manager = ConversationManager.Instance;
        return introTimelineManaged && introTimelineActive && introDialogueStarted && !introDialogueDismissed &&
            gameObject.scene.path == "Assets/Scenes/MaestroBenHouse.unity" &&
            UnityEngine.SceneManagement.SceneManager.GetActiveScene() == gameObject.scene &&
            ownedTimelineAsset != null && ownedTimelineAsset.name == "MaestroBenTimeline" &&
            storySequenceToken != null && storySequenceToken.IsValid && StorySequenceCoordinator.ActiveOwnerCount == 1 &&
            // ActiveConversation is assigned before OnConversationStarted, but the
            // manager's UI-state-based IsConversationActive becomes true after it.
            manager != null && timelineConversation != null && manager.ActiveConversation == timelineConversation;
    }

    private bool TryBeginIntroVisualIdle(out string reason)
    {
        reason = "The original intro scene, dialogue or exclusive Story Sequence context is not active.";
        if (!IntroVisualContextValid() || playerMovement == null || introVisualIdleController != null) return false;
        introVisualAnimator = playerMovement.GetComponent<Animator>();
        if (introVisualAnimator == null || introVisualAnimator.name != "FInalChar1" ||
            introVisualAnimator.gameObject.scene != gameObject.scene || !introVisualAnimator.isActiveAndEnabled ||
            introVisualAnimator.transform.parent == null || introVisualAnimator.transform.parent.name != "Miguel" ||
            introVisualAnimator.applyRootMotion)
        { reason = "The audited Miguel/FInalChar1 Animator is unavailable or its root-motion configuration changed."; return false; }
        introOriginalAnimatorController = introVisualAnimator.runtimeAnimatorController;
        if (introOriginalAnimatorController == null || introOriginalAnimatorController.name != "FreeAnimations" ||
            HasCompetingIntroAnimationOwner())
        { reason = "FreeAnimations is unavailable or another cinematic animation owner is active."; return false; }

        // Resolve the real Idle asset from the currently authored bound track. Never
        // activate in a walking clip or use a hardcoded time/asset path.
        AnimationClip idle = null;
        var timeline = ownedTimelineAsset as TimelineAsset;
        if (timeline == null) { reason = "The owned asset is not a TimelineAsset."; return false; }
        foreach (var track in timeline.GetOutputTracks())
        {
            if (!(track is AnimationTrack)) continue;
            var binding = entranceTimeline.GetGenericBinding(track);
            if (binding != introVisualAnimator && binding != introVisualAnimator.gameObject) continue;
            foreach (var clip in track.GetClips())
            {
                if (entranceTimeline.time < clip.start || entranceTimeline.time >= clip.end) continue;
                var animation = clip.asset as AnimationPlayableAsset;
                if (animation == null || animation.clip == null || animation.clip.name != "idle" ||
                    clip.EvaluateMixIn(entranceTimeline.time) * clip.EvaluateMixOut(entranceTimeline.time) < .999f)
                { reason = "The current authored dialogue position is not exclusively full-weight Idle."; return false; }
                idle = animation.clip;
            }
        }
        if (idle == null) { reason = "No authored Idle clip targets Miguel at the dialogue signal."; return false; }

        // One runtime instance on this Animator only. Midair/Jump/Land/blend-tree
        // clips all present the same verified Idle while parameters/states stay intact.
        // No second playable output, Rebind, Play, parameter writes or physics writes.
        introVisualIdleController = new AnimatorOverrideController(introOriginalAnimatorController)
        {
            name = "Maestro Ben Intro Visual Idle (Runtime)",
            hideFlags = HideFlags.DontSave
        };
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        introVisualIdleController.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; ++i)
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, idle);
        introVisualIdleController.ApplyOverrides(overrides);
        introVisualAnimator.runtimeAnimatorController = introVisualIdleController;
        return true;
    }

    private bool HasCompetingIntroAnimationOwner()
    {
        if (introVisualAnimator == null) return true;
        var reaction = introVisualAnimator.GetComponent<CharacterReactionController>();
        if (reaction != null && reaction.IsReacting) return true;
        foreach (var director in FindObjectsByType<PlayableDirector>(FindObjectsInactive.Exclude))
        {
            if (director == entranceTimeline || director.playableAsset == null || !director.playableGraph.IsValid()) continue;
            foreach (var output in director.playableAsset.outputs)
            {
                var binding = director.GetGenericBinding(output.sourceObject);
                var animator = binding as Animator;
                if (animator == null && binding is GameObject boundObject) animator = boundObject.GetComponent<Animator>();
                if (animator != null && (animator.transform == introVisualAnimator.transform ||
                    introVisualAnimator.transform.IsChildOf(animator.transform) || animator.transform.IsChildOf(introVisualAnimator.transform)))
                    return true;
            }
        }
        return false;
    }

    private void ReleaseIntroVisualIdle()
    {
        // Do not overwrite a controller installed by a subsequent owner.
        if (introVisualAnimator != null && introVisualIdleController != null &&
            introVisualAnimator.runtimeAnimatorController == introVisualIdleController)
            introVisualAnimator.runtimeAnimatorController = introOriginalAnimatorController;
        if (introVisualIdleController != null) Destroy(introVisualIdleController);
        introVisualIdleController = null;
        introOriginalAnimatorController = null;
        introVisualAnimator = null;
    }

    private void CompleteFirstConversation()
    {
        SessionStoryState.SetFlag(CompletedFlag, true);

        ReconstructionJournalManager.Instance?.UnlockObservation(MaestroBenObservationId);

        if (interaction != null)
            interaction.enabled = true;

        TaskManager manager = TaskManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning(
                "Maestro Ben conversation finished, but TaskManager.Instance is null; " +
                "the Church investigation stage could not be applied.",
                this);
            ReleaseStorySequence();
            return;
        }

        bool advanced = manager.AdvanceTaskStage(MainTaskId, ChurchStageId);
        if (!advanced)
        {
            Debug.LogWarning(
                "Maestro Ben conversation finished, but the main task could not advance " +
                "to the Church investigation stage.",
                this);
        }
        else
        {
            GameplaySystemState.SetUnlocked(GameplaySystemId.Clarity, true);
        }

        if (storySequenceToken != null)
        {
            storySequenceToken.Release();
            storySequenceToken = null;
        }

        if (advanced && GameplaySystemState.IsUnlocked(GameplaySystemId.Clarity))
        {
            GameplaySystemTutorialManager.Instance?.NotifySystemRevealCompleted(
                GameplaySystemId.Clarity);
        }
    }

    private void ReleaseStorySequence()
    {
        if (storySequenceToken == null)
            return;

        storySequenceToken.Release();
        storySequenceToken = null;
    }
}
