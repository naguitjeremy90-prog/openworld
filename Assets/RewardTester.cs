using UnityEngine;

public class RewardTester : MonoBehaviour
{
    public int rewardAmount = 10; // amount of coins to give
    public CurrencyManager currencyManager; // drag your CurrencyManager here

    // Call this method to give the player a reward
    public void GiveReward()
    {
        if (currencyManager != null)
        {
            currencyManager.AddCoins(rewardAmount);
            Debug.Log($"Rewarded {rewardAmount} coins!");
        }
        else
        {
            Debug.LogWarning("CurrencyManager not assigned!");
        }
    }

    // For testing, press R key to get the reward
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            GiveReward();
        }
    }
}