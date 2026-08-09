using UnityEngine;
using TMPro;

public class ClaritySystem : MonoBehaviour
{
    public static ClaritySystem Instance;

    public int clarity = 5;
    public int maxClarity = 5;

    public ClarityHeartsUI heartsUI;
    public TextMeshProUGUI clarityStatusText;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateUI();
    }

    public void LoseClarity()
    {
        clarity--;

        if (clarity < 0)
            clarity = 0;

        UpdateUI();
    }

    public void GainClarity()
    {
        clarity++;

        if (clarity > maxClarity)
            clarity = maxClarity;

        UpdateUI();
    }

    void UpdateUI()
    {
        if (heartsUI != null)
            heartsUI.UpdateHearts(clarity);

        if (clarityStatusText != null)
            clarityStatusText.text = GetClarityStatus();
    }

    string GetClarityStatus()
    {
        if (clarity == 5)
            return "High Clarity";

        if (clarity == 4)
            return "Good Understanding";

        if (clarity == 3)
            return "Partial Understanding";

        if (clarity == 2)
            return "Unclear Understanding";

        if (clarity == 1)
            return "Misunderstanding";

        return "Lost Clarity";
    }
}