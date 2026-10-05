using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>Plays a cinematic bridge and hands its story presentation to the destination intro.</summary>
public sealed class TransportationSceneController : MonoBehaviour
{
    private const string TransportDestinationStateId = "makamisa_transport_destination";
    private const string Part1PosadaDestination = "posada_part1";
    private const string Part1PosadaSceneName = "PosadaRoom";

    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private PlayableDirector cinematicDirector;
    [SerializeField] private AudioClip miguelShoutClip;
    [SerializeField, Min(0f)] private float miguelShoutDelay = 1.5f;
    [SerializeField, Min(1)] private int miguelShoutCount = 3;
    [SerializeField, Min(0f)] private float miguelShoutPause = 0.5f;
    [SerializeField, Min(0.1f)] private float prepareWaitTimeout = 2f;
    [SerializeField] private string destinationSceneName = "ChurchNEWMAKAMISA";
    [SerializeField] private string destinationIntroDirectorName = "Introtimeline";

    private StorySequenceToken transportToken;
    private StorySequenceToken destinationIntroToken;
    private PlayableDirector destinationIntroDirector;
    private AudioSource miguelShoutAudioSource;
    private bool videoPrepared;
    private bool videoFailed;
    private bool cinematicStarted;
    private bool miguelShoutSequenceStarted;
    private bool leavingScene;
    private bool part1PosadaRoute;
    private string activeDestinationSceneName;
    private float playbackStartedAt;

