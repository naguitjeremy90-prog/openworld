using System.Collections.Generic;
using UnityEngine;

public class ReflectionProgressTracker : MonoBehaviour
{
    public static ReflectionProgressTracker Instance;

    private HashSet<string> unlockedReflections = new HashSet<string>();
    private HashSet<string> completedReflections = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void UnlockReflection(string questionID)
    {
        if (!unlockedReflections.Contains(questionID))
        {
            unlockedReflections.Add(questionID);
            Debug.Log("Unlocked Reflection: " + questionID);
        }

        if (JournalManager.Instance != null && JournalManager.Instance.journalIconUI != null)
        {
            JournalManager.Instance.journalIconUI.ShowBadge();
        }
    }

    public bool IsReflectionUnlocked(string questionID)
    {
        return unlockedReflections.Contains(questionID);
    }

    public void MarkReflectionCompleted(string questionID)
    {
        if (!completedReflections.Contains(questionID))
        {
            completedReflections.Add(questionID);
            Debug.Log("Completed Reflection: " + questionID);
        }
    }

    public bool IsReflectionCompleted(string questionID)
    {
        return completedReflections.Contains(questionID);
    }
}