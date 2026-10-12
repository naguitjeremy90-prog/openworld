using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public sealed class SceneMenuUISoundPlayer : MonoBehaviour
{
    private static SceneMenuUISoundPlayer instance;

    [Header("UI Clips")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    [Header("Relative Volumes")]
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float clickVolume = 0.55f;

    private AudioSource audioSource;
    private Coroutine transitionCleanup;

    public static SceneMenuUISoundPlayer Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // A SceneMenu transition player may still be finishing its outgoing click.
            // Let the newly loaded scene take over so its UI remains usable while the old
            // one-shot source cleans itself up.
            if (instance.transitionCleanup == null)
            {
                Destroy(gameObject);
                return;
            }
        }

        instance = this;
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        audioSource.ignoreListenerVolume = false;

    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void PlayHover()
    {
        Play(hoverClip, hoverVolume);
    }

    public void PlayClick(bool persistThroughSceneLoad)
    {
        if (clickClip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clickClip, Mathf.Clamp01(clickVolume));

        if (!persistThroughSceneLoad || transitionCleanup != null)
            return;

        Scene startingScene = SceneManager.GetActiveScene();
        DontDestroyOnLoad(gameObject);
        transitionCleanup = StartCoroutine(CleanupAfterTransition(startingScene));
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private IEnumerator CleanupAfterTransition(Scene startingScene)
    {
        // SceneMenu's transition buttons use LoadScene. Keep the source across that handoff,
        // but return it to SceneMenu if a transition fails to happen promptly.
        float transitionDeadline = Time.unscaledTime + 5f;
        while (SceneManager.GetActiveScene().path == startingScene.path &&
               Time.unscaledTime < transitionDeadline)
        {
            yield return null;
        }

        if (SceneManager.GetActiveScene().path != startingScene.path)
        {
            while (audioSource != null && audioSource.isPlaying)
                yield return null;

            Destroy(gameObject);
            yield break;
        }

        if (startingScene.IsValid() && startingScene.isLoaded)
            SceneManager.MoveGameObjectToScene(gameObject, startingScene);

        transitionCleanup = null;
    }
}