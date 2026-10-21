using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Temporary Posada night presentation, using the existing sleep-to-morning state.</summary>
[DefaultExecutionOrder(-12000)]
[DisallowMultipleComponent]
public sealed class PosadaSleepLighting : MonoBehaviour
{
    [SerializeField] private Light interiorLight;
    [SerializeField] private Material nightSkybox;
    [SerializeField, Range(0f, 1f)] private float nightLightMultiplier = .45f;
    [SerializeField] private Color nightLightTint = new Color(1f, .85f, .65f);
    [SerializeField] private Color nightAmbientColor = new Color(.12f, .13f, .17f);
    [SerializeField, Range(0f, 1f)] private float nightAmbientIntensity = .35f;
    [SerializeField, Range(0f, 1f)] private float nightReflectionMultiplier = .25f;

    private bool captured, lightOverridden, environmentOverridden;
    private float originalLightIntensity, originalAmbientIntensity, originalReflectionIntensity;
    private Color originalLightColor, originalSkyColor, originalEquatorColor, originalGroundColor;
    private AmbientMode originalAmbientMode;
    private Material originalSkybox;

    private bool IsPosada => gameObject.scene.path == "Assets/Scenes/PosadaInterior.unity" ||
        gameObject.scene.path == "Assets/Scenes/PosadaRoom.unity";

    private void OnEnable()
    {
        if (!Application.isPlaying || !IsPosada) return;
        SceneManager.activeSceneChanged += ActiveSceneChanged;
        RefreshLighting();
    }

    private void ActiveSceneChanged(Scene previous, Scene next)
    {
        if (next == gameObject.scene) RefreshLighting();
    }

    // Called at the existing isMorning assignment, while the sleep blackout still covers the room.
    // No new state, time progression, or tutorial/dialogue dependency is introduced.
    internal static void RefreshAfterSleep(Scene sleepScene)
    {
        if (!Application.isPlaying) return;
        foreach (var lighting in FindObjectsByType<PosadaSleepLighting>(FindObjectsInactive.Exclude))
            if (lighting.gameObject.scene == sleepScene && lighting.isActiveAndEnabled && lighting.IsPosada)
                lighting.RefreshLighting();
    }

    private void RefreshLighting()
    {
        // RenderSettings belong to the active scene. Never overwrite an outgoing/different scene.
        if (!Application.isPlaying || !IsPosada || SceneManager.GetActiveScene() != gameObject.scene) return;
        if (interiorLight == null || interiorLight.gameObject.scene != gameObject.scene ||
            interiorLight.lightmapBakeType != LightmapBakeType.Realtime)
        {
            Debug.LogError("PosadaSleepLighting needs its own scene's realtime interior Light; lighting was left unchanged.", this);
            return;
        }
        if (!captured)
        {
            originalLightIntensity = interiorLight.intensity;
            originalLightColor = interiorLight.color;
            originalAmbientMode = RenderSettings.ambientMode;
            originalSkyColor = RenderSettings.ambientSkyColor;
            originalEquatorColor = RenderSettings.ambientEquatorColor;
            originalGroundColor = RenderSettings.ambientGroundColor;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalReflectionIntensity = RenderSettings.reflectionIntensity;
            originalSkybox = RenderSettings.skybox;
            captured = true;
        }
        if (GameFlags.isMorning)
        {
            RestoreOriginalLighting();
            return;
        }
        if (lightOverridden && environmentOverridden) return;
        interiorLight.intensity = originalLightIntensity * nightLightMultiplier;
        interiorLight.color = originalLightColor * nightLightTint;
        lightOverridden = true;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = nightAmbientColor;
        RenderSettings.ambientIntensity = nightAmbientIntensity;
        RenderSettings.reflectionIntensity = originalReflectionIntensity * nightReflectionMultiplier;
        if (nightSkybox != null) RenderSettings.skybox = nightSkybox;
        environmentOverridden = true;
        DynamicGI.UpdateEnvironment();
    }

    private void RestoreOriginalLighting()
    {
        if (!captured) return;
        if (lightOverridden && interiorLight != null)
        {
            interiorLight.intensity = originalLightIntensity;
            interiorLight.color = originalLightColor;
            lightOverridden = false;
        }
        if (!environmentOverridden || SceneManager.GetActiveScene() != gameObject.scene) return;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientSkyColor = originalSkyColor;
        RenderSettings.ambientEquatorColor = originalEquatorColor;
        RenderSettings.ambientGroundColor = originalGroundColor;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
        RenderSettings.skybox = originalSkybox;
        environmentOverridden = false;
        DynamicGI.UpdateEnvironment();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= ActiveSceneChanged;
        if (Application.isPlaying) RestoreOriginalLighting();
    }
}
