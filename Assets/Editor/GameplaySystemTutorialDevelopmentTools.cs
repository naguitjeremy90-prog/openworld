#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only session simulations. No player-facing controls or saved state.</summary>
public static class GameplaySystemTutorialDevelopmentTools
{
    private static StorySequenceToken simulatedStorySequence;

    [MenuItem("Tools/ALAALA/Tutorial Testing/Reset Session Tutorial Scenario", true)]
    [MenuItem("Tools/ALAALA/Tutorial Testing/Simulate Journal First-Dream Unlock", true)]
    [MenuItem("Tools/ALAALA/Tutorial Testing/Simulate Inventory Superseding Journal", true)]
    [MenuItem("Tools/ALAALA/Tutorial Testing/Toggle Simulated Story Sequence", true)]
    private static bool ValidatePlayMode()
    {
        return EditorApplication.isPlaying;
    }

    [MenuItem("Tools/ALAALA/Tutorial Testing/Reset Session Tutorial Scenario")]
    private static void ResetTutorialFlags()
    {
        foreach (GameplaySystemId system in System.Enum.GetValues(typeof(GameplaySystemId)))
            GameplaySystemTutorialState.SetSeen(system, false);
        GameplaySystemState.SetDevelopmentUnlocks(false, false, false);
        Debug.Log("Tutorial test: reset session-only system unlock and tutorial flags.");
    }

    [MenuItem("Tools/ALAALA/Tutorial Testing/Simulate Journal First-Dream Unlock")]
    private static void SimulateJournalUnlock()
    {
        ReconstructionJournalManager journal =
            Object.FindAnyObjectByType<ReconstructionJournalManager>();
        bool started = journal != null && journal.UnlockJournalForFirstDream();
        Debug.Log("Tutorial test: Journal first-dream unlock started = " + started + ".");
    }

    [MenuItem("Tools/ALAALA/Tutorial Testing/Simulate Inventory Superseding Journal")]
    private static void SimulateInventorySupersede()
    {
        GameplaySystemTutorialManager.Instance.RequestDevelopmentTutorial(
            GameplaySystemId.Inventory);
        Debug.Log(
            "Tutorial test: Inventory superseded the active tutorial. Journal completion remains " +
            GameplaySystemTutorialState.IsSeen(GameplaySystemId.Journal) + ".");
    }

    [MenuItem("Tools/ALAALA/Tutorial Testing/Toggle Simulated Story Sequence")]
    private static void ToggleStorySequence()
    {
        if (simulatedStorySequence != null && simulatedStorySequence.IsValid)
        {
            simulatedStorySequence.Release();
            simulatedStorySequence = null;
            Debug.Log("Tutorial test: simulated Story Sequence ended.");
        }
        else
        {
            simulatedStorySequence = StorySequenceCoordinator.Acquire(
                "GameplaySystemTutorialDevelopmentSimulation");
            Debug.Log("Tutorial test: simulated Story Sequence started.");
        }
    }
}
#endif