    private void Awake()
    {
        transportToken = StorySequenceCoordinator.Acquire(this);
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;

        GameObject shoutAudioObject = new GameObject("MiguelShoutAudio");
        miguelShoutAudioSource = shoutAudioObject.AddComponent<AudioSource>();

        miguelShoutAudioSource.playOnAwake = false;
        miguelShoutAudioSource.loop = false;
        miguelShoutAudioSource.volume = 1f;
        miguelShoutAudioSource.spatialBlend = 0f;
        miguelShoutAudioSource.clip = null;

        if (videoPlayer == null)
            videoPlayer = FindAnyObjectByType<VideoPlayer>();

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            // Avoid frame skipping while decoding this short cinematic. The
            // affected Unity 6000.4.4 editor can overflow the VideoPlayer audio
            // provider when Skip On Drop is enabled.
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.GameTime;
            videoPlayer.skipOnDrop = false;
            videoPlayer.targetCameraAlpha = 0f;
            videoPlayer.prepareCompleted += HandleVideoPrepared;
            videoPlayer.started += HandleVideoStarted;
            videoPlayer.loopPointReached += HandleVideoFinished;
            videoPlayer.errorReceived += HandleVideoError;
        }
    }

    private IEnumerator Start()
    {
        if (videoPlayer == null || videoPlayer.clip == null)
        {
            Debug.LogError("Transportation scene is missing its VideoPlayer or clip.", this);
            yield break;
        }

        if (videoPlayer.isPlaying)
            videoPlayer.Stop();

        // A new scene player already starts at time zero. Setting time before
        // preparation can be rejected by platform decoders that cannot seek yet.
        videoPlayer.Prepare();

        float prepareDeadline = Time.realtimeSinceStartup + prepareWaitTimeout;
        while (!videoPrepared && !videoFailed &&
               Time.realtimeSinceStartup < prepareDeadline)
            yield return null;

        if (videoFailed)
            yield break;

        if (!videoPrepared)
        {
            Debug.LogWarning(
                "Transportation video did not report prepareCompleted in time; " +
                "starting playback and letting VideoPlayer finish preparation.",
                this);
        }

        // The incoming iris opens onto the camera's black clear color. Start the
        // clip only after it has finished opening, so its beginning is not lost.
        IrisTransitionController iris = IrisTransitionController.Instance;
        while (iris != null && iris.IsCovered)
            yield return null;

        videoPlayer.targetCameraAlpha = 1f;
        videoPlayer.Play();
        playbackStartedAt = Time.realtimeSinceStartup;

        // The VideoPlayer.started event is the normal synchronization point.
        // Also wait for isPlaying in case a platform misses that event.
        while (!videoPlayer.isPlaying && !videoFailed)
            yield return null;

        if (videoPlayer.isPlaying)
            StartCinematic();

        // loopPointReached is the normal exit. Some platform decoders stop
        // without sending it, so also watch for the end of a non-looping clip.
        while (!leavingScene && !videoFailed)
        {
            if (Time.realtimeSinceStartup - playbackStartedAt > 1f &&
                !videoPlayer.isPlaying &&
                videoPlayer.time >= videoPlayer.clip.length - 0.25d)
                FinishVideo();
            yield return null;
        }
    }

    private void HandleVideoPrepared(VideoPlayer source)
    {
        if (source.audioTrackCount > 0)
        {
            source.EnableAudioTrack(0, true);
            if (source.canSetDirectAudioVolume)
                source.SetDirectAudioVolume(0, 1f);
        }

        videoPrepared = true;
    }

    private void HandleVideoFinished(VideoPlayer source)
    {
        FinishVideo();
    }

    private void HandleVideoStarted(VideoPlayer source)
    {
        StartCinematic();
    }

    private void StartCinematic()
    {
        if (cinematicStarted)
            return;

        if (cinematicDirector == null)
            return;

        cinematicStarted = true;
        cinematicDirector.time = 0d;
        cinematicDirector.Play();
        if (!miguelShoutSequenceStarted)
        {
            miguelShoutSequenceStarted = true;
            StartCoroutine(PlayMiguelShoutsAfterDelay());
        }
    }

    private IEnumerator PlayMiguelShoutsAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, miguelShoutDelay));

        if (miguelShoutClip == null || miguelShoutAudioSource == null)
        {
            yield break;
        }

        int shoutCount = Mathf.Max(1, miguelShoutCount);
        for (int shoutIndex = 0; shoutIndex < shoutCount; shoutIndex++)
        {
            if (leavingScene)
                yield break;

            miguelShoutAudioSource.PlayOneShot(miguelShoutClip);

            if (shoutIndex >= shoutCount - 1)
                yield break;

            while (miguelShoutAudioSource.isPlaying)
            {
                if (leavingScene)
                    yield break;

                yield return null;
            }

            if (leavingScene)
                yield break;

            yield return new WaitForSecondsRealtime(Mathf.Max(0f, miguelShoutPause));
        }
    }

    private void FinishVideo()
    {
        if (leavingScene)
            return;

        leavingScene = true;
        videoPlayer.targetCameraAlpha = 0f;
        videoPlayer.Stop(); // The camera keeps the screen black after the last frame.

        IrisTransitionController iris = IrisTransitionController.Instance;
        if (iris == null)
            iris = new GameObject("IrisTransitionController").AddComponent<IrisTransitionController>();

        part1PosadaRoute = SessionStoryState.GetString(TransportDestinationStateId) ==
                           Part1PosadaDestination;
        activeDestinationSceneName = part1PosadaRoute
            ? Part1PosadaSceneName
            : destinationSceneName;

        if (iris.TransitionToScene(activeDestinationSceneName, transportToken) == null)
        {
            Debug.LogError("Transportation scene could not start the destination transition.", this);
            leavingScene = false;
            part1PosadaRoute = false;
            activeDestinationSceneName = null;
            return;
        }

        // IrisTransitionController now owns release of this token after opening.
        transportToken = null;
    }

    private void HandleVideoError(VideoPlayer source, string message)
    {
        videoFailed = true;
        Debug.LogError("Transportation video playback failed: " + message, this);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!leavingScene || scene.name != activeDestinationSceneName)
            return;

        // PosadaRoom's scene-local arrival controller claims its own story token
        // in Awake, before this sceneLoaded callback and before the iris releases
        // the transport token. It owns the sequence instead of an intro Timeline.
        if (part1PosadaRoute)
        {
            Destroy(gameObject);
            return;
        }

        PlayableDirector[] directors = FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PlayableDirector director in directors)
        {
            if (director.gameObject.scene != scene || director.name != destinationIntroDirectorName)
                continue;

            destinationIntroDirector = director;
            destinationIntroToken = StorySequenceCoordinator.Acquire("TransportationIntro:" + GetInstanceID());
            destinationIntroDirector.stopped += HandleDestinationIntroStopped;
            return;
        }

        Debug.LogWarning("Transportation destination has no configured intro director.", this);
        Destroy(gameObject);
    }

    private void HandleDestinationIntroStopped(PlayableDirector director)
    {
        if (director != destinationIntroDirector)
            return;

        destinationIntroToken?.Release();
        destinationIntroToken = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (destinationIntroDirector != null)
            destinationIntroDirector.stopped -= HandleDestinationIntroStopped;
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= HandleVideoPrepared;
            videoPlayer.started -= HandleVideoStarted;
            videoPlayer.loopPointReached -= HandleVideoFinished;
            videoPlayer.errorReceived -= HandleVideoError;
        }
        transportToken?.Release();
        destinationIntroToken?.Release();
    }
}
