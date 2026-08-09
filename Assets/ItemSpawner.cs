using UnityEngine;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        public GameObject itemPrefab;
        public Transform spawnPoint;
        public int amount = 1;
        public bool spawnOnStart = true;
    }

    [Header("Spawn Entries")]
    public List<SpawnEntry> spawnEntries = new List<SpawnEntry>();

    private void Start()
    {
        foreach (SpawnEntry entry in spawnEntries)
        {
            if (entry.spawnOnStart)
                SpawnItem(entry);
        }
    }

    public void SpawnItem(SpawnEntry entry)
    {
        if (entry.itemPrefab == null || entry.spawnPoint == null) return;

        for (int i = 0; i < entry.amount; i++)
            Instantiate(entry.itemPrefab, entry.spawnPoint.position, entry.spawnPoint.rotation);
    }
}