using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private Transform player;

    private void Start()
    {
        if (string.IsNullOrEmpty(SpawnData.spawnPointName))
            return;

        GameObject spawnPoint = GameObject.Find(SpawnData.spawnPointName);

        if (spawnPoint != null)
        {
            Rigidbody rb = player.GetComponent<Rigidbody>();

            if (rb != null)
            {
                // Teleport through the Rigidbody instead of only the Transform
                rb.position = spawnPoint.transform.position;
                rb.rotation = spawnPoint.transform.rotation;

                // Stop any leftover movement from before
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            else
            {
                player.position = spawnPoint.transform.position;
                player.rotation = spawnPoint.transform.rotation;
            }

            Physics.SyncTransforms();

            Debug.Log("Peter spawned at: " + spawnPoint.transform.position);

            SpawnData.spawnPointName = "";
        }
        else
        {
            Debug.LogWarning("Spawn point not found: " + SpawnData.spawnPointName);
        }
    }
}