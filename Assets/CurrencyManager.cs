using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public PlayFabCoinManager playFabCoinManager;
    public static CurrencyManager Instance; // optional singleton

    public int coins = 0;

    public TMP_Text coinCounterText; // UI outside shop
    public TMP_Text shopCoinsText;   // UI inside shop

    private void Awake()
    {
        Instance = this; // keep a reference for other scripts
        
    }

    private void Start()
    {
        if (playFabCoinManager != null)  // check if PlayFabCoinManager exists
        {
            playFabCoinManager.GetCoinBalance(coinsFromPlayFab =>  // get coins from PlayFab
            {
                coins = coinsFromPlayFab;   // update local coin variable
                UpdateCoinUI();             // update your UI with PlayFab balance
            });
        }
        else
        {
            UpdateCoinUI();  // if PlayFabCoinManager is missing, just show local coins
        }
    }

    public void AddCoins(int amount)
    {
        coins += amount;
        UpdateCoinUI();

        // Sync with PlayFab
        if (playFabCoinManager != null)
            playFabCoinManager.AddCoins(amount);
    }

    public bool SpendCoins(int amount)
    {
        if (coins < amount)
            return false;

        coins -= amount;
        UpdateCoinUI();

        // Sync with PlayFab
        if (playFabCoinManager != null)
            playFabCoinManager.SpendCoins(amount);

        return true;
    }

    public void UpdateCoinUI()
    {
        if (coinCounterText != null)
            coinCounterText.text = "Coins: " + coins;

        if (shopCoinsText != null)
            shopCoinsText.text = "Coins: " + coins;
    }
}