using UnityEngine;
using TMPro;

public class CoinManager : MonoBehaviour
{
    public int coins = 0;

    public TMP_Text coinCounterText;   // main UI
    public TMP_Text shopCoinsText;     // shop UI

    void Start()
    {
        UpdateCoinUI();
    }

    public void AddCoins(int amount)
    {
        coins += amount;
        UpdateCoinUI();
    }

    public void UpdateCoinUI()
    {
        if (coinCounterText != null)
            coinCounterText.text = " " + coins;

        if (shopCoinsText != null)
            shopCoinsText.text = " " + coins;
    }
}