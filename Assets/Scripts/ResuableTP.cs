using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneEntrance : MonoBehaviour
{
    [SerializeField] private string sceneName;
    [SerializeField] private GameObject interactText;
    [SerializeField] private string returnSpawnPoint;
    private bool playerNear = false;

    private void Start()
    {
        if (interactText != null)
            interactText.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;

            if (interactText != null)
                interactText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;

            if (interactText != null)
                interactText.SetActive(false);
        }
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
