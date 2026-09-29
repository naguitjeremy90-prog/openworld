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

    private StorySequenceToken storyToken;
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
        if (!started || cleaningUp || stoppedDirector != introDirector)
            return;

        // The stopped event also fires for an interrupted Timeline. Only a
        // director that reached its authored end completes this session flag.
        bool completed = introDirector.duration > 0d &&
            introDirector.time >= introDirector.duration;

        started = false;
        introDirector.stopped -= HandleIntroStopped;
        if (completed)
            SessionStoryState.SetFlag(IntroSeenFlag, true);

        DisableIntroCamera();
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
