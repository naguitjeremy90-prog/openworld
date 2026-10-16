using UnityEngine;

/// <summary>
/// Editor-only direct unlock for testing Pagsusuri in the configured scene.
/// Does not alter task or story progression.
/// </summary>
[DisallowMultipleComponent]
public sealed class PagsusuriDevelopmentTesting : MonoBehaviour
{
    [SerializeField] private bool enableDirectPagsusuriTesting;
    [SerializeField] private bool suppressClarityTutorial = true;
    [SerializeField] private bool ensureInventoryForDirectTesting = true;
    [SerializeField] private InventoryManager inventoryManagerPrefab;
    [SerializeField] private bool unlockInventoryForDirectTesting = true;

    private void Start()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || !enableDirectPagsusuriTesting)
            return;

        if (ensureInventoryForDirectTesting && InventoryManager.Instance == null)
        {
            if (inventoryManagerPrefab == null ||
                !UnityEditor.EditorUtility.IsPersistent(inventoryManagerPrefab))
            {
                Debug.LogError(
                    "Direct Present Pili reward testing requires the InventorySystem prefab asset.",
                    this);
            }
            else
            {
                Instantiate(inventoryManagerPrefab.gameObject);
            }
        }

        if (unlockInventoryForDirectTesting)
        {
            GameplaySystemTutorialManager tutorialManager =
                GameplaySystemTutorialManager.Instance;
            if (tutorialManager != null)
            {
                tutorialManager.SuppressTutorialForDevelopment(
                    GameplaySystemId.Inventory);
            }

            GameplaySystemState.SetUnlocked(GameplaySystemId.Inventory, true);
        }

        if (suppressClarityTutorial)
        {
            GameplaySystemTutorialManager tutorialManager =
                GameplaySystemTutorialManager.Instance;
            if (tutorialManager != null)
            {
                tutorialManager.SuppressTutorialForDevelopment(
                    GameplaySystemId.Clarity);
            }
        }

        GameplaySystemState.SetUnlocked(
            GameplaySystemId.Clarity,
            true);
#endif
    }
}
