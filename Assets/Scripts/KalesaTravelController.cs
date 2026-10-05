using System;
using System.Collections;
using System.Collections.Generic;
using DialogueEditor;
using TMPro;
using UnityEngine;

/// <summary>Runs destination selection and same-scene kalesa travel for NEWMAKAMISA.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class KalesaTravelController : MonoBehaviour
{
    private const string AndayRecollectionReturnPointName = "AndayRecollectionReturnPoint";

    public const string TravelUnlockedFlag = "makamisa_kalesa_travel_unlocked";
    public const string CurrentDestinationStateId = "makamisa_kalesa_current_destination";
    public const string PendingDestinationStateId = "makamisa_kalesa_pending_destination";
    public const string CurrentDestinationFlagPrefix = "makamisa_kalesa_at_";

    [Serializable]
    public sealed class DestinationEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Transform kalesaStop;
        [SerializeField] private Transform kutseroStop;
        [SerializeField] private Transform miguelStop;

        public string Id => id;
        public string DisplayName => displayName;
        public Transform KalesaStop => kalesaStop;
        public Transform KutseroStop => kutseroStop;
        public Transform MiguelStop => miguelStop;
    }

    [Header("Existing characters and travel system")]
    [SerializeField] private Transform kalesaRoot;
    [SerializeField] private Transform kutseroRoot;
    [SerializeField] private Transform miguelRoot;
    [SerializeField] private Rigidbody miguelRigidbody;
    [SerializeField] private NPCConversationTrigger kutseroTrigger;
    [SerializeField] private FadeController fadeController;
    [SerializeField] private MonoBehaviour[] gameplayBehavioursToDisable =
        Array.Empty<MonoBehaviour>();
    [SerializeField] private DestinationEntry[] destinations = Array.Empty<DestinationEntry>();

    [Header("Boarding interaction")]
    [SerializeField] private SphereCollider boardingTrigger;
    [SerializeField] private GameObject boardingPrompt;

    [Header("Travel presentation")]
    [SerializeField] private AudioClip travelAudioClip;
    [SerializeField] private AudioSource travelAudioSource;
    [SerializeField, Min(0f)] private float travelBlackScreenDuration = 3f;

    private StorySequenceToken storySequenceToken;
    private NPCConversationTrigger subscribedKutseroTrigger;
    private Coroutine pendingDestinationRoutine;
    private Coroutine travelRoutine;
    private bool[] previousBehaviourStates;
    private bool previousRigidbodyKinematic;
    private bool rigidbodyStateCaptured;
    private bool playerInsideBoardingArea;
    private bool traveling;
    private bool enteredFromAndayRecollectionReturn;
    private bool fadeNeedsRecovery;
    private Vector3 miguelBodyRestingLocalPosition;
    private Quaternion miguelBodyRestingLocalRotation;
    private bool hasMiguelBodyRestingLocalPose;
    private string selectedDestinationId;
    private string pendingDestinationId;
    private string relocatedDestinationId;

    private void Awake()
    {
        // PlayerSpawner consumes this transient scene-entry intent in Start.
        // Capture it here so the delayed Kalesa restore can preserve Miguel's
        // recollection return placement without changing normal travel behavior.
        enteredFromAndayRecollectionReturn = string.Equals(
            SpawnData.spawnPointName,
            AndayRecollectionReturnPointName,
            StringComparison.Ordinal);

        CaptureMiguelBodyRestingLocalPose();

        if (boardingTrigger == null)
            boardingTrigger = GetComponent<SphereCollider>();

        if (boardingTrigger != null)
            boardingTrigger.isTrigger = true;

        ConfigureBoardingPrompt();

        if (travelAudioClip != null && travelAudioSource == null)
            travelAudioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        BindKutseroTrigger();
    }

    private void Start()
    {
        BindKutseroTrigger();
        ConfigureBoardingPrompt();

        string currentId = SessionStoryState.GetString(CurrentDestinationStateId);
        SetCurrentDestinationFlags(currentId);

        pendingDestinationId = SessionStoryState.GetString(PendingDestinationStateId);
        if (!IsDestinationValid(pendingDestinationId) ||
            !SessionStoryState.GetFlag(TravelUnlockedFlag) ||
            string.Equals(pendingDestinationId, currentId, StringComparison.Ordinal))
            SetPendingDestination(string.Empty);

        if (SessionStoryState.GetFlag(TravelUnlockedFlag) &&
            IsDestinationValid(currentId))
        {
            // Allow other scene-entry Start methods to finish before restoring the
            // session's actual stop (the side-task controller also restores Kutsero).
            StartCoroutine(RestoreCurrentStopAfterSceneInitialization(currentId));
        }
    }

    private void OnDisable()
    {
        if (subscribedKutseroTrigger != null)
            subscribedKutseroTrigger.ConversationFinished -= HandleKutseroConversationFinished;
        subscribedKutseroTrigger = null;

        if (pendingDestinationRoutine != null)
        {
            StopCoroutine(pendingDestinationRoutine);
            pendingDestinationRoutine = null;
        }
        selectedDestinationId = string.Empty;

        if (travelRoutine != null)
        {
            StopCoroutine(travelRoutine);
            travelRoutine = null;
        }

        if (!string.IsNullOrEmpty(relocatedDestinationId))
            StoreCurrentDestination(relocatedDestinationId);

        RecoverFromInterruptedTravel();
        playerInsideBoardingArea = false;
        SetPromptVisible(false);
    }

    private void BindKutseroTrigger()
    {
        if (subscribedKutseroTrigger == kutseroTrigger)
            return;

        if (subscribedKutseroTrigger != null)
            subscribedKutseroTrigger.ConversationFinished -= HandleKutseroConversationFinished;

        subscribedKutseroTrigger = kutseroTrigger;
        if (subscribedKutseroTrigger != null)
            subscribedKutseroTrigger.ConversationFinished += HandleKutseroConversationFinished;
    }

    private void ConfigureBoardingPrompt()
    {
        if (boardingPrompt == null)
            return;

        GameplayHUDTarget.AttachTo(boardingPrompt);
        if (!CanBoard())
            boardingPrompt.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInsideBoardingArea = true;
            RefreshPrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInsideBoardingArea = false;
            RefreshPrompt();
        }
    }

    private void Update()
    {
        RefreshPrompt();

        if (playerInsideBoardingArea && CanBoard() &&
            !StorySequenceCoordinator.IsStorySequenceActive &&
            Input.GetKeyDown(KeyCode.E))
        {
            BeginTravel();
        }
    }

    /// <summary>Persistent dialogue-event callback for the Bungad ng Bayan choice.</summary>
    public void SelectBungadNgBayan()
    {
        SelectDestination("bungad_ng_bayan");
    }

    /// <summary>Persistent dialogue-event callback for the Plaza choice.</summary>
    public void SelectPlaza()
    {
        SelectDestination("plaza");
    }

    /// <summary>Persistent dialogue-event callback for the Dulo ng Bayan choice.</summary>
    public void SelectDuloNgBayan()
    {
        SelectDestination("dulo_ng_bayan");
    }

    /// <summary>Persistent dialogue-event callback for Huwag muna.</summary>
    public void SelectNoDestination()
    {
        selectedDestinationId = string.Empty;
        SetPendingDestination(string.Empty);
    }

    private void HandleKutseroConversationFinished()
    {
        if (traveling || pendingDestinationRoutine != null ||
            !SessionStoryState.GetFlag(TravelUnlockedFlag) ||
            !IsDestinationValid(selectedDestinationId))
            return;

        string chosenId = selectedDestinationId;
        pendingDestinationRoutine = StartCoroutine(
            SetPendingDestinationAfterDialogueCloses(chosenId));
    }

    private IEnumerator SetPendingDestinationAfterDialogueCloses(string chosenId)
    {
        yield return WaitForDialogueToClose();

        if (string.Equals(selectedDestinationId, chosenId, StringComparison.Ordinal) &&
            SessionStoryState.GetFlag(TravelUnlockedFlag) &&
            IsDestinationValid(chosenId) &&
            !string.Equals(GetCurrentDestinationId(), chosenId, StringComparison.Ordinal))
        {
            SetPendingDestination(chosenId);
        }

        selectedDestinationId = string.Empty;
        pendingDestinationRoutine = null;
        RefreshPrompt();
    }

    private void SelectDestination(string destinationId)
    {
        if (!SessionStoryState.GetFlag(TravelUnlockedFlag) ||
            !IsDestinationValid(destinationId) ||
            string.Equals(GetCurrentDestinationId(), destinationId, StringComparison.Ordinal))
        {
            selectedDestinationId = string.Empty;
            return;
        }

        selectedDestinationId = destinationId;
    }

    private void BeginTravel()
    {
        if (traveling || pendingDestinationRoutine != null ||
            !SessionStoryState.GetFlag(TravelUnlockedFlag) ||
            fadeController == null || kalesaRoot == null || kutseroRoot == null ||
            miguelRoot == null || miguelRigidbody == null)
            return;

        DestinationEntry destination = FindDestination(pendingDestinationId);
        if (!HasDestinationTransforms(destination))
        {
            Debug.LogWarning("Kalesa travel destination is missing one or more stop references.", this);
            return;
        }

        traveling = true;
        relocatedDestinationId = string.Empty;
        SetPromptVisible(false);
        travelRoutine = StartCoroutine(TravelToDestination(destination));
    }

    private IEnumerator TravelToDestination(DestinationEntry destination)
    {
        try
        {
            storySequenceToken = StorySequenceCoordinator.Acquire(this);
            if (storySequenceToken == null)
            {
                Debug.LogWarning("Kalesa travel could not acquire story-sequence ownership.", this);
                yield break;
            }

            CaptureAndLockGameplay();
            fadeNeedsRecovery = true;
            yield return fadeController.FadeToBlack();
            yield return null;

            LogTravelTransformDiagnostics("Before relocation", destination);
            MoveActorsToDestination(destination);
            LogTravelTransformDiagnostics("Immediately after relocation", destination);
            relocatedDestinationId = destination.Id;

            if (travelAudioClip != null)
            {
                if (travelAudioSource == null)
                    travelAudioSource = GetComponent<AudioSource>();
                if (travelAudioSource == null)
                    travelAudioSource = gameObject.AddComponent<AudioSource>();

                travelAudioSource.PlayOneShot(travelAudioClip);
            }

            yield return WaitUnscaled(Mathf.Max(0f, travelBlackScreenDuration));
            yield return fadeController.FadeFromBlack();
            fadeNeedsRecovery = false;

            StoreCurrentDestination(destination.Id);
            SetPendingDestination(string.Empty);
            relocatedDestinationId = string.Empty;
        }
        finally
        {
            if (!string.IsNullOrEmpty(relocatedDestinationId))
            {
                StoreCurrentDestination(relocatedDestinationId);
                SetPendingDestination(string.Empty);
                relocatedDestinationId = string.Empty;
            }

            RecoverFromInterruptedTravel();
            LogTravelTransformDiagnostics("After Rigidbody and movement restoration", destination);
            travelRoutine = null;
        }
    }

    private void LogTravelTransformDiagnostics(string phase, DestinationEntry destination)
    {
        Transform playerBodyTransform = miguelRigidbody != null
            ? miguelRigidbody.transform
            : null;
        string localPosition = playerBodyTransform != null
            ? playerBodyTransform.localPosition.ToString()
            : "<null>";
        string rigidbodyPosition = miguelRigidbody != null
            ? miguelRigidbody.position.ToString()
            : "<null>";
        string rigidbodyKinematic = miguelRigidbody != null
            ? miguelRigidbody.isKinematic.ToString()
            : "<null>";
        string destinationId = destination != null ? destination.Id : "<null>";

        Debug.Log(
            $"[KALESA TRAVEL DEBUG] {phase}; destination={destinationId}; " +
            $"stops(Kalesa={FormatPosition(destination?.KalesaStop)}, " +
            $"Kutsero={FormatPosition(destination?.KutseroStop)}, " +
            $"Miguel={FormatPosition(destination?.MiguelStop)}); " +
            $"actors(Kalesa={FormatPosition(kalesaRoot)}, " +
            $"Kutsero={FormatPosition(kutseroRoot)}, " +
            $"Miguel={FormatPosition(miguelRoot)}, " +
            $"FInalChar1={FormatPosition(playerBodyTransform)}, " +
            $"FInalChar1.localPosition={localPosition}, " +
            $"Rigidbody.position={rigidbodyPosition}, " +
            $"Rigidbody.isKinematic={rigidbodyKinematic})",
            this);
    }

    private static string FormatPosition(Transform target)
    {
        return target != null ? target.position.ToString() : "<null>";
    }

    private IEnumerator RestoreCurrentStopAfterSceneInitialization(string destinationId)
    {
        yield return null;

        DestinationEntry destination = FindDestination(destinationId);
        if (!SessionStoryState.GetFlag(TravelUnlockedFlag) ||
            traveling || !HasDestinationTransforms(destination))
            yield break;

        if (enteredFromAndayRecollectionReturn)
        {
            kalesaRoot.SetPositionAndRotation(
                destination.KalesaStop.position,
                destination.KalesaStop.rotation);
            kutseroRoot.SetPositionAndRotation(
                destination.KutseroStop.position,
                destination.KutseroStop.rotation);
        }
        else
        {
            MoveActorsToDestination(destination);
        }

        Physics.SyncTransforms();
    }

    private IEnumerator WaitForDialogueToClose()
    {
        ConversationManager manager = ConversationManager.Instance;
        while (manager != null && manager.IsConversationActive)
        {
            manager = ConversationManager.Instance;
            yield return null;
        }

        yield return null;
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool CanBoard()
    {
        ConversationManager manager = ConversationManager.Instance;
        return !traveling && pendingDestinationRoutine == null &&
               !string.IsNullOrWhiteSpace(pendingDestinationId) &&
               SessionStoryState.GetFlag(TravelUnlockedFlag) &&
               IsDestinationValid(pendingDestinationId) &&
               !string.Equals(GetCurrentDestinationId(), pendingDestinationId, StringComparison.Ordinal) &&
               (manager == null || !manager.IsConversationActive);
    }

    private void RefreshPrompt()
    {
        SetPromptVisible(playerInsideBoardingArea && CanBoard() &&
                         !StorySequenceCoordinator.IsStorySequenceActive);
    }

    private void SetPromptVisible(bool visible)
    {
        if (boardingPrompt != null && boardingPrompt.activeSelf != visible)
            boardingPrompt.SetActive(visible);
    }

    private void CaptureAndLockGameplay()
    {
        if (gameplayBehavioursToDisable != null)
        {
            previousBehaviourStates = new bool[gameplayBehavioursToDisable.Length];
            for (int i = 0; i < gameplayBehavioursToDisable.Length; i++)
            {
                MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
                previousBehaviourStates[i] = behaviour != null && behaviour.enabled;
                if (behaviour != null)
                    behaviour.enabled = false;
            }
        }

        if (miguelRigidbody != null)
        {
            previousRigidbodyKinematic = miguelRigidbody.isKinematic;
            rigidbodyStateCaptured = true;
            miguelRigidbody.linearVelocity = Vector3.zero;
            miguelRigidbody.angularVelocity = Vector3.zero;
            miguelRigidbody.isKinematic = true;
        }
    }

    private void MoveActorsToDestination(DestinationEntry destination)
    {
        kalesaRoot.SetPositionAndRotation(
            destination.KalesaStop.position,
            destination.KalesaStop.rotation);
        kutseroRoot.SetPositionAndRotation(
            destination.KutseroStop.position,
            destination.KutseroStop.rotation);
        miguelRoot.SetPositionAndRotation(
            destination.MiguelStop.position,
            destination.MiguelStop.rotation);

        if (miguelRigidbody != null)
        {
            Transform miguelBodyTransform = miguelRigidbody.transform;
            if (hasMiguelBodyRestingLocalPose)
            {
                miguelBodyTransform.SetLocalPositionAndRotation(
                    miguelBodyRestingLocalPosition,
                    miguelBodyRestingLocalRotation);
            }

            miguelRigidbody.position = miguelRigidbody.transform.position;
            miguelRigidbody.rotation = miguelRigidbody.transform.rotation;
            miguelRigidbody.linearVelocity = Vector3.zero;
            miguelRigidbody.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }

    private void CaptureMiguelBodyRestingLocalPose()
    {
        if (miguelRoot == null || miguelRigidbody == null)
            return;

        Transform miguelBodyTransform = miguelRigidbody.transform;
        if (miguelBodyTransform == miguelRoot || !miguelBodyTransform.IsChildOf(miguelRoot))
        {
            Debug.LogWarning(
                "Kalesa travel could not capture Miguel's resting body pose because the " +
                "configured Rigidbody is not a child of Miguel.", this);
            return;
        }

        // Capture during Awake, before gameplay movement or physics can move the
        // Rigidbody child independently of the logical Miguel root.
        miguelBodyRestingLocalPosition = miguelBodyTransform.localPosition;
        miguelBodyRestingLocalRotation = miguelBodyTransform.localRotation;
        hasMiguelBodyRestingLocalPose = true;
    }

    private void StoreCurrentDestination(string destinationId)
    {
        SessionStoryState.SetString(CurrentDestinationStateId, destinationId);
        SetCurrentDestinationFlags(destinationId);
    }

    private void SetCurrentDestinationFlags(string currentId)
    {
        if (destinations == null)
            return;

        foreach (DestinationEntry destination in destinations)
        {
            if (destination == null || string.IsNullOrWhiteSpace(destination.Id))
                continue;

            SessionStoryState.SetFlag(
                CurrentDestinationFlagPrefix + destination.Id,
                string.Equals(destination.Id, currentId, StringComparison.Ordinal));
        }
    }

#if UNITY_EDITOR
    internal void RefreshAfterDevelopmentTravelStateReset()
    {
        selectedDestinationId = string.Empty;
        pendingDestinationId = SessionStoryState.GetString(PendingDestinationStateId);
        SetCurrentDestinationFlags(SessionStoryState.GetString(CurrentDestinationStateId));
        RefreshPrompt();
    }
#endif

    private void SetPendingDestination(string destinationId)
    {
        pendingDestinationId = destinationId ?? string.Empty;
        SessionStoryState.SetString(PendingDestinationStateId, pendingDestinationId);
        RefreshPrompt();
    }

    private string GetCurrentDestinationId()
    {
        return SessionStoryState.GetString(CurrentDestinationStateId);
    }

    private bool IsDestinationValid(string destinationId)
    {
        return HasDestinationTransforms(FindDestination(destinationId));
    }

    private DestinationEntry FindDestination(string destinationId)
    {
        if (string.IsNullOrWhiteSpace(destinationId) || destinations == null)
            return null;

        foreach (DestinationEntry destination in destinations)
        {
            if (destination != null && string.Equals(
                destination.Id?.Trim(), destinationId.Trim(), StringComparison.Ordinal))
                return destination;
        }

        return null;
    }

    private static bool HasDestinationTransforms(DestinationEntry destination)
    {
        return destination != null && destination.KalesaStop != null &&
               destination.KutseroStop != null && destination.MiguelStop != null;
    }

    private void RecoverFromInterruptedTravel()
    {
        if (rigidbodyStateCaptured && miguelRigidbody != null)
        {
            miguelRigidbody.linearVelocity = Vector3.zero;
            miguelRigidbody.angularVelocity = Vector3.zero;
            miguelRigidbody.isKinematic = previousRigidbodyKinematic;
        }
        rigidbodyStateCaptured = false;

        if (previousBehaviourStates != null && gameplayBehavioursToDisable != null)
        {
            for (int i = 0; i < gameplayBehavioursToDisable.Length &&
                 i < previousBehaviourStates.Length; i++)
            {
                MonoBehaviour behaviour = gameplayBehavioursToDisable[i];
                if (behaviour != null)
                    behaviour.enabled = previousBehaviourStates[i];
            }
        }
        previousBehaviourStates = null;

        if (storySequenceToken != null)
        {
            storySequenceToken.Release();
            storySequenceToken = null;
        }

        if (fadeNeedsRecovery && fadeController != null && fadeController.isActiveAndEnabled)
            fadeController.StartCoroutine(fadeController.FadeFromBlack());

        fadeNeedsRecovery = false;
        traveling = false;
        RefreshPrompt();
    }
}
