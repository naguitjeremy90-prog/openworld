using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ReflectionQuizUI : MonoBehaviour
{
    public TMP_Text questionText;
    public TMP_Text feedbackText;
    public List<ReflectionAnswerButtonUI> answerButtons = new List<ReflectionAnswerButtonUI>();

    private ReflectionQuestionData currentQuestion;

    public void ShowQuestion(ReflectionQuestionData question)
    {
        currentQuestion = question;

        if (questionText != null)
            questionText.text = question.questionText;

        if (feedbackText != null)
            feedbackText.text = "";

        for (int i = 0; i < answerButtons.Count; i++)
        {
            if (i < question.choices.Count)
            {
                answerButtons[i].gameObject.SetActive(true);
                answerButtons[i].Setup(question.choices[i].answerText, i, this);
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void SelectAnswer(int answerIndex)
    {
        if (currentQuestion == null)
            return;

        if (answerIndex < 0 || answerIndex >= currentQuestion.choices.Count)
            return;

        bool isCorrect = currentQuestion.choices[answerIndex].isCorrect;

        if (isCorrect)
        {
            if (feedbackText != null)
                feedbackText.text = currentQuestion.correctFeedback;

            ReflectionProgressTracker.Instance.MarkReflectionCompleted(currentQuestion.questionID);
        }
        else
        {
            if (feedbackText != null)
                feedbackText.text = currentQuestion.wrongFeedback;
        }
    }
}