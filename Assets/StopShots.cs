using UnityEngine;
using UnityEngine.Playables;

public class StopShots : MonoBehaviour
{
    public PlayableDirector director;
    public MonoBehaviour playerMovementScript;
    public GameObject gameplayUI;

    public GameObject shot1;
    public GameObject shot2;
    public GameObject shot3;

    void Start()
    {
        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        if (gameplayUI != null)
            gameplayUI.SetActive(false);

        if (director != null)
            director.stopped += OnCutsceneFinished;
    }

    void OnCutsceneFinished(PlayableDirector pd)
    {
        if (shot1 != null) shot1.SetActive(false);
        if (shot2 != null) shot2.SetActive(false);
        if (shot3 != null) shot3.SetActive(false);

        if (playerMovementScript != null)
            playerMovementScript.enabled = true;

        if (gameplayUI != null)
            gameplayUI.SetActive(true);
    }

    void OnDestroy()
    {
        if (director != null)
            director.stopped -= OnCutsceneFinished;
    }
}