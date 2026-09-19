using System.Collections;
using DialogueEditor;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>Authored one-time reaction for completing the first information-gathering milestone.</summary>
public sealed class GatherInformationReactionController : MonoBehaviour
{
    private const string TaskId = "main_investigate_pili";
    private const string StageId = "gather_information";
    private const string SeenFlag = "gather_information_reaction_seen";
    private const string ReactionText = "Iba-iba talaga ang kanilang mga sinasabi... Kailangan ko ng taong makapagbibigay sa akin ng mas tiyak na impormasyon.";

    [SerializeField, Min(0f)] private float zoomInDuration = 0.5f;
    [SerializeField, Min(0f)] private float zoomOutDuration = 0.5f;
    [SerializeField, Range(0.01f, 0.2f)] private float perspectiveFovReduction = 0.08f;
    [SerializeField, Range(0.01f, 0.2f)] private float orthographicSizeReduction = 0.08f;

    private SelfDialogueTrigger selfDialogue;
    private StorySequenceToken storySequenceToken;
    private Coroutine reactionRoutine;
    private LensState lensState;
    private bool reactionRequested;
    private bool subscribed;

    private void Awake()
    {
        CreateReactionDialogue();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
        if (TaskManager.Instance != null && TaskManager.Instance.IsCurrentStage(TaskId, StageId))
            TryRequestReaction();
    }

    private void OnDisable()
    {
        if (subscribed && TaskManager.Instance != null)
            TaskManager.Instance.StageChanged -= HandleStageChanged;
        subscribed = false;
        if (reactionRoutine != null) StopCoroutine(reactionRoutine);
        reactionRoutine = null;
        lensState?.Restore();
        lensState = null;
        ReleaseStorySequence();
    }

    private void Subscribe()
    {
        if (subscribed || TaskManager.Instance == null) return;
        TaskManager.Instance.StageChanged += HandleStageChanged;
        subscribed = true;
    }

    private void HandleStageChanged(string taskId, string stageId)
    {
        if (string.Equals(taskId, TaskId, System.StringComparison.Ordinal) &&
            string.Equals(stageId, StageId, System.StringComparison.Ordinal))
            TryRequestReaction();
    }

    private void TryRequestReaction()
    {
        if (reactionRequested || SessionStoryState.GetFlag(SeenFlag) ||
            TaskManager.Instance == null ||
            !TaskManager.Instance.IsCurrentStage(TaskId, "find_knowledgeable_person"))
            return;
        reactionRequested = true;
        reactionRoutine = StartCoroutine(WaitForConversationThenPlay());
    }

    private IEnumerator WaitForConversationThenPlay()
    {
        while (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive)
            yield return null;

        if (SessionStoryState.GetFlag(SeenFlag)) yield break;
        storySequenceToken = StorySequenceCoordinator.Acquire(this);
        if (storySequenceToken == null) yield break;
        SessionStoryState.SetFlag(SeenFlag, true);

        lensState = LensState.CaptureCurrent();
        yield return AnimateLens(zoomInDuration, 1f);
        if (selfDialogue != null)
        {
            bool finished = false;
            System.Action finishedHandler = () => finished = true;
            selfDialogue.ConversationFinished += finishedHandler;
            selfDialogue.StartSelfDialogue();
            while (!finished)
                yield return null;
            while (ConversationManager.Instance != null && ConversationManager.Instance.IsConversationActive)
                yield return null;
            selfDialogue.ConversationFinished -= finishedHandler;
        }
        yield return AnimateLens(zoomOutDuration, 0f);
        lensState.Restore();
        lensState = null;
        ReleaseStorySequence();
        reactionRoutine = null;
    }

    private IEnumerator AnimateLens(float duration, float target)
    {
        if (lensState == null || !lensState.StillOwnsCamera()) yield break;
        if (duration <= 0f) { lensState.SetZoomAmount(target, perspectiveFovReduction, orthographicSizeReduction); yield break; }
        float elapsed = 0f;
        float start = target > 0.5f ? 0f : 1f;
        while (elapsed < duration)
        {
            if (!lensState.StillOwnsCamera()) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            lensState.SetZoomAmount(Mathf.Lerp(start, target, t), perspectiveFovReduction, orthographicSizeReduction);
            yield return null;
        }
        lensState.SetZoomAmount(target, perspectiveFovReduction, orthographicSizeReduction);
    }

