using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class IrisTransitionController : MonoBehaviour
{
    public static IrisTransitionController Instance { get; private set; }

    [SerializeField, Min(0.01f)] private float closeDuration = 0.75f;
    [SerializeField, Min(0.01f)] private float openDuration = 0.75f;
    [SerializeField] private Vector2 irisCenter = new Vector2(0.5f, 0.5f);
    [SerializeField, Min(0f)] private float coveredHoldDuration = 0f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private Image overlayImage;

    private Material runtimeMaterial;
    private Coroutine transitionRoutine;
    private bool isCovered;

    public bool IsCovered => isCovered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
        SetRadius(1.5f);
        SetOverlayVisible(false);
    }

    public Coroutine TransitionToScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || transitionRoutine != null)
            return null;

        transitionRoutine = StartCoroutine(TransitionRoutine(sceneName.Trim()));
        return transitionRoutine;
    }

    public void CoverImmediately()
    {
        EnsureOverlay();
        SetOverlayVisible(true);
        SetRadius(0f);
        isCovered = true;
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        EnsureOverlay();
        SetOverlayVisible(true);
        yield return AnimateRadius(1.5f, 0f, closeDuration);
        isCovered = true;

        if (coveredHoldDuration > 0f)
            yield return Wait(coveredHoldDuration);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (load != null)
        {
            while (!load.isDone)
                yield return null;
        }

        yield return null;
        yield return AnimateRadius(0f, 1.5f, openDuration);
        isCovered = false;
        SetOverlayVisible(false);
        transitionRoutine = null;
    }

    private IEnumerator AnimateRadius(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetRadius(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            SetRadius(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetRadius(to);
    }

    private IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas == null)
        {
            GameObject canvasObject = new GameObject("IrisTransitionOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            overlayCanvas = canvasObject.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 32000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        if (overlayImage == null)
        {
            GameObject imageObject = new GameObject("Iris", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(overlayCanvas.transform, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlayImage = imageObject.GetComponent<Image>();
            Shader shader = Shader.Find("UI/ClarityIrisTransition");
            if (shader != null)
            {
                runtimeMaterial = new Material(shader) { name = "ClarityIrisTransition (Runtime)" };
                overlayImage.material = runtimeMaterial;
            }
            overlayImage.color = Color.white;
            overlayImage.raycastTarget = true;
        }

        if (runtimeMaterial == null && overlayImage.material != null)
            runtimeMaterial = new Material(overlayImage.material);
    }

    private void SetRadius(float radius)
    {
        EnsureOverlay();
        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat("_Radius", radius);
            runtimeMaterial.SetVector("_Center", irisCenter);
        }
    }

    private void SetOverlayVisible(bool visible)
    {
        if (overlayCanvas != null)
            overlayCanvas.enabled = visible;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);

        runtimeMaterial = null;
    }
}
