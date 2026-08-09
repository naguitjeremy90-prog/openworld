using UnityEngine;
using UnityEngine.UI;

public class ClarityHeartsUI : MonoBehaviour
{
    public Image[] hearts;
    public Sprite filledHeart;
    public Sprite emptyHeart;

    public void UpdateHearts(int clarity)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            hearts[i].sprite = i < clarity ? filledHeart : emptyHeart;
        }
    }
}