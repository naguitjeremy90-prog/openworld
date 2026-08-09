using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    public PlayerStats playerStats;

    public StatBarUI hpBar;
    public StatBarUI staminaBar;
    public StatBarUI hungerBar;

    void Update()
    {
        if (playerStats == null)
            return;

        if (hpBar != null)
            hpBar.UpdateBar(playerStats.currentHP, playerStats.maxHP);

        if (staminaBar != null)
            staminaBar.UpdateBar(playerStats.currentStamina, playerStats.maxStamina);

        if (hungerBar != null)
            hungerBar.UpdateBar(playerStats.currentHunger, playerStats.maxHunger);
    }
}