using UnityEngine;

/// <summary>Runtime-only world-space following for the dedicated Part 3 focus marker.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CameraFocusPoint))]
public sealed class Part3MiguelCameraFocusFollow : MonoBehaviour
{
    [SerializeField, Tooltip("Miguel's actual scene-local locomotion Transform (FInalChar1), also used by PlayerSpawner and the normal camera.")]
    private Transform player;
    [SerializeField, Tooltip("The existing scene-local population readiness gate; ordinary Part 1 must never follow.")]
    private NewMakamisaPart3PopulationPresentation presentation;

    private Vector3 followOffset;
    private Vector3 previousPlayerPosition;
    private bool initialized;

    private void Awake()
    {
        if (player == null || presentation == null ||
            player.gameObject.scene != gameObject.scene ||
            presentation.gameObject.scene != gameObject.scene ||
            player == transform || transform.IsChildOf(player))
        {
            Debug.LogError("Part 3 focus follow requires a separate focus marker and verified scene-local player/presentation references.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || gameObject.scene.path != "Assets/Scenes/NEWMAKAMISA.unity" ||
            player == null || presentation == null || !presentation.isActiveAndEnabled ||
            !presentation.IsPart3PresentationReady)
            return;

        Vector3 playerPosition = player.position;
        if (!initialized)
        {
            // PlayerSpawner teleports in Start and then clears the route.
            // LateUpdate samples the actual arrival pose, never the pre-spawn player.
            if (!string.IsNullOrEmpty(SpawnData.spawnPointName))
                return;

            followOffset = transform.position - playerPosition;
            previousPlayerPosition = playerPosition;
            initialized = true;
            return; // Preserve the approved marker pose exactly at capture.
        }

        if (playerPosition.Equals(previousPlayerPosition))
            return;

        // Rebuild from the fixed original offset, never by accumulating deltas.
        transform.position = playerPosition + followOffset;
        previousPlayerPosition = playerPosition;
    }
}
