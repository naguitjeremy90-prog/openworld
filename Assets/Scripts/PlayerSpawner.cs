using UnityEngine;
using System.Collections;

public class PlayerSpawner : MonoBehaviour
{
    private const string AndayRecollectionReturnPoint = "AndayRecollectionReturnPoint";

    [SerializeField] private Transform player;
    [SerializeField] private MonoBehaviour movementToHoldDuringReturn;

    private void Start()
    {
        if (string.IsNullOrEmpty(SpawnData.spawnPointName))
            return;

        bool returningFromAndayRecollection =
            SpawnData.spawnPointName == AndayRecollectionReturnPoint;
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

            if (returningFromAndayRecollection && movementToHoldDuringReturn != null)
                StartCoroutine(WaitForIncomingTransitionThenRestoreMovement());

            Debug.Log("Peter spawned at: " + spawnPoint.transform.position);

            SpawnData.spawnPointName = "";
        }
        else
        {
            Debug.LogWarning("Spawn point not found: " + SpawnData.spawnPointName);
        }
    }

    private IEnumerator WaitForIncomingTransitionThenRestoreMovement()
    {
        bool movementWasEnabled = movementToHoldDuringReturn.enabled;
        movementToHoldDuringReturn.enabled = false;

        // The persistent iris owns the cross-scene handoff. Release movement
        // only after its opening animation has made the returned scene visible.
        IrisTransitionController transition = IrisTransitionController.Instance;
        while (transition != null && transition.IsCovered)
            yield return null;

        if (movementToHoldDuringReturn != null)
            movementToHoldDuringReturn.enabled = movementWasEnabled;
    }
}
