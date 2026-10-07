using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class TaskNotificationUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text headingText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField, Min(0f)] private float fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float visibleDuration = 2.75f;

    [SerializeField] private AudioSource taskNotificationAudioSource;
    private TaskAudioConfig taskAudioConfig;
    private readonly Queue<NotificationRequest> requests = new Queue<NotificationRequest>();
    private bool isPresenting;
    private PresentationPhase phase;
    private float phaseElapsed;

    private enum PresentationPhase { FadeIn, Hold, FadeOut }

    private sealed class NotificationRequest
    {
        public readonly string Heading;
        public readonly string Body;
        public readonly AudioClip Sound;
        public readonly Action OnFinished;
        public bool SoundPlayed;

        public NotificationRequest(string heading, string body, AudioClip sound, Action onFinished)
        {
            Heading = heading;
            Body = body;
            Sound = sound;
            OnFinished = onFinished;
        }
    }

    private void Awake()
    {
        taskAudioConfig = Resources.Load<TaskAudioConfig>("TaskAudioConfig");
        if (taskNotificationAudioSource == null)
            taskNotificationAudioSource = GetComponent<AudioSource>();
        if (taskNotificationAudioSource == null)
            taskNotificationAudioSource = gameObject.AddComponent<AudioSource>();

        taskNotificationAudioSource.spatialBlend = 0f;
        taskNotificationAudioSource.playOnAwake = false;
        taskNotificationAudioSource.loop = false;
        taskNotificationAudioSource.volume = 1f;
        taskNotificationAudioSource.clip = null;

        SetVisibleAmount(0f);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        BindToManager();
    }

    private void Start()
    {
        BindToManager();
    }

    private void OnDisable()
    {
        InterruptPresentation();
        TaskManager.Instance?.UnregisterNotificationUI(this);
    }

    private static bool IsPresentationSafe()
    {
        return !StorySequenceCoordinator.IsStorySequenceActive &&
            (DialogueEditor.ConversationManager.Instance == null ||
             !DialogueEditor.ConversationManager.Instance.IsConversationActive);
    }

    private void Update()
    {
        if (!IsPresentationSafe())
        {
            InterruptPresentation();
            return;
        }

        if (!isPresenting)
        {
            TryStartPresentation();
            return;
        }

        phaseElapsed += Time.unscaledDeltaTime;
        switch (phase)
        {
            case PresentationPhase.FadeIn:
                SetVisibleAmount(fadeDuration <= 0f ? 1f : Mathf.Clamp01(phaseElapsed / fadeDuration));
                if (phaseElapsed >= fadeDuration)
                {
                    phase = PresentationPhase.Hold;
                    phaseElapsed = 0f;
                }
                break;
            case PresentationPhase.Hold:
                if (phaseElapsed >= visibleDuration)
                {
                    phase = PresentationPhase.FadeOut;
                    phaseElapsed = 0f;
                }
                break;
            case PresentationPhase.FadeOut:
                SetVisibleAmount(fadeDuration <= 0f ? 0f : 1f - Mathf.Clamp01(phaseElapsed / fadeDuration));
                if (phaseElapsed >= fadeDuration)
                {
                    // Remove only after successful presentation, before calling user code.
                    NotificationRequest completed = requests.Dequeue();
                    isPresenting = false;
                    completed.OnFinished?.Invoke();
                }
                break;
        }
    }

    private void LateUpdate()
    {
        // Catch ownership acquired later in this frame before the canvas renders.
        if (!IsPresentationSafe())
            InterruptPresentation();
    }

    private void TryStartPresentation()
    {
        if (!isActiveAndEnabled || isPresenting || requests.Count == 0 || !IsPresentationSafe())
            return;

        NotificationRequest request = requests.Peek();
        headingText.text = request.Heading;
        detailText.text = request.Body;
        phase = fadeDuration <= 0f ? PresentationPhase.Hold : PresentationPhase.FadeIn;
        phaseElapsed = 0f;
        isPresenting = true;
        SetVisibleAmount(fadeDuration <= 0f ? 1f : 0f);
        if (!request.SoundPlayed)
        {
            PlayNotificationSound(request.Sound);
            request.SoundPlayed = true;
        }
    }

    private void InterruptPresentation()
    {
        // Keep the front request pending. Retry its full authored duration later.
        isPresenting = false;
        phaseElapsed = 0f;
        SetVisibleAmount(0f);
        if (taskNotificationAudioSource != null)
            taskNotificationAudioSource.Stop();
    }

    private void BindToManager()
    {
        TaskManager.Instance?.RegisterNotificationUI(this);
    }

    public void ShowTaskStarted(TaskType taskType, Action onFinished = null)
    {
        AudioClip sound = taskAudioConfig == null
            ? null
            : taskType == TaskType.Main
                ? taskAudioConfig.MainTaskReceivedSound
                : taskAudioConfig.SideTaskReceivedSound;
        ShowNotification(GetHeading(taskType, "OBTAINED"), sound, onFinished);
    }

    public void ShowTaskUpdated(TaskType taskType, Action onFinished = null)
    {
        ShowNotification(
            GetHeading(taskType, "UPDATED"),
            taskAudioConfig != null ? taskAudioConfig.TaskUpdatedSound : null,
            onFinished);
    }

    public void ShowTaskCompleted(TaskType taskType, Action onFinished = null)
    {
        AudioClip sound = taskAudioConfig == null
            ? null
            : taskType == TaskType.Main
                ? taskAudioConfig.MainTaskCompletedSound
                : taskAudioConfig.SideTaskCompletedSound;
        ShowNotification(GetHeading(taskType, "COMPLETED"), sound, onFinished);
    }

    private void ShowNotification(
        string heading,
        AudioClip sound,
        Action onFinished)
    {
        requests.Enqueue(new NotificationRequest(heading, string.Empty, sound, onFinished));
        TryStartPresentation();
    }

    private static string GetHeading(TaskType taskType, string status)
    {
        string typeLabel = taskType == TaskType.Main ? "PANGUNAHING GAWAIN" : "KARAGDAGANG GAWAIN";
        switch (status)
        {
            case "OBTAINED": return "NATANGGAP ANG " + typeLabel;
            case "UPDATED": return "MAY BAGONG LAYUNIN SA " + typeLabel;
            case "COMPLETED": return "NATAPOS ANG " + typeLabel;
            default: return typeLabel + " " + status;
        }
    }

    private void PlayNotificationSound(AudioClip clip)
    {
        if (clip == null || taskNotificationAudioSource == null ||
            taskAudioConfig == null)
        {
            return;
        }

        taskNotificationAudioSource.PlayOneShot(
            clip,
            Mathf.Clamp01(taskAudioConfig.UIVolume));
    }

    private void SetVisibleAmount(float amount)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = amount;
    }
}
