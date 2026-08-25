using UnityEngine;
using System.Collections;
using TMPro;
public class SleepInteraction : MonoBehaviour
{
    [SerializeField] private GameObject interactText;
    [SerializeField] private TMP_Text sleepText;

    private bool playerNear = false;  
    private bool hasSlept = false;

    private void Start()
    {
        if(interactText != null)
            interactText.SetActive(false);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !hasSlept)
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
        if (playerNear && Input.GetKeyDown(KeyCode.E) && !hasSlept)
        {
            
            StartCoroutine(Sleep());
        }
    }
    private IEnumerator Sleep()
    {
        hasSlept = true;
        if (interactText != null)
            interactText.SetActive(false);

        GameFlags.isMorning = true;

        sleepText.gameObject.SetActive(false);

        FadeController fadeController = FindAnyObjectByType<FadeController>();

        if(fadeController != null)
        {
            yield return StartCoroutine(fadeController.FadeToBlack());

            yield return new WaitForSeconds(0.7f);

            sleepText.gameObject.SetActive(true);

            sleepText.text = "z";
            yield return new
            WaitForSeconds(1f);

            sleepText.text = "zZ";
            yield return new
            WaitForSeconds(1f);

            sleepText.text = "zZz";
            yield return new
            WaitForSeconds(2f);

            sleepText.gameObject.SetActive(false);

            

            if(fadeController != null)
            {
                yield return StartCoroutine(fadeController.FadeFromBlack());
            }
        }
    }
}
