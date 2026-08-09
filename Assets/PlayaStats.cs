using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    public float maxHP = 100f;
    public float currentHP = 100f;
    public Image hpBarFill;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public Image staminaBarFill;
    public float staminaRegenRate = 10f;

    [Header("Hunger")]
    public float maxHunger = 100f;
    public float currentHunger = 100f;
    public Image hungerBarFill;
    public float hungerDrainRate = 0.5f;
    public float starvationDamageRate = 1f;

    [Header("Warning UI")]
    public TextMeshProUGUI warningSign;
    public float warningDuration = 3f;

    [Header("Item Effect UI")]
    public TextMeshProUGUI itemEffectText;
    public float itemEffectDuration = 3f;

    [Header("Player References")]
    public PlayerMovement playerMovement;
    public Light playerFlashlight;

    private float warningTimer;
    private float itemEffectTimer;

    private Coroutine hpRegenCoroutine;
    private Coroutine speedBuffCoroutine;
    private Coroutine staminaBuffCoroutine;
    private Coroutine hungerBuffCoroutine;
    private Coroutine nightVisionCoroutine;

    void Start()
    {
        currentHP = maxHP;
        currentStamina = maxStamina;
        currentHunger = maxHunger;

        UpdateBars();

        if (warningSign != null)
            warningSign.text = "";

        if (itemEffectText != null)
            itemEffectText.text = "";
    }

    void Update()
    {
        HandleStaminaRegen();
        HandleHungerAndStarvation();
        UpdateBars();
        UpdateWarningMessage();
        UpdateItemEffectMessage();
    }

    void HandleStaminaRegen()
    {
        if (!Input.GetKey(KeyCode.LeftShift) && currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
            currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
        }
    }

    void HandleHungerAndStarvation()
    {
        currentHunger -= hungerDrainRate * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);

        if (currentHunger <= 0f)
        {
            currentHP -= starvationDamageRate * Time.deltaTime;
            currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

            ShowWarning("You are starving! HP is dropping.");
        }
        else if (currentHunger <= 25f)
        {
            ShowWarning("You should eat something soon.");
        }
    }

    void UpdateBars()
    {
        if (hpBarFill != null)
            hpBarFill.fillAmount = currentHP / maxHP;

        if (staminaBarFill != null)
            staminaBarFill.fillAmount = currentStamina / maxStamina;

        if (hungerBarFill != null)
            hungerBarFill.fillAmount = currentHunger / maxHunger;
    }

    void UpdateWarningMessage()
    {
        if (warningSign == null)
            return;

        if (currentStamina <= 0f)
        {
            ShowWarning("You are exhausted. You need to rest.");
        }

        if (warningTimer > 0f)
        {
            warningTimer -= Time.deltaTime;
        }
        else
        {
            warningSign.text = "";
        }
    }

    void UpdateItemEffectMessage()
    {
        if (itemEffectText == null)
            return;

        if (itemEffectTimer > 0f)
        {
            itemEffectTimer -= Time.deltaTime;
        }
        else
        {
            itemEffectText.text = "";
        }
    }

    void ShowWarning(string message)
    {
        if (warningSign == null)
            return;

        warningSign.text = message;
        warningTimer = warningDuration;
    }

    public void ShowItemEffect(string message)
    {
        if (itemEffectText == null)
            return;

        itemEffectText.text = message;
        itemEffectTimer = itemEffectDuration;
    }

    public void HealInstantPercent(float percent)
    {
        float amount = maxHP * (percent / 100f);
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
    }

    public void RestoreHungerInstant(float percent)
    {
        float amount = maxHunger * (percent / 100f);
        currentHunger += amount;
        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);
    }

    public void UseElixirNgBuhay()
    {
        if (hpRegenCoroutine != null)
            StopCoroutine(hpRegenCoroutine);

        hpRegenCoroutine = StartCoroutine(RegenerateHPOverTime(10f, 5f));
        ShowItemEffect("Elixir ng Buhay used. Regenerating health.");
    }

    public void UseElixirNgHangin()
    {
        if (speedBuffCoroutine != null)
            StopCoroutine(speedBuffCoroutine);

        speedBuffCoroutine = StartCoroutine(SpeedBuff(55f, 60f));
        ShowItemEffect("Elixir ng Hangin used. Speed increased.");
    }

    public void UseElixirNgLiwanag()
    {
        if (nightVisionCoroutine != null)
            StopCoroutine(nightVisionCoroutine);

        nightVisionCoroutine = StartCoroutine(NightVisionBuff(120f));
        ShowItemEffect("Elixir ng Liwanag used. Night vision granted.");
    }

    public void UseElixirNgTibay()
    {
        HealInstantPercent(45f);
        ShowItemEffect("Elixir ng Tibay used. HP restored instantly.");
    }
    public void UseElixirNgPagkain()
    {
        RestoreHungerInstant(25f);
        ShowItemEffect("Elixir ng Pagkain used. Hunger restored.");
    }
    public void UseSeresaNgLunas()
    {
        if (hpRegenCoroutine != null)
            StopCoroutine(hpRegenCoroutine);

        hpRegenCoroutine = StartCoroutine(RegenerateHPOverTime(1f, 3f));
        ShowItemEffect("Seresa ng Lunas used. Minor regeneration active.");
    }

    public void UseUbasNgGalaw()
    {
        if (staminaBuffCoroutine != null)
            StopCoroutine(staminaBuffCoroutine);

        staminaBuffCoroutine = StartCoroutine(StaminaUsageBuff(0.98f, 55f));
        ShowItemEffect("Ubas ng Galaw used. Stamina loss reduced.");
    }

    public void UsePakwanNgGinhawan()
    {
        if (hungerBuffCoroutine != null)
            StopCoroutine(hungerBuffCoroutine);

        hungerBuffCoroutine = StartCoroutine(HungerDrainBuff(0.95f, 50f));
        ShowItemEffect("Pakwan ng Ginhawan used. Hunger drain reduced.");
    }

    public void UseKahelNgLakas()
    {
        HealInstantPercent(2f);
        ShowItemEffect("Kahel ng Lakas used. A little HP was restored.");
    }

    IEnumerator RegenerateHPOverTime(float percentPerSecond, float duration)
    {
        float timer = 0f;

        while (timer < duration)
        {
            float healAmount = (maxHP * (percentPerSecond / 100f)) * Time.deltaTime;
            currentHP += healAmount;
            currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

            timer += Time.deltaTime;
            yield return null;
        }

        hpRegenCoroutine = null;
    }

    IEnumerator SpeedBuff(float bonusPercent, float duration)
    {
        if (playerMovement != null)
            playerMovement.speedMultiplier = 1f + (bonusPercent / 100f);

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (playerMovement != null)
            playerMovement.speedMultiplier = 1f;

        speedBuffCoroutine = null;
    }

    IEnumerator StaminaUsageBuff(float multiplier, float duration)
    {
        if (playerMovement != null)
            playerMovement.staminaUseMultiplier = multiplier;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (playerMovement != null)
            playerMovement.staminaUseMultiplier = 1f;

        staminaBuffCoroutine = null;
    }

    IEnumerator HungerDrainBuff(float multiplier, float duration)
    {
        float originalHungerDrain = hungerDrainRate;
        hungerDrainRate = originalHungerDrain * multiplier;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        hungerDrainRate = originalHungerDrain;
        hungerBuffCoroutine = null;
    }

    IEnumerator NightVisionBuff(float duration)
    {
        if (playerFlashlight != null)
            playerFlashlight.enabled = true;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (playerFlashlight != null)
            playerFlashlight.enabled = false;

        nightVisionCoroutine = null;
    }
}