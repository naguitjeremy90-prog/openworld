using UnityEngine;

public class CoinPickUp : MonoBehaviour
{
    public int coinAmount = 500;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.AddCoins(coinAmount);
            Destroy(gameObject); // remove coin after pickup
        }
    }
}