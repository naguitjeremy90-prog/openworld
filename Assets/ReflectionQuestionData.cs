using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ReflectionChoice
{
    public string answerText;
    public bool isCorrect;
}

[CreateAssetMenu(fileName = "NewReflectionQuestion", menuName = "Journal/Reflection Question")]
public class ReflectionQuestionData : ScriptableObject
{
    public string questionID;
    public string title;
    [TextArea(3, 8)] public string questionText;

    public List<ReflectionChoice> choices = new List<ReflectionChoice>();

    [TextArea(2, 5)] public string correctFeedback;
    [TextArea(2, 5)] public string wrongFeedback;
}