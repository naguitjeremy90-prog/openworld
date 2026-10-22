using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Alaala.Saving
{
    /// <summary>Only the staged DraftWorld scene. No changes to authored scenes or movement code.</summary>
    internal sealed class AlaalaResumeWorldGate
    {
        private readonly DialogueMovementLock movement;
        private readonly Rigidbody body;
        private readonly RigidbodyConstraints previousConstraints;
        private readonly Collider[] colliders;
        private readonly bool[] colliderStates;
        private readonly Transform spawn, player;
        private readonly Scene scene;

        internal AlaalaResumeWorldGate(Scene scene, string route)
        {
            this.scene = scene;
            var roots = scene.GetRootGameObjects();
            foreach (var opening in roots.SelectMany(r => r.GetComponentsInChildren<SelfDialogueTrigger>(true)))
                opening.RestoreCompletedOpeningTrigger();
            var spawners = roots.SelectMany(r => r.GetComponentsInChildren<PlayerSpawner>(true)).Where(s => s.isActiveAndEnabled).ToArray();
            var players = spawners.Select(s => s.SaveRestorationPlayer).Distinct().ToArray();
            var routes = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name == route && t.gameObject.activeInHierarchy).ToArray();
            if (players.Length != 1 || players[0] == null || routes.Length != 1) throw new InvalidOperationException("Authored player/spawn route is ambiguous or missing.");
            player = players[0]; spawn = routes[0]; body = player.GetComponent<Rigidbody>();
            movement = roots.SelectMany(r => r.GetComponentsInChildren<DialogueMovementLock>(true)).SingleOrDefault(m => m.SaveRestorationPlayer == player);
            if (body == null || movement == null) throw new InvalidOperationException("Authored player physics/movement lock is unavailable.");
            colliders = player.GetComponentsInChildren<Collider>(true); colliderStates = colliders.Select(c => c.enabled).ToArray();
            if (body.isKinematic) throw new InvalidOperationException("Unexpected kinematic player configuration.");
            previousConstraints = body.constraints;
        }

        internal void Hold()
        {
            movement.HoldMovementForStorySequence();
            body.constraints = RigidbodyConstraints.FreezeAll;
            foreach (var collider in colliders) collider.enabled = false;
            var roots = scene.GetRootGameObjects();
            // This legacy tutorial has no durable completion API and otherwise replays on
            // every scene entry. Both supported checkpoints are after its opening chapter.
            foreach (var tutorial in roots.SelectMany(r => r.GetComponentsInChildren<TalkTutorial>(true)))
            { tutorial.enabled = false; if (tutorial.talkGroup != null) tutorial.talkGroup.gameObject.SetActive(false); }
            // The audited legacy director has only unbound Cinemachine shots, no markers
            // or story bindings. Refuse different content instead of skipping a real sequence.
            foreach (var legacy in roots.SelectMany(r => r.GetComponentsInChildren<StopShots>(true)).Where(s => s.isActiveAndEnabled))
            {
                var director = legacy.director;
                if (legacy.playerMovementScript != null || legacy.gameplayUI != null || legacy.shot1 != null || legacy.shot2 != null || legacy.shot3 != null ||
                    director == null || !(director.playableAsset is TimelineAsset timeline) ||
                    timeline.markerTrack != null && timeline.markerTrack.GetMarkers().Any() ||
                    timeline.GetOutputTracks().Any(t => t.GetType().Name != "CinemachineTrack" || director.GetGenericBinding(t) != null || t.GetMarkers().Any()))
                    throw new InvalidOperationException("Legacy startup Timeline no longer matches the audited unbound content.");
                legacy.enabled = false; director.Stop(); director.enabled = false;
            }
        }

        internal bool IsReady(out string error)
        {
            error = null;
            if (player == null || spawn == null || body == null || movement == null || !string.IsNullOrEmpty(SpawnData.spawnPointName))
            { error = "Authored spawn has not completed."; return false; }
            if ((player.position - spawn.position).sqrMagnitude > .01f)
            { error = "PlayerSpawner did not place the player at the approved route."; return false; }
            var days = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DayNightManager>(true)).Where(d => d.isActiveAndEnabled).ToArray();
            if (days.Length != 1 || !days[0].IsDay)
            { error = "Morning world initialization is incomplete."; return false; }
            return true;
        }

        internal void Release()
        {
            if (body != null) { body.constraints = previousConstraints; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
            if (movement != null) movement.ReleaseStorySequenceHold();
        }
    }
}
