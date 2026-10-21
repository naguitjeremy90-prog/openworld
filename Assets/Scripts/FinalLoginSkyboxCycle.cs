using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Alaala.Presentation
{
    /// <summary>Camera-local login background. Never changes shared materials or RenderSettings.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera), typeof(Skybox))]
    public sealed class FinalLoginSkyboxCycle : MonoBehaviour
    {
        private const string ScenePath = "Assets/Scenes/FinalLoginPage.unity";
        private const int SkyCount = 5;

        [Header("360-degree LDR panoramas (Skybox/Panoramic, default tint/exposure/rotation)")]
        [SerializeField] private Material skybox1;
        [SerializeField] private Material skybox2;
        [SerializeField] private Material skybox3;
        [SerializeField] private Material skybox4;
        [SerializeField] private Material skybox5;
        [SerializeField, Min(0.01f)] private float skyDuration = 60f;
        [Tooltip("Included in Sky Duration, not added to it.")]
        [SerializeField, Min(0f)] private float transitionDuration = 5f;
        [SerializeField] private bool loop = true;
        [SerializeField] private Shader blendShader;

        private static readonly int TexA = Shader.PropertyToID("_TexA");
        private static readonly int TexB = Shader.PropertyToID("_TexB");
        private static readonly int Blend = Shader.PropertyToID("_Blend");
        private Camera targetCamera;
        private Skybox cameraSkybox;
        private Material originalMaterial;
        private Material runtimeMaterial;
        private Texture[] textures;
        private CameraClearFlags originalClearFlags;
        private bool originalSkyboxEnabled;
        private double startTime;
        private int boundCurrent = -1, boundNext = -1;

        private void OnEnable()
        {
            if (!Application.isPlaying || gameObject.scene.path != ScenePath) return;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            if (SceneManager.GetActiveScene() == gameObject.scene) BeginCycle();
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            ReleaseResources();
            if (isActiveAndEnabled && next.IsValid() && next.path == ScenePath && next == gameObject.scene)
                BeginCycle();
        }

        private void BeginCycle()
        {
            ReleaseResources();
            if (GraphicsSettings.currentRenderPipeline != null || blendShader == null ||
                blendShader.name != "Custom/PanoramicSkyboxBlend" || !blendShader.isSupported)
            {
                Debug.LogWarning("FinalLoginSkyboxCycle requires Built-in rendering and the existing panoramic blend shader.", this);
                return;
            }

            var sources = new[] { skybox1, skybox2, skybox3, skybox4, skybox5 };
            var validated = new Texture[SkyCount];
            for (int i = 0; i < SkyCount; i++)
            {
                if (!TryGetCompatibleTexture(sources[i], out validated[i]))
                {
                    Debug.LogWarning($"FinalLoginSkyboxCycle: assign a compatible panorama to Skybox {i + 1}. Keeping the original sky.", this);
                    return;
                }
            }

            targetCamera = GetComponent<Camera>();
            cameraSkybox = GetComponent<Skybox>();
            originalMaterial = cameraSkybox.material;
            originalSkyboxEnabled = cameraSkybox.enabled;
            originalClearFlags = targetCamera.clearFlags;
            textures = validated;
            runtimeMaterial = new Material(blendShader)
            {
                name = "FinalLogin sky blend (runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            runtimeMaterial.SetFloat("_Exposure", 1f);
            runtimeMaterial.SetFloat("_Rotation", 0f);
            cameraSkybox.material = runtimeMaterial;
            cameraSkybox.enabled = true;
            targetCamera.clearFlags = CameraClearFlags.Skybox;
            startTime = Time.unscaledTimeAsDouble;
            ApplyFrame(0);
        }

        // The reused shader samples two ordinary equirectangular images. It does not decode
        // HDR, cubemaps, procedural skies, stereo layouts, or per-material tint/rotation.
        private static bool TryGetCompatibleTexture(Material source, out Texture texture)
        {
            texture = null;
            if (source == null || source.shader == null || source.shader.name != "Skybox/Panoramic") return false;
            if (!Mathf.Approximately(source.GetFloat("_Mapping"), 1f) ||
                !Mathf.Approximately(source.GetFloat("_ImageType"), 0f) ||
                !Mathf.Approximately(source.GetFloat("_Layout"), 0f) ||
                !Mathf.Approximately(source.GetFloat("_Exposure"), 1f) ||
                !Mathf.Approximately(source.GetFloat("_Rotation"), 0f)) return false;
            var tint = source.GetColor("_Tint");
            if (!Mathf.Approximately(tint.r, .5f) || !Mathf.Approximately(tint.g, .5f) ||
                !Mathf.Approximately(tint.b, .5f)) return false;
            var image = source.GetTexture("_MainTex") as Texture2D;
            if (image == null || image.width != image.height * 2) return false;
            switch (image.format)
            {
                case TextureFormat.RGB24:
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC2_RGB:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                    texture = image;
                    return true;
                default:
                    return false;
            }
        }

        private void Update()
        {
            if (runtimeMaterial == null) return;
            if (SceneManager.GetActiveScene() != gameObject.scene)
            {
                ReleaseResources();
                return;
            }
            ApplyFrame(Time.unscaledTimeAsDouble - startTime);
        }

        private void ApplyFrame(double elapsed)
        {
            EvaluateCycle(elapsed, skyDuration, transitionDuration, loop, out int current, out int next, out float blend);
            if (current != boundCurrent || next != boundNext)
            {
                runtimeMaterial.SetTexture(TexA, textures[current]);
                runtimeMaterial.SetTexture(TexB, textures[next]);
                boundCurrent = current;
                boundNext = next;
            }
            runtimeMaterial.SetFloat(Blend, blend);
        }

        internal static void EvaluateCycle(double elapsed, float duration, float transition, bool repeat,
            out int current, out int next, out float blend)
        {
            double period = float.IsNaN(duration) || float.IsInfinity(duration) ? 60 : Math.Max(.01, duration);
            double fade = float.IsNaN(transition) || float.IsInfinity(transition) ? 5 : Math.Max(0, Math.Min(transition, period));
            double time = Math.Max(0, elapsed);
            time = repeat ? time % (period * SkyCount) : Math.Min(time, period * SkyCount);
            current = Math.Min(SkyCount - 1, (int)(time / period));
            next = current == SkyCount - 1 ? (repeat ? 0 : current) : current + 1;
            double phase = time - current * period;
            blend = current == next || fade <= 0 ? 0 : Mathf.SmoothStep(0, 1, (float)((phase - (period - fade)) / fade));
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            ReleaseResources();
        }

        private void OnDestroy() => ReleaseResources();

        private void ReleaseResources()
        {
            if (runtimeMaterial != null)
            {
                // Restore only our camera override; a new scene's RenderSettings are never touched.
                if (cameraSkybox != null && cameraSkybox.material == runtimeMaterial)
                {
                    cameraSkybox.material = originalMaterial;
                    cameraSkybox.enabled = originalSkyboxEnabled;
                    if (targetCamera != null && targetCamera.clearFlags == CameraClearFlags.Skybox)
                        targetCamera.clearFlags = originalClearFlags;
                }
                if (Application.isPlaying) Destroy(runtimeMaterial);
                else DestroyImmediate(runtimeMaterial);
            }
            runtimeMaterial = null;
            originalMaterial = null;
            textures = null;
            boundCurrent = boundNext = -1;
        }
    }
}
