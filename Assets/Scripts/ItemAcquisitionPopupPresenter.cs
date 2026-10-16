using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ItemAcquisitionPopupDestination
{
    None,
    Inventory,
    Journal
}

/// <summary>Queues and animates generic item discovery presentations.</summary>
[DisallowMultipleComponent]
public sealed class ItemAcquisitionPopupPresenter : MonoBehaviour
{
    private struct PresentationRequest
    {
        public Sprite Icon;
        public string DisplayName;
        public ItemAcquisitionPopupDestination Destination;

        public PresentationRequest(
            Sprite icon,
            string displayName,
            ItemAcquisitionPopupDestination destination)
        {
            Icon = icon;
            DisplayName = displayName;
            Destination = destination;
        }
    }

    [Header("Visual References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private RectTransform inventoryHudTarget;
    [SerializeField] private RectTransform journalHudTarget;
    [SerializeField] private GameObject confettiPrefab;
    [SerializeField, Min(0.1f)] private float confettiCameraDistance = 3f;
    [SerializeField] private AudioClip popupSfx;

    [Header("Reveal Animation")]
    [SerializeField, Min(0f)] private float startScale = 0.70f;
    [SerializeField, Min(1f)] private float peakScale = 1.12f;
    [SerializeField, Min(0f)] private float settledScale = 1f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float growDuration = 0.20f;
    [SerializeField, Min(0f)] private float settleDuration = 0.16f;
    [SerializeField, Min(0f)] private float holdDuration = 1.5f;

    [Header("Exit Animation")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.30f;
    [SerializeField, Min(0.01f)] private float inventoryTravelDuration = 0.48f;
    [SerializeField, Range(0f, 1f)] private float travelEndScale = 0.24f;

    private readonly Queue<PresentationRequest> requests = new Queue<PresentationRequest>();
    private Canvas parentCanvas;
    private AudioSource audioSource;
    private Coroutine presentationRoutine;
    private Vector3 baseScale = Vector3.one;
    private Vector2 homePosition;
    private Transform confettiContainer;
    private GameObject confettiInstance;
    private ParticleSystem[] confettiSystems;

    private void Awake()
    {
        if (popupRoot == null)
            popupRoot = transform as RectTransform;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (itemIcon == null)
            itemIcon = GetComponentInChildren<Image>(true);
        if (itemName == null)
            itemName = GetComponentInChildren<TMP_Text>(true);
        parentCanvas = GetComponentInParent<Canvas>();
        audioSource = GetComponent<AudioSource>();
        if (popupSfx != null && audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        if (audioSource != null)
            audioSource.playOnAwake = false;

        if (itemIcon != null)
            itemIcon.preserveAspect = true;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (popupRoot != null)
        {
            baseScale = popupRoot.localScale;
            homePosition = popupRoot.anchoredPosition;
        }
    }

    private void OnEnable()
    {
        if (requests.Count > 0 && presentationRoutine == null)
            presentationRoutine = StartCoroutine(ProcessQueue());
    }

    /// <summary>Adds a presentation to the FIFO queue. This component never grants items.</summary>
    public void Enqueue(Sprite icon, string displayName, bool flyToInventory)
    {
        Enqueue(icon, displayName, flyToInventory
            ? ItemAcquisitionPopupDestination.Inventory
            : ItemAcquisitionPopupDestination.None);
    }

    /// <summary>Adds a presentation and its visual destination to the FIFO queue.</summary>
    public void Enqueue(
        Sprite icon,
        string displayName,
        ItemAcquisitionPopupDestination destination)
    {
        requests.Enqueue(new PresentationRequest(icon, displayName, destination));
        if (presentationRoutine == null && isActiveAndEnabled)
            presentationRoutine = StartCoroutine(ProcessQueue());
        else if (!gameObject.activeSelf)
            gameObject.SetActive(true);
    }

    private IEnumerator ProcessQueue()
    {
        while (requests.Count > 0)
        {
            while (Time.timeScale <= 0f)
                yield return null;

            PresentationRequest request = requests.Dequeue();
            PlayConfetti();
            if (itemIcon != null)
                itemIcon.sprite = request.Icon;
            if (itemName != null)
            {
                itemName.text = request.DisplayName ?? string.Empty;
                itemName.gameObject.SetActive(!string.IsNullOrWhiteSpace(request.DisplayName));
            }
            if (popupRoot != null)
            {
                popupRoot.anchoredPosition = homePosition;
                popupRoot.localScale = baseScale * startScale;
            }
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
            if (popupSfx != null && audioSource != null)
                audioSource.PlayOneShot(popupSfx);

            float revealDuration = Mathf.Max(fadeInDuration, growDuration);
            float revealElapsed = 0f;
            while (revealElapsed < revealDuration)
            {
                if (Time.timeScale <= 0f)
                {
                    yield return null;
                    continue;
                }
                revealElapsed += Time.unscaledDeltaTime;
                float growT = EaseOutCubic(growDuration <= 0f ? 1f : revealElapsed / growDuration);
                float fadeT = EaseOutCubic(fadeInDuration <= 0f ? 1f : revealElapsed / fadeInDuration);
                if (popupRoot != null)
                    popupRoot.localScale = baseScale * Mathf.Lerp(startScale, peakScale, growT);
                if (canvasGroup != null)
                    canvasGroup.alpha = fadeT;
                yield return null;
            }

            yield return AnimateScale(peakScale, settledScale, settleDuration);
            if (popupRoot != null)
                popupRoot.localScale = baseScale * settledScale;
            yield return WaitUnscaled(holdDuration);

            RectTransform target = GetDestinationTarget(request.Destination);
            if (target != null && TryGetTargetPosition(target, out Vector2 targetPosition))
                yield return AnimateTravel(targetPosition);
            else
                yield return AnimateFadeOut();

            ResetVisual();
        }

        presentationRoutine = null;
        gameObject.SetActive(false);
    }

    private IEnumerator AnimateScale(float from, float to, float duration)
    {
        if (duration <= 0f)
            yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutCubic(elapsed / duration);
            if (popupRoot != null)
                popupRoot.localScale = baseScale * Mathf.Lerp(from, to, t);
            yield return null;
        }
    }

    private IEnumerator AnimateFadeOut()
    {
        if (fadeOutDuration <= 0f)
            yield break;
        float elapsed = 0f;
        float initialAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        while (elapsed < fadeOutDuration)
        {
            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }
            elapsed += Time.unscaledDeltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(initialAlpha, 0f, EaseInOut(elapsed / fadeOutDuration));
            yield return null;
        }
    }

    private IEnumerator AnimateTravel(Vector2 targetPosition)
    {
        float duration = Mathf.Max(0.01f, inventoryTravelDuration);
        Vector2 startPosition = popupRoot != null ? popupRoot.anchoredPosition : homePosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Time.timeScale <= 0f)
            {
                yield return null;
                continue;
            }
            elapsed += Time.unscaledDeltaTime;
            float rawT = Mathf.Clamp01(elapsed / duration);
            float t = EaseInOut(rawT);
            if (popupRoot != null)
            {
                popupRoot.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
                popupRoot.localScale = baseScale * Mathf.Lerp(settledScale, travelEndScale, t);
            }
            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, Mathf.InverseLerp(0.60f, 1f, rawT));
            yield return null;
        }
    }

    private void PlayConfetti()
    {
        if (confettiPrefab == null)
            return;

        Camera effectCamera = GetEffectCamera();
        if (effectCamera == null)
            return;

        if (confettiContainer == null)
        {
            GameObject container = new GameObject("Acquisition Confetti");
            container.SetActive(false);
            confettiContainer = container.transform;
            confettiContainer.SetParent(effectCamera.transform, false);
            confettiInstance = Instantiate(confettiPrefab, confettiContainer, false);
            confettiInstance.name = confettiPrefab.name + " (Popup Instance)";
            ParticleSystem[] allSystems =
                confettiInstance.GetComponentsInChildren<ParticleSystem>(true);
            List<ParticleSystem> activeSystems = new List<ParticleSystem>(allSystems.Length);

            foreach (ParticleSystem particleSystem in allSystems)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.playOnAwake = false;
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                // The source prefab includes two oversized circular flash emitters.
                // Disable them only on this instance so the popup gets confetti without a disk.
                if (particleSystem.gameObject == confettiInstance ||
                    particleSystem.gameObject.name == "circle")
                {
                    ParticleSystemRenderer renderer =
                        particleSystem.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                        renderer.enabled = false;
                    continue;
                }

                activeSystems.Add(particleSystem);
            }

            confettiSystems = activeSystems.ToArray();
        }
        else if (confettiContainer.parent != effectCamera.transform)
        {
            confettiContainer.SetParent(effectCamera.transform, false);
        }

        Vector2 screenPosition = popupRoot != null
            ? RectTransformUtility.WorldToScreenPoint(
                parentCanvas != null && parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : effectCamera,
                popupRoot.position)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float depth = Mathf.Max(confettiCameraDistance, effectCamera.nearClipPlane + 0.1f);
        Vector3 worldPosition = effectCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth));

        confettiContainer.localPosition =
            effectCamera.transform.InverseTransformPoint(worldPosition);
        confettiContainer.localRotation = Quaternion.identity;
        confettiContainer.localScale = Vector3.one;
        confettiContainer.gameObject.SetActive(true);
        confettiInstance.SetActive(true);

        foreach (ParticleSystem particleSystem in confettiSystems)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(false);
        }
    }

    private Camera GetEffectCamera()
    {
        if (parentCanvas != null &&
            parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay &&
            parentCanvas.worldCamera != null)
        {
            return parentCanvas.worldCamera;
        }

        return Camera.main;
    }

    private RectTransform GetDestinationTarget(ItemAcquisitionPopupDestination destination)
    {
        switch (destination)
        {
            case ItemAcquisitionPopupDestination.Inventory:
                return inventoryHudTarget;
            case ItemAcquisitionPopupDestination.Journal:
                return journalHudTarget != null
                    ? journalHudTarget
                    : ReconstructionJournalManager.Instance != null
                        ? ReconstructionJournalManager.Instance.OpenButtonRectTransform
                        : null;
            default:
                return null;
        }
    }

    private bool TryGetTargetPosition(RectTransform target, out Vector2 localPosition)
    {
        localPosition = homePosition;
        if (target == null || popupRoot == null)
            return false;
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            return false;

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect == null)
            return false;
        Camera canvasCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : parentCanvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, target.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, canvasCamera, out localPosition);
    }

    private IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (Time.timeScale > 0f)
                elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ResetVisual()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (popupRoot != null)
        {
            popupRoot.anchoredPosition = homePosition;
            popupRoot.localScale = baseScale;
        }
    }

    private void OnDestroy()
    {
        if (confettiContainer != null)
            Destroy(confettiContainer.gameObject);
    }

    private static float EaseOutCubic(float value)
    {
        float t = Mathf.Clamp01(value);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseInOut(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }
}
