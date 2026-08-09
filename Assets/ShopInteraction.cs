using UnityEngine;
using System.Collections;

public class ShopInteraction : MonoBehaviour
{
    public static bool IsShopOpen = false;

    [Header("UI References")]
    public GameObject interactText;
    public GameObject questionText;
    public GameObject yesButton;
    public GameObject noButton;
    public GameObject responseTextOne;
    public GameObject responseTextTwo;
    public GameObject shopPanel;

    [Header("Shop Reference")]
    public ShopManager shopManager;

    private bool playerNear = false;
    private bool questionActive = false;
    private Coroutine openShopCoroutine;
    private Coroutine hideResponseCoroutine;

    void Start()
    {
        IsShopOpen = false;

        if (shopManager == null)
            shopManager = FindAnyObjectByType<ShopManager>();

        if (interactText != null) interactText.SetActive(false);
        if (questionText != null) questionText.SetActive(false);
        if (yesButton != null) yesButton.SetActive(false);
        if (noButton != null) noButton.SetActive(false);
        if (responseTextOne != null) responseTextOne.SetActive(false);
        if (responseTextTwo != null) responseTextTwo.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    void Update()
    {
        // Close shop with E or ESC
        if (IsShopOpen && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape)))
        {
            CloseShop();
            return;
        }

        // Show question UI when near
        if (playerNear && Input.GetKeyDown(KeyCode.E) && !questionActive && !IsShopOpen)
        {
            if (interactText != null) interactText.SetActive(false);
            if (questionText != null) questionText.SetActive(true);
            if (yesButton != null) yesButton.SetActive(true);
            if (noButton != null) noButton.SetActive(true);
            questionActive = true;
        }

        // Handle Y/N responses
        if (questionActive)
        {
            if (Input.GetKeyDown(KeyCode.Y))
            {
                HideQuestionUI();
                if (responseTextOne != null)
                    responseTextOne.SetActive(true);
                questionActive = false;

                if (openShopCoroutine != null)
                    StopCoroutine(openShopCoroutine);

                openShopCoroutine = StartCoroutine(OpenShopAfterResponse(responseTextOne));
            }

            if (Input.GetKeyDown(KeyCode.N))
            {
                HideQuestionUI();
                if (responseTextTwo != null)
                    responseTextTwo.SetActive(true);
                questionActive = false;

                if (hideResponseCoroutine != null)
                    StopCoroutine(hideResponseCoroutine);

                hideResponseCoroutine = StartCoroutine(HideResponseText(responseTextTwo));
            }
        }
    }

    void HideQuestionUI()
    {
        if (questionText != null) questionText.SetActive(false);
        if (yesButton != null) yesButton.SetActive(false);
        if (noButton != null) noButton.SetActive(false);
    }

    void OpenShop()
    {
        if (openShopCoroutine != null)
            StopCoroutine(openShopCoroutine);

        openShopCoroutine = StartCoroutine(OpenShopCoroutine());
    }

    IEnumerator OpenShopCoroutine()
    {
        IsShopOpen = true;

        if (shopPanel != null)
            shopPanel.SetActive(true);

        // Wait one frame so TMP_Text inside the shop properly updates
        yield return null;

        // Update coins in shop UI
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.UpdateCoinUI();

       
    }

    void CloseShop()
    {
        if (openShopCoroutine != null)
        {
            StopCoroutine(openShopCoroutine);
            openShopCoroutine = null;
        }

        if (hideResponseCoroutine != null)
        {
            StopCoroutine(hideResponseCoroutine);
            hideResponseCoroutine = null;
        }

        IsShopOpen = false;
        questionActive = false;

        if (interactText != null) interactText.SetActive(false);
        if (questionText != null) questionText.SetActive(false);
        if (yesButton != null) yesButton.SetActive(false);
        if (noButton != null) noButton.SetActive(false);
        if (responseTextOne != null) responseTextOne.SetActive(false);
        if (responseTextTwo != null) responseTextTwo.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);

        if (shopManager != null)
            shopManager.ClearAllSelections();

        // Update coin UI after closing shop
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.UpdateCoinUI();


    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;

            if (!IsShopOpen && interactText != null)
                interactText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
            CloseShop();
        }
    }

    IEnumerator OpenShopAfterResponse(GameObject response)
    {
        yield return new WaitForSeconds(1.5f);

        if (!playerNear)
            yield break;

        if (response != null)
            response.SetActive(false);

        OpenShop();
    }

    IEnumerator HideResponseText(GameObject response)
    {
        yield return new WaitForSeconds(1.5f);

        if (response != null)
            response.SetActive(false);

        if (playerNear && interactText != null && !IsShopOpen)
            interactText.SetActive(true);
    }
}