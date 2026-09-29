using UnityEngine;
using UnityEngine.Playables;

/// <summary>Clears the local cutscene overlay when its Timeline stops.</summary>
public sealed class IntroMakamisaCutsceneFade : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private CanvasGroup fadeGroup;

    private void Awake()
    {
        Clear();
    }

    private void OnEnable()
    {
        if (director != null)
            director.stopped += HandleStopped;
    }

    private void OnDisable()
    {
        if (director != null)
            director.stopped -= HandleStopped;

        Clear();
    }

    private void HandleStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector == director)
            Clear();
    }

    private void Clear()
    {
        if (fadeGroup == null)
            return;

        fadeGroup.alpha = 0f;
        fadeGroup.interactable = false;
        fadeGroup.blocksRaycasts = false;
    }
}
