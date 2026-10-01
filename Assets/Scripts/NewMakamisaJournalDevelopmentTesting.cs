using UnityEngine;

/// <summary>Optional prior Journal state for starting NEWMAKAMISA directly in the Editor.</summary>
[DefaultExecutionOrder(-30000)]
[DisallowMultipleComponent]
public sealed class NewMakamisaJournalDevelopmentTesting : MonoBehaviour
{
    [Header("DIRECT NEWMAKAMISA DEVELOPMENT TESTING ONLY")]
    [SerializeField] private bool enablePriorJournalState = false;

    private void Awake()
    {
#if UNITY_EDITOR
        if (!enablePriorJournalState ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "NEWMAKAMISA" ||
            InventoryManager.Instance != null ||
            TaskManager.Instance != null)
            return;

        // The tutorial must be seen before the Journal unlock event is emitted.
        GameplaySystemTutorialState.SetSeen(GameplaySystemId.Journal, true);

        // Journal category components restore these flags in their own Awake.
        // Setting them here avoids gameplay unlock events and entry presentation.
        SessionStoryState.SetFlag("journal_observation_unlocked:ang_panaginip", true);
        SessionStoryState.SetFlag("journal_observation_unlocked:ang_hindi_natapos_na_akda", true);
        SessionStoryState.SetFlag("journal_observation_unlocked:mga_bulung_bulungan_sa_pili", true);
        SessionStoryState.SetFlag("journal_fragment_unlocked:ang_lumang_sulatin", true);
        GameplaySystemState.SetUnlocked(GameplaySystemId.Journal, true);
#endif
    }
}