    private void ReleaseStorySequence()
    {
        if (storySequenceToken != null) storySequenceToken.Release();
        storySequenceToken = null;
    }

    private void CreateReactionDialogue()
    {
        GameObject root = new GameObject("Gather Information Reaction Dialogue");
        root.transform.SetParent(transform, false);
        NPCConversation conversation = root.AddComponent<NPCConversation>();
        conversation.ParameterList = new System.Collections.Generic.List<EditableParameter>();
        EditableConversation editable = new EditableConversation { Parameters = new System.Collections.Generic.List<EditableParameter>() };
        EditableSpeechNode speech = new EditableSpeechNode
        {
            ID = 0,
            Text = ReactionText,
            Name = "Miguel",
            AdvanceDialogueAutomatically = false,
            AutoAdvanceShouldDisplayOption = false
        };
        speech.EditorInfo.isRoot = true;
        editable.SpeechNodes.Add(speech);
        conversation.Serialize(editable);
        selfDialogue = root.AddComponent<SelfDialogueTrigger>();
        selfDialogue.ConfigureConversation(conversation);
    }

    private sealed class LensState
    {
        private readonly Camera outputCamera;
        private readonly CinemachineBrain brain;
        private readonly CinemachineCamera virtualCamera;
        private readonly bool orthographic;
        private readonly float originalValue;

        private LensState(Camera camera, CinemachineBrain cameraBrain, CinemachineCamera current, bool isOrtho, float value)
        { outputCamera = camera; brain = cameraBrain; virtualCamera = current; orthographic = isOrtho; originalValue = value; }

        public static LensState CaptureCurrent()
        {
            Camera camera = Camera.main;
            if (camera == null) return new LensState(null, null, null, false, 0f);
            CinemachineBrain cameraBrain = camera.GetComponent<CinemachineBrain>();
            if (cameraBrain != null)
            {
                CinemachineCamera current = cameraBrain.ActiveVirtualCamera as CinemachineCamera;
                if (current == null) return new LensState(camera, cameraBrain, null, false, 0f);
                LensSettings lens = current.Lens;
                return new LensState(camera, cameraBrain, current, lens.Orthographic, lens.Orthographic ? lens.OrthographicSize : lens.FieldOfView);
            }
            return new LensState(camera, null, null, camera.orthographic, camera.orthographic ? camera.orthographicSize : camera.fieldOfView);
        }

        public bool StillOwnsCamera()
        {
            if (outputCamera == null || Camera.main != outputCamera) return outputCamera == null;
            return brain == null || (virtualCamera != null && ReferenceEquals(brain.ActiveVirtualCamera, virtualCamera));
        }

        public void SetZoomAmount(float amount, float perspectiveReduction, float orthoReduction)
        {
            if (outputCamera == null) return;
            float value = Mathf.Lerp(originalValue, originalValue * (1f - (orthographic ? orthoReduction : perspectiveReduction)), Mathf.Clamp01(amount));
            if (virtualCamera != null)
            {
                LensSettings lens = virtualCamera.Lens;
                if (orthographic) lens.OrthographicSize = value; else lens.FieldOfView = value;
                virtualCamera.Lens = lens;
            }
            else if (brain == null)
            {
                if (orthographic) outputCamera.orthographicSize = value; else outputCamera.fieldOfView = value;
            }
        }

        public void Restore()
        {
            if (virtualCamera != null)
            {
                LensSettings lens = virtualCamera.Lens;
                if (orthographic) lens.OrthographicSize = originalValue; else lens.FieldOfView = originalValue;
                virtualCamera.Lens = lens;
            }
            else if (outputCamera != null && brain == null)
            {
                if (orthographic) outputCamera.orthographicSize = originalValue; else outputCamera.fieldOfView = originalValue;
            }
        }
    }
}
