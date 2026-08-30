using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DialogueEditor;

public class JeepIntroManager : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private FadeController fadeController;

    [Header("Audio")]
    [SerializeField] private AudioSource engineRevSource;
    [SerializeField] private AudioSource engineLoopSource;
    [SerializeField] private AudioSource jeepHaltSource;

    [Header("Intro Timing")]
    [SerializeField] private float blackScreenDuration = 1.5f;

    [Header("Narration")]
    [SerializeField] private IntroNarrationManager narrationManager;
    [SerializeField] private float narrationStartDelay = 2f;

    [Header("Night Dialogue")]
    [SerializeField] private SelfDialogueTrigger nightDialogue;
    [SerializeField] private float nightDialogueDelay = 2f;

    [Header("Ending")]
    [SerializeField] private float pauseAfterDialogue = 1f;
    [SerializeField] private float haltSoundDelay = 0.3f;
    [SerializeField] private float waitAfterHalt = 2f;
    [SerializeField] private string nextSceneName = "DraftWorld";

    [Header("Sky Transition")]
    [SerializeField] private Material skyBlendMaterial;

    [SerializeField] private Texture afternoonSky;
    [SerializeField] private Texture sunsetSky;
    [SerializeField] private Texture nightSky;

    [SerializeField] private Light sun;

    [Header("Time Progression")]
    [SerializeField] private float afternoonDuration = 10f;
    [SerializeField] private float dayToSunsetDuration = 12f;
    [SerializeField] private float sunsetDuration = 5f;
    [SerializeField] private float sunsetToNightDuration = 12f;

    [Header("Sun Intensity")]
    [SerializeField] private float afternoonIntensity = 1f;
    [SerializeField] private float sunsetIntensity = 0.6f;
    [SerializeField] private float nightIntensity = 0.15f;

    private bool waitingForNightDialogue = false;

    private void OnEnable()
    {
        ConversationManager.OnConversationEnded += OnConversationEnded;
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationEnded -= OnConversationEnded;
    }

    private IEnumerator Start()
    {
        SetupStartingSky();

        if (sun != null)
            sun.intensity = afternoonIntensity;

        if (engineLoopSource != null)
            engineLoopSource.Stop();

        if (engineRevSource != null)
            engineRevSource.Play();

        // Start on black screen.
        yield return new WaitForSeconds(blackScreenDuration);

        // Reveal Miguel inside the jeep.
        if (fadeController != null)
        {
            yield return StartCoroutine(
                fadeController.FadeFromBlack()
            );
        }

        // Begin normal jeep engine.
        if (engineLoopSource != null)
            engineLoopSource.Play();

        // Start narration independently.
        StartCoroutine(StartNarrationAfterDelay());

        // Afternoon.
        yield return new WaitForSeconds(
            afternoonDuration
        );

        // Afternoon -> Sunset.
        yield return StartCoroutine(
            BlendSky(
                afternoonSky,
                sunsetSky,
                afternoonIntensity,
                sunsetIntensity,
                dayToSunsetDuration
            )
        );

        // Stay at sunset briefly.
        yield return new WaitForSeconds(
            sunsetDuration
        );

        // Sunset -> Night.
        yield return StartCoroutine(
            BlendSky(
                sunsetSky,
                nightSky,
                sunsetIntensity,
                nightIntensity,
                sunsetToNightDuration
            )
        );

        // Night has fully arrived.
        yield return new WaitForSeconds(
            nightDialogueDelay
        );

        if (nightDialogue != null)
        {
            waitingForNightDialogue = true;
            nightDialogue.StartSelfDialogue();
        }
    }

    private void OnConversationEnded()
    {
        if (!waitingForNightDialogue)
            return;

        waitingForNightDialogue = false;

        StartCoroutine(EndJeepRide());
    }

    private IEnumerator EndJeepRide()
    {
        // Small pause after Miguel finishes speaking.
        yield return new WaitForSeconds(pauseAfterDialogue);

        // Begin fading to black.
        if (fadeController != null)
        {
            yield return StartCoroutine(
                fadeController.FadeToBlack()
            );
        }

        // Screen is now completely black.
        yield return new WaitForSeconds(haltSoundDelay);

        // Stop the normal driving engine.
        if (engineLoopSource != null)
            engineLoopSource.Stop();

        // Play the jeep stopping sound.
        if (jeepHaltSource != null)
            jeepHaltSource.Play();

        // Stay on black briefly while the halt sound plays.
        yield return new WaitForSeconds(waitAfterHalt);

        // Load the arrival scene.
        SceneManager.LoadScene(nextSceneName);
    }

    private IEnumerator StartNarrationAfterDelay()
    {
        yield return new WaitForSeconds(
            narrationStartDelay
        );

        if (narrationManager != null)
            narrationManager.PlayNarration();
    }

    private void SetupStartingSky()
    {
        if (skyBlendMaterial == null)
            return;

        skyBlendMaterial.SetTexture(
            "_TexA",
            afternoonSky
        );

        skyBlendMaterial.SetTexture(
            "_TexB",
            sunsetSky
        );

        skyBlendMaterial.SetFloat(
            "_Blend",
            0f
        );

        RenderSettings.skybox =
            skyBlendMaterial;

        DynamicGI.UpdateEnvironment();
    }

    private IEnumerator BlendSky(
        Texture fromSky,
        Texture toSky,
        float startSunIntensity,
        float endSunIntensity,
        float duration)
    {
        if (skyBlendMaterial == null)
            yield break;

        skyBlendMaterial.SetTexture(
            "_TexA",
            fromSky
        );

        skyBlendMaterial.SetTexture(
            "_TexB",
            toSky
        );

        skyBlendMaterial.SetFloat(
            "_Blend",
            0f
        );

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float progress = Mathf.Clamp01(
                time / duration
            );

            skyBlendMaterial.SetFloat(
                "_Blend",
                progress
            );

            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(
                    startSunIntensity,
                    endSunIntensity,
                    progress
                );
            }

            yield return null;
        }

        skyBlendMaterial.SetFloat(
            "_Blend",
            1f
        );

        if (sun != null)
            sun.intensity = endSunIntensity;

        DynamicGI.UpdateEnvironment();
    }
}


