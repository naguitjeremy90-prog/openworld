using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ReflectionAnswerButtonUI : MonoBehaviour
{
    public TMP_Text answerText;
    private int answerIndex;
    private ReflectionQuizUI quizUI;

    public void Setup(string text, int index, ReflectionQuizUI owner)
    {
        answerIndex = index;
        quizUI = owner;

        if (answerText != null)
            answerText.text = text;
    }

    public void OnClickAnswer()
    {
        if (quizUI != null)
            quizUI.SelectAnswer(answerIndex);
    }
}