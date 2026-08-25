using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneEntrance : MonoBehaviour
{
    [SerializeField] private string sceneName;
    [SerializeField] private GameObject interactText;
    [SerializeField] private string returnSpawnPoint;
    [Header("Interaction Highlight")]
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Color highlightColor = Color.white;
    [SerializeField, Min(0f)] private float highlightIntensity = 1.5f;

    private bool playerNear = false;
    private Material[] highlightMaterials;
    private Color[] originalBaseColors;
    private Color[] originalEmissionColors;

    private void Start()
    {
        if (interactText != null)
            interactText.SetActive(false);

        CacheHighlightMaterials();
        SetHighlight(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;

            if (interactText != null)
                interactText.SetActive(true);

            SetHighlight(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;

            if (interactText != null)
                interactText.SetActive(false);

            SetHighlight(false);
        }
    }

    private void OnDisable()
    {
        SetHighlight(false);
    }

    private void CacheHighlightMaterials()
    {
        if (highlightRenderer == null)
            return;

        // Renderer.materials creates per-renderer instances, so the shared door
        // materials elsewhere in the project are never changed.
        highlightMaterials = highlightRenderer.materials;
        originalBaseColors = new Color[highlightMaterials.Length];
        originalEmissionColors = new Color[highlightMaterials.Length];

        for (int i = 0; i < highlightMaterials.Length; i++)
        {
            Material material = highlightMaterials[i];
            originalBaseColors[i] = GetBaseColor(material);
            originalEmissionColors[i] = material.HasProperty("_EmissionColor")
                ? material.GetColor("_EmissionColor")
                : Color.black;
        }
    }

    private void SetHighlight(bool active)
    {
        if (highlightMaterials == null)
            return;

        for (int i = 0; i < highlightMaterials.Length; i++)
        {
            Material material = highlightMaterials[i];
            Color baseColor = originalBaseColors[i];

            if (active)
            {
                Color whiteTint = Color.Lerp(baseColor, highlightColor, 0.65f);
                whiteTint.a = baseColor.a;
                SetBaseColor(material, whiteTint);

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", highlightColor * highlightIntensity);
                }
            }
            else
            {
                SetBaseColor(material, baseColor);

                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", originalEmissionColors[i]);
            }
        }
    }

    private static Color GetBaseColor(Material material)
    {
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");
        return Color.white;
    }

    private static void SetBaseColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private void Update()
    {
        if (playerNear && Input.GetKeyDown(KeyCode.E))
        {
            StartCoroutine(TransitionScene());
        }
    }

    private IEnumerator TransitionScene()
    {
        FadeController fadeController = FindAnyObjectByType<FadeController>();

        if(fadeController != null)
        {
            yield return StartCoroutine(fadeController.FadeToBlack());
        }

        if (!string.IsNullOrEmpty(returnSpawnPoint))
            {
                SpawnData.spawnPointName = returnSpawnPoint;
            }
            SceneManager.LoadSceneAsync(sceneName);
        }
    }
