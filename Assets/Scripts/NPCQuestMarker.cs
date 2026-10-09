using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Displays task markers by reading the existing task and story state.</summary>
[DisallowMultipleComponent]
public sealed class NPCQuestMarker : MonoBehaviour
{
    private const float ScreenSizeReferenceDistance = 10f;
    private const float ScreenSizeReferenceFov = 45f;

    public enum TaskStateRequirement { Any, Inactive, Active, Completed }
    public enum MarkerAppearance
    {
        YellowAvailable, YellowTarget, BlueAvailable, BlueTarget, GrayAvailable, GrayTarget
    }

    [Serializable]
    private sealed class MarkerRule
    {
        [SerializeField] private string taskId;
        [SerializeField] private string requiredStageId;
        [SerializeField] private string minimumStageId;
        [SerializeField] private TaskStateRequirement taskState = TaskStateRequirement.Any;
        [SerializeField] private string requiredStoryFlagId;
        [SerializeField] private bool expectedStoryFlagValue = true;
        [SerializeField] private string blockingStoryFlagId;
        [SerializeField] private MarkerAppearance appearance = MarkerAppearance.YellowTarget;
        [SerializeField] private bool hideWhenTaskCompleted = true;

        public string TaskId => taskId;
        public string RequiredStageId => requiredStageId;
        public string MinimumStageId => minimumStageId;
        public TaskStateRequirement TaskState => taskState;
        public string RequiredStoryFlagId => requiredStoryFlagId;
        public bool ExpectedStoryFlagValue => expectedStoryFlagValue;
        public string BlockingStoryFlagId => blockingStoryFlagId;
        public MarkerAppearance Appearance => appearance;
        public bool HideWhenTaskCompleted => hideWhenTaskCompleted;
    }

    [Header("Marker sprites (quest 1 = !, quest 2 = ?)")]
    [SerializeField] private Sprite yellowAvailable;
    [SerializeField] private Sprite yellowTarget;
    [SerializeField] private Sprite blueAvailable;
    [SerializeField] private Sprite blueTarget;
    [SerializeField] private Sprite grayAvailable;
    [SerializeField] private Sprite grayTarget;

