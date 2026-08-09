using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class PlayFabCoinManager : MonoBehaviour
{
    private string currencyCode = "GC"; // your legacy currency code

    // Add coins to the player
    public void AddCoins(int amount)
    {
        var request = new AddUserVirtualCurrencyRequest
        {
            VirtualCurrency = currencyCode,
            Amount = amount
        };
        PlayFabClientAPI.AddUserVirtualCurrency(request,
            result => Debug.Log($"Added {amount} coins! New balance: {result.Balance}"),
            error => Debug.LogError(error.GenerateErrorReport()));
    }

    // Spend coins
    public void SpendCoins(int amount)
    {
        var request = new SubtractUserVirtualCurrencyRequest
        {
            VirtualCurrency = currencyCode,
            Amount = amount
        };
        PlayFabClientAPI.SubtractUserVirtualCurrency(request,
            result => Debug.Log($"Spent {amount} coins! New balance: {result.Balance}"),
            error => Debug.LogError(error.GenerateErrorReport()));
    }

    // Get current coin balance
    public void GetCoinBalance(System.Action<int> callback)
    {
        PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), result =>
        {
            int coins = result.VirtualCurrency.ContainsKey(currencyCode) ? result.VirtualCurrency[currencyCode] : 0;
            Debug.Log($"Player has {coins} coins");
            callback?.Invoke(coins);
        }, error => Debug.LogError(error.GenerateErrorReport()));
    }
}