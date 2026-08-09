using UnityEngine;
using UnityEngine.UI;

public class StatBarUI : MonoBehaviour
{
    public Image fillImage;

    public void UpdateBar(float current, float max)
    {
        if (fillImage == null)
            return;

        fillImage.fillAmount = max > 0 ? current / max : 0f;
    }
}