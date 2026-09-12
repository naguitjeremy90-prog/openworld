using System.Collections;
using UnityEngine;

/// <summary>Generic registration bridge for any normal exploration HUD element.</summary>
[DisallowMultipleComponent]
public sealed class GameplayHUDTarget : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject visualRoot;
    [SerializeField, Min(0f)] private float fadeDuration = 0.2f;
    [SerializeField] private bool available = true;

    private Coroutine fadeRoutine;
    private bool presentationHidden;
    private bool wasActive;
    private float previousAlpha = 1f;
    private bool previousInteractable;
    private bool previousBlocksRaycasts;

    public bool IsAvailable
    {
        get { return available; }
        set
        {
            available = value;
            if (!StorySequenceCoordinator.IsStorySequenceActive && !available)
                ApplyPresentationState(true, false);
            else if (!StorySequenceCoordinator.IsStorySequenceActive && available && presentationHidden)
                ApplyPresentationState(false, false);
        }
    }

    public static GameplayHUDTarget AttachTo(GameObject root)
    {
        if (root == null)
            return null;

        GameplayHUDTarget target = root.GetComponent<GameplayHUDTarget>();
        if (target == null)
            target = root.AddComponent<GameplayHUDTarget>();

        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
            group = root.AddComponent<CanvasGroup>();
        target.Configure(group, root);
        return target;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        StorySequenceCoordinator coordinator = StorySequenceCoordinator.Instance;
        if (coordinator != null)
            coordinator.Register(this);
    }

    private void OnDisable()
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);
        fadeRoutine = null;

        StorySequenceCoordinator coordinator;
        if (StorySequenceCoordinator.TryGetExisting(out coordinator))
            coordinator.Unregister(this);
    }

    private void LateUpdate()
    {
        if (!presentationHidden || canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void Configure(CanvasGroup group, GameObject root = null)
    {
        canvasGroup = group != null ? group : canvasGroup;
        visualRoot = root != null ? root : visualRoot;
        ResolveReferences();
        if (StorySequenceCoordinator.IsStorySequenceActive)
            ApplyPresentationState(true, false);
    }

    internal void ApplyPresentationState(bool hidden, bool animate)
    {
        ResolveReferences();

        if (hidden)
        {
            if (!presentationHidden)
            {
                presentationHidden = true;
                wasActive = visualRoot == null || visualRoot.activeSelf;
                previousAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
                previousInteractable = canvasGroup != null && canvasGroup.interactable;
                previousBlocksRaycasts = canvasGroup != null && canvasGroup.blocksRaycasts;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            FadeTo(0f, animate);
            return;
        }

        if (!presentationHidden)
            return;

        presentationHidden = false;
        if (!available || !wasActive)
        {
            FadeTo(0f, false);
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.interactable = previousInteractable;
            canvasGroup.blocksRaycasts = previousBlocksRaycasts;
        }

        FadeTo(previousAlpha, animate);
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (visualRoot == null)
            visualRoot = gameObject;

        if (canvasGroup == null && visualRoot == gameObject)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void FadeTo(float target, bool animate)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        if (canvasGroup == null)
            return;

        if (!animate || fadeDuration <= 0f)
        {
            canvasGroup.alpha = target;
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(target));
    }

    private IEnumerator FadeRoutine(float target)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = target;
        fadeRoutine = null;
    }
}