    [Header("Presentation")]
    [Tooltip("Offset from this interaction anchor in unscaled local units.")]
    [SerializeField] private Vector3 markerLocalOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Min(0.001f)] private float markerWorldScale = 0.45f;
    [SerializeField, Min(0f), Tooltip("Vertical movement above and below the marker's base position, in world units.")]
    private float floatAmount = 0.1f;
    [SerializeField, Min(0f), Tooltip("Complete up/down cycles per second.")]
    private float floatSpeed = 0.55f;
    [SerializeField, Tooltip("Compensate for perspective distance while preserving Marker World Scale at the 10-unit reference distance.")]
    private bool maintainScreenSize = false;
    [SerializeField, Tooltip("Create the runtime visual as a scene-local root so it does not inherit this NPC transform's scale or rotation.")]
    private bool independentVisualTransform = false;
    [SerializeField] private bool faceGameplayCamera = true;
    [SerializeField] private bool showMarker = true;

    [Header("Rules; first matching rule wins")]
    [SerializeField] private List<MarkerRule> rules = new List<MarkerRule>();

    [Header("Optional second channel; first matching rule wins independently")]
    [SerializeField] private bool enableSecondaryChannel;
    [SerializeField] private List<MarkerRule> secondaryRules = new List<MarkerRule>();
    [SerializeField, Min(0f), Tooltip("Gap between the visible sprite edges, in world units at the configured scale.")]
    private float dualMarkerGap = 0.15f;

    private Transform visualTransform;
    private SpriteRenderer spriteRenderer;
    private Sprite displayedSprite;
    private Transform secondaryVisualTransform;
    private SpriteRenderer secondarySpriteRenderer;
    private TaskManager subscribedTaskManager;
    private NPCConversationTrigger conversationTrigger;
    private Coroutine dialogueCloseRoutine;
    private bool dialogueSuppressed;

    private void OnEnable()
    {
        SessionStoryState.FlagChanged += OnStoryFlagChanged;
        SubscribeToConversationManager();
        SubscribeToTaskManager();
        EnsureVisual();
        Refresh();
    }

    private void OnDisable()
    {
        SessionStoryState.FlagChanged -= OnStoryFlagChanged;
        UnsubscribeFromConversationManager();
        UnsubscribeFromTaskManager();
        if (dialogueCloseRoutine != null)
            StopCoroutine(dialogueCloseRoutine);
        dialogueCloseRoutine = null;
        dialogueSuppressed = false;
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
        if (secondarySpriteRenderer != null)
            secondarySpriteRenderer.enabled = false;
        CleanupIndependentVisual();
    }

    private void OnDestroy()
    {
        CleanupIndependentVisual();
    }

    private void Update()
    {
        // The TaskManager may be created after this scene component.
        SubscribeToTaskManager();
        if (spriteRenderer == null)
            EnsureVisual();
        if (enableSecondaryChannel && secondarySpriteRenderer == null)
            EnsureSecondaryVisual();

        if (dialogueSuppressed)
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
            if (secondarySpriteRenderer != null)
                secondarySpriteRenderer.enabled = false;
            if (dialogueCloseRoutine == null)
                dialogueCloseRoutine = StartCoroutine(RefreshAfterDialogueCloses());
        }
    }

    private void LateUpdate()
    {
        if (visualTransform == null)
            return;

        float floatOffset = Mathf.Sin(Time.time * floatSpeed * Mathf.PI * 2f) * floatAmount;
        Vector3 markerBasePosition = transform.position + transform.rotation * markerLocalOffset;
        visualTransform.position = markerBasePosition + Vector3.up * floatOffset;
        Camera camera = Camera.main;
        float runtimeWorldScale = markerWorldScale;
        if (maintainScreenSize && camera != null && !camera.orthographic)
        {
            float cameraDepth = camera.WorldToScreenPoint(markerBasePosition).z;
            float projectionY = camera.projectionMatrix.m11;
            float referenceProjectionY = 1f / Mathf.Tan(
                ScreenSizeReferenceFov * 0.5f * Mathf.Deg2Rad);
            if (cameraDepth > 0f && Mathf.Abs(projectionY) > 0.0001f)
            {
                runtimeWorldScale *= cameraDepth / ScreenSizeReferenceDistance;
                runtimeWorldScale *= referenceProjectionY / Mathf.Abs(projectionY);
            }
        }

        if (independentVisualTransform)
        {
            visualTransform.localScale = Vector3.one * runtimeWorldScale;
        }
        else
        {
            Vector3 lossy = visualTransform.parent != null
                ? visualTransform.parent.lossyScale
                : Vector3.one;
            visualTransform.localScale = new Vector3(
                runtimeWorldScale / SafeScale(lossy.x),
                runtimeWorldScale / SafeScale(lossy.y),
                runtimeWorldScale / SafeScale(lossy.z));
        }

        if (faceGameplayCamera && camera != null)
        {
            if (independentVisualTransform)
                visualTransform.rotation = camera.transform.rotation;
            else
                visualTransform.forward = camera.transform.forward;
        }

        if (secondaryVisualTransform != null)
        {
            secondaryVisualTransform.position = visualTransform.position;
            secondaryVisualTransform.rotation = visualTransform.rotation;
            secondaryVisualTransform.localScale = visualTransform.localScale;
            if (enableSecondaryChannel && spriteRenderer.enabled && secondarySpriteRenderer.enabled)
            {
                // Equal opposite camera-horizontal offsets keep the pair centered in screen space.
                float halfSeparation = ((spriteRenderer.sprite.bounds.size.x +
                    secondarySpriteRenderer.sprite.bounds.size.x) * 0.5f * runtimeWorldScale +
                    dualMarkerGap * runtimeWorldScale / Mathf.Max(0.001f, markerWorldScale)) * 0.5f;
                Vector3 right = camera != null ? camera.transform.right : transform.right;
                visualTransform.position -= right * halfSeparation;
                secondaryVisualTransform.position += right * halfSeparation;
            }
        }
    }

    private static float SafeScale(float value) => Mathf.Abs(value) < 0.0001f ? 1f : value;

    private void EnsureVisual()
    {
        if (visualTransform != null)
            return;

        Transform existing = transform.Find("NPCQuestMarkerVisual");
        GameObject visual = existing != null ? existing.gameObject : new GameObject("NPCQuestMarkerVisual");
        visualTransform = visual.transform;
        if (independentVisualTransform)
        {
            if (visualTransform.parent != null)
                visualTransform.SetParent(null, true);
            if (gameObject.scene.IsValid() && visual.scene != gameObject.scene)
                SceneManager.MoveGameObjectToScene(visual, gameObject.scene);
        }
        else if (visualTransform.parent != transform)
        {
            visualTransform.SetParent(transform, true);
        }
        spriteRenderer = visual.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 50;
        if (enableSecondaryChannel)
            EnsureSecondaryVisual();
    }

    private void EnsureSecondaryVisual()
    {
        if (secondaryVisualTransform != null)
            return;
        Transform existing = transform.Find("NPCQuestMarkerSecondaryVisual");
        GameObject visual = existing != null ? existing.gameObject :
            new GameObject("NPCQuestMarkerSecondaryVisual");
        secondaryVisualTransform = visual.transform;
        if (independentVisualTransform)
        {
            secondaryVisualTransform.SetParent(null, true);
            if (gameObject.scene.IsValid() && visual.scene != gameObject.scene)
                SceneManager.MoveGameObjectToScene(visual, gameObject.scene);
        }
        else
            secondaryVisualTransform.SetParent(transform, true);
        secondarySpriteRenderer = visual.GetComponent<SpriteRenderer>();
        if (secondarySpriteRenderer == null)
            secondarySpriteRenderer = visual.AddComponent<SpriteRenderer>();
        secondarySpriteRenderer.sortingOrder = 50;
    }

    private void CleanupIndependentVisual()
    {
        if (!independentVisualTransform)
            return;

        if (visualTransform != null)
        {
            GameObject visual = visualTransform.gameObject;
            visual.SetActive(false);
            Destroy(visual);
        }
        visualTransform = null;
        spriteRenderer = null;
        displayedSprite = null;
        if (secondaryVisualTransform != null)
        {
            secondaryVisualTransform.gameObject.SetActive(false);
            Destroy(secondaryVisualTransform.gameObject);
        }
        secondaryVisualTransform = null;
        secondarySpriteRenderer = null;
    }

    private void OnStoryFlagChanged(string _, bool __) => Refresh();

    private void SubscribeToConversationManager()
    {
        conversationTrigger = GetComponent<NPCConversationTrigger>();
        DialogueEditor.ConversationManager.OnConversationStarted += OnConversationStarted;
    }

    private void UnsubscribeFromConversationManager()
    {
        DialogueEditor.ConversationManager.OnConversationStarted -= OnConversationStarted;
        conversationTrigger = null;
    }

    private void OnConversationStarted()
    {
        if (conversationTrigger == null || !conversationTrigger.IsTalking)
            return;

        dialogueSuppressed = true;
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
        if (secondarySpriteRenderer != null)
            secondarySpriteRenderer.enabled = false;
    }

    private System.Collections.IEnumerator RefreshAfterDialogueCloses()
    {
        while (true)
        {
            DialogueEditor.ConversationManager manager =
                DialogueEditor.ConversationManager.Instance;
            while ((manager != null && manager.isActiveAndEnabled && manager.IsConversationActive) ||
                   StorySequenceCoordinator.IsStorySequenceActive)
                yield return null;

            // NPC stage changes are handled from OnConversationEnded; this also lets
            // post-close sequence coroutines finish before evaluating the new state.
            yield return null;
            yield return null;

            manager = DialogueEditor.ConversationManager.Instance;
            if ((manager != null && manager.isActiveAndEnabled && manager.IsConversationActive) ||
                StorySequenceCoordinator.IsStorySequenceActive)
                continue;

            dialogueSuppressed = false;
            dialogueCloseRoutine = null;
            Refresh();
            yield break;
        }
    }

    private void OnStageChanged(string _, string __) => Refresh();

    private void SubscribeToTaskManager()
    {
        TaskManager current = TaskManager.Instance;
        if (subscribedTaskManager == current)
            return;

        UnsubscribeFromTaskManager();
        subscribedTaskManager = current;
        if (subscribedTaskManager == null)
            return;

        subscribedTaskManager.TaskChanged += Refresh;
        subscribedTaskManager.StageChanged += OnStageChanged;
        Refresh();
    }

    private void UnsubscribeFromTaskManager()
    {
        if (subscribedTaskManager == null)
            return;

        subscribedTaskManager.TaskChanged -= Refresh;
        subscribedTaskManager.StageChanged -= OnStageChanged;
        subscribedTaskManager = null;
    }

    private void Refresh()
    {
        if (spriteRenderer == null)
            return;

        if (dialogueSuppressed)
        {
            spriteRenderer.enabled = false;
            if (secondarySpriteRenderer != null)
                secondarySpriteRenderer.enabled = false;
            return;
        }

        Sprite next = null;
        bool visible = showMarker && rules != null;
        if (visible)
        {
            foreach (MarkerRule rule in rules)
            {
                if (rule == null || !Matches(rule))
                    continue;
                next = GetSprite(rule);
                if (next == null)
                    visible = false;
                break;
            }
        }

        spriteRenderer.enabled = visible && next != null;
        if (next != displayedSprite)
        {
            spriteRenderer.sprite = next;
            displayedSprite = next;
        }
        if (secondarySpriteRenderer != null)
        {
            Sprite secondary = null;
            if (showMarker && enableSecondaryChannel && secondaryRules != null)
                foreach (MarkerRule rule in secondaryRules)
                    if (rule != null && Matches(rule))
                    {
                        secondary = GetSprite(rule);
                        break;
                    }
            secondarySpriteRenderer.sprite = secondary;
            secondarySpriteRenderer.enabled = secondary != null;
        }
    }

    private bool Matches(MarkerRule rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.BlockingStoryFlagId) &&
            SessionStoryState.GetFlag(rule.BlockingStoryFlagId))
            return false;
        if (!string.IsNullOrWhiteSpace(rule.RequiredStoryFlagId) &&
            SessionStoryState.GetFlag(rule.RequiredStoryFlagId) != rule.ExpectedStoryFlagValue)
            return false;

        if (string.IsNullOrWhiteSpace(rule.TaskId))
            return rule.TaskState == TaskStateRequirement.Any &&
                   string.IsNullOrWhiteSpace(rule.RequiredStageId);

        TaskManager manager = TaskManager.Instance;
        if (manager == null)
            return false;

        TaskState state = manager.GetTaskState(rule.TaskId);
        if (!MatchesState(state, rule.TaskState))
            return false;
        if (state == TaskState.Completed && rule.HideWhenTaskCompleted)
            return false;
        if (!string.IsNullOrWhiteSpace(rule.RequiredStageId) &&
            (state != TaskState.Active || !manager.IsCurrentStage(rule.TaskId, rule.RequiredStageId)))
            return false;

        return string.IsNullOrWhiteSpace(rule.MinimumStageId) ||
               (state == TaskState.Active && manager.IsCurrentStageAtOrAfter(rule.TaskId, rule.MinimumStageId));
    }

    private static bool MatchesState(TaskState state, TaskStateRequirement requirement)
    {
        return requirement == TaskStateRequirement.Any ||
               (requirement == TaskStateRequirement.Inactive && state == TaskState.Inactive) ||
               (requirement == TaskStateRequirement.Active && state == TaskState.Active) ||
               (requirement == TaskStateRequirement.Completed && state == TaskState.Completed);
    }

    private Sprite GetSprite(MarkerRule rule)
    {
        switch (rule.Appearance)
        {
            case MarkerAppearance.YellowAvailable: return yellowAvailable;
            case MarkerAppearance.YellowTarget: return yellowTarget;
            case MarkerAppearance.BlueAvailable: return blueAvailable;
            case MarkerAppearance.BlueTarget: return blueTarget;
            case MarkerAppearance.GrayAvailable: return grayAvailable;
            default:
                return grayTarget;
        }
    }
}
