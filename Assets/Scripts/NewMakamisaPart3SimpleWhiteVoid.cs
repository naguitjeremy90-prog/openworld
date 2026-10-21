using UnityEngine;

/// <summary>Manual, Part 3-only moving white wall prototype.</summary>
[DisallowMultipleComponent]
public sealed class NewMakamisaPart3SimpleWhiteVoid : MonoBehaviour
{
    [SerializeField, Tooltip("Opt in to manual white wall controls during a ready Part 3 Editor Play Mode session. Never starts automatically.")]
    private bool enableWhiteWallDevelopmentTesting = false;

    [SerializeField, Tooltip("Verified population presentation in this scene. Its readiness API owns Part 3 eligibility.")]
    private NewMakamisaPart3PopulationPresentation presentation;
    [SerializeField, Tooltip("The dedicated WhiteVoidWall renderer owned by this root.")]
    private MeshRenderer wallRenderer;

    [SerializeField, Min(1f), Tooltip("Wall height in world units, applied when the session starts.")]
    private float wallHeight = 100f;
    [SerializeField, Min(1f), Tooltip("Wall width across world Z in world units, applied when the session starts.")]
    private float wallWidthAcrossZ = 800f;
    [SerializeField, Tooltip("Initial world X, applied when the session starts. Keep behind Part3Spawn.")]
    private float startingXPosition = 150f;
    [SerializeField, Min(0f), Tooltip("Movement speed toward world +X in world units per second.")]
    private float travelSpeed = 5f;
    [SerializeField, Tooltip("Stop the wall at Maximum X Endpoint when enabled.")]
    private bool useMaximumXEndpoint = true;
    [SerializeField, Tooltip("Maximum world X. Values below the starting X stop at the starting X.")]
    private float maximumXEndpoint = 1000f;

    [Header("White camera presentation")]
    [SerializeField, Tooltip("The actual rendering Camera with the existing CinemachineBrain. Tracking is never changed.")]
    private Camera gameplayCamera;
    [SerializeField, Tooltip("Scene-local Miguel movement/follow target. Only renderer GameObjects beneath this transform change layer temporarily.")]
    private Transform player;
    [SerializeField, Tooltip("Enter white mode when wall X reaches player X plus this offset, in world units.")]
    private float crossingOffset = 0f;

    private const string PlayerVisualLayer = "Part3PlayerVisuals";
    private bool crossingHandled;
    private bool whiteCameraActive;
    private Camera overriddenCamera;
    private CameraClearFlags originalClearFlags;
    private Color originalBackgroundColor;
    private int originalCullingMask;
    private GameObject[] isolatedVisuals;
    private int[] originalVisualLayers;

    private Transform wall;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private bool initialized;
    private bool moving;
    private MonoBehaviour storyOwner;
    private bool storyControlled;
    private bool storyHeld;
    private bool encounterGuard;
    private float encounterLimitX;
    private float encounterSlowdownDistance = 8f;
    private float encounterNearSpeed = 0.05f;
    private CapsuleCollider encounterPlayerCapsule;
    private bool approachingEncounter;
    private float encounterTargetX;
    private bool chapter11Takeover;
    private float chapter11Speed;
    private bool chapter11Staged;

    public bool IsWhiteCameraActive => whiteCameraActive;
    public bool HasCrossedPlayer => crossingHandled;
    public float BoundaryX => wall != null ? wall.position.x : startingXPosition;

    public bool IsChapter11TakeoverActive(NewMakamisaPart3Chapter11 owner) =>
        IsControlledBy(owner) && chapter11Takeover && !storyHeld && moving;

    /// <summary>One deliberate Chapter 11-only reposition, then the existing physical takeover.</summary>
    public bool TryStageChapter11Takeover(NewMakamisaPart3Chapter11 owner, float stagingX,
        float speed, out string reason)
    {
        reason = null;
        if (owner == null || !owner.AllowsWallStaging || !IsHeldBy(owner) || !IsQualifiedPart3Session())
            reason = "Staging requires the Chapter 11 owner, completed 11A, cinematic control and the qualified wall hold.";
        else if (chapter11Staged || chapter11Takeover)
            reason = "Chapter 11 has already staged this wall ownership; duplicate staging was rejected.";
        else if (player == null || whiteCameraActive || crossingHandled ||
            BoundaryX >= Mathf.Min(player.position.x, player.position.x + crossingOffset))
            reason = "The wall has already reached/passed Miguel, white mode is active, or Miguel is missing.";
        else if (float.IsNaN(stagingX) || float.IsInfinity(stagingX))
            reason = "Chapter 11 Wall Staging X must be finite.";
        else if (stagingX < BoundaryX)
            reason = $"Chapter 11 Wall Staging X ({stagingX}) is behind current wall X ({BoundaryX}); backward staging is forbidden.";
        else if (stagingX >= Mathf.Min(player.position.x, player.position.x + crossingOffset) - 1f)
            reason = $"Chapter 11 Wall Staging X ({stagingX}) must stay more than one unit behind Miguel/crossing X ({player.position.x + crossingOffset}).";
        else if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < 1f)
            reason = "Chapter 11 Takeover Speed must be finite and at least 1.";
        else if (useMaximumXEndpoint &&
            Mathf.Max(originalPosition.x, maximumXEndpoint) < player.position.x + crossingOffset)
            reason = "The authored maximum X endpoint is before Miguel; it was not expanded.";
        else if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled ||
            gameplayCamera.gameObject.scene != gameObject.scene)
            reason = "The actual scene-local rendering Camera is unavailable for staging validation.";
        else if (stagingX >= gameplayCamera.transform.position.x - gameplayCamera.nearClipPlane)
            reason = "Staging must remain behind the rendering Camera in world X, outside its near-plane margin.";
        if (reason != null) return false;

        // A single center-ray/plane test is a conservative fixed-coordinate safety
        // check, not a dynamic frustum or occlusion solver. Keep the center in world view.
        Bounds stagedBounds = wallRenderer.bounds;
        stagedBounds.center += Vector3.right * (stagingX - BoundaryX);
        Ray center = gameplayCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Mathf.Abs(center.direction.x) > 0.00001f)
        {
            float distance = (stagedBounds.center.x - center.origin.x) / center.direction.x;
            Vector3 point = center.GetPoint(distance);
            if (distance >= 0f && distance <= gameplayCamera.farClipPlane &&
                point.y >= stagedBounds.min.y && point.y <= stagedBounds.max.y &&
                point.z >= stagedBounds.min.z && point.z <= stagedBounds.max.z)
            {
                reason = "Configured staging would already cover the camera center. Choose a safer smaller staging X.";
                return false;
            }
        }

        // All prerequisites are validated before the sole wall owner moves anything.
        // Reuse the existing speed/cap/crossing path without altering authored defaults.
        if (!TryBeginChapter11Takeover(owner, speed, out reason)) return false;
        Vector3 position = wall.position;
        position.x = stagingX;
        wall.position = position;
        chapter11Staged = true;
        return true;
    }

    /// <summary>Uses this controller's normal +X update and physical player crossing.</summary>
    public bool TryBeginChapter11Takeover(NewMakamisaPart3Chapter11 owner, float speed, out string reason)
    {
        reason = null;
        if (!IsHeldBy(owner) || !IsQualifiedPart3Session())
            reason = "Chapter 11 does not hold the qualified scene-local wall.";
        else if (player == null || whiteCameraActive || crossingHandled ||
            BoundaryX >= player.position.x + crossingOffset)
            reason = "The wall has already crossed Miguel, white mode is active, or Miguel is missing.";
        else if (float.IsNaN(speed) || float.IsInfinity(speed) || speed < 1f)
            reason = "Chapter 11 Takeover Speed must be finite and at least 1.";
        else if (useMaximumXEndpoint &&
            Mathf.Max(originalPosition.x, maximumXEndpoint) < player.position.x + crossingOffset)
            reason = "The authored maximum X endpoint is before Miguel; it was not expanded.";
        if (reason != null) return false;
        chapter11Takeover = true;
        chapter11Speed = speed;
        encounterGuard = false;
        approachingEncounter = false;
        moving = true;
        storyHeld = false;
        return true;
    }

    public void CancelChapter11Takeover(NewMakamisaPart3Chapter11 owner)
    {
        if (!IsControlledBy(owner) || !chapter11Takeover) return;
        chapter11Takeover = false;
        chapter11Speed = 0f;
        storyHeld = true;
    }

    public bool IsControlledBy(MonoBehaviour owner) =>
        owner != null && storyControlled && storyOwner == owner && wall != null &&
        wall.gameObject.activeSelf && wallRenderer != null && wallRenderer.enabled;

    // Chapter 10 slows near Miguel; the fixed church boundary still protects Chapter 11.
    // Crossing stays suppressed for this guard, including player-driven penetration.
    public bool TryGuardEncounter(MonoBehaviour owner, float limitX, float slowdownDistance = 8f,
        float nearSpeed = 0.05f, CapsuleCollider playerCapsule = null)
    {
        if (!IsControlledBy(owner) || !IsQualifiedPart3Session() || whiteCameraActive ||
            float.IsNaN(limitX) || float.IsInfinity(limitX) ||
            float.IsNaN(slowdownDistance) || float.IsInfinity(slowdownDistance) || slowdownDistance <= 0f ||
            float.IsNaN(nearSpeed) || float.IsInfinity(nearSpeed) || nearSpeed <= 0f || nearSpeed > travelSpeed ||
            (playerCapsule != null && playerCapsule.transform != player))
            return false;
        encounterGuard = true;
        encounterLimitX = limitX;
        encounterSlowdownDistance = slowdownDistance;
        encounterNearSpeed = nearSpeed;
        encounterPlayerCapsule = playerCapsule;
        approachingEncounter = false;
        return true;
    }

    public bool TrySuspendEncounterGuard(MonoBehaviour owner)
    {
        if (!IsControlledBy(owner) || !IsQualifiedPart3Session()) return false;
        // Hold before releasing this owner's cap: crossing cannot run while suspended.
        storyHeld = true;
        encounterGuard = false;
        return true;
    }

    public bool TryTransferHeld(MonoBehaviour from, MonoBehaviour to)
    {
        if (!IsControlledBy(from) || !IsQualifiedPart3Session() || to == null ||
            to.gameObject.scene != gameObject.scene)
            return false;
        storyHeld = true;
        encounterGuard = false;
        chapter11Takeover = false;
        chapter11Speed = 0f;
        chapter11Staged = false;
        storyOwner = to;
        return true;
    }

    public bool TryApproachEncounter(MonoBehaviour owner, float targetX)
    {
        if (!IsHeldBy(owner) || !IsQualifiedPart3Session() || whiteCameraActive ||
            travelSpeed <= 0f || float.IsNaN(targetX) || float.IsInfinity(targetX) ||
            targetX < BoundaryX || (useMaximumXEndpoint && targetX > maximumXEndpoint))
            return false;
        encounterGuard = false;
        approachingEncounter = true;
        encounterTargetX = targetX;
        moving = true;
        storyHeld = false;
        return true;
    }

    /// <summary>Immediate cinematic takeover for the qualified owner holding the existing wall.</summary>
    public bool TryEnterEncounterWhite(MonoBehaviour owner)
    {
        if (!IsHeldBy(owner) || !IsQualifiedPart3Session())
            return false;
        EnterWhiteCamera();
        if (!whiteCameraActive) return false;
        crossingHandled = true;
        approachingEncounter = false;
        return true;
    }

    // The wall itself retains the approved visual state after its cinematic owner finishes.
    // The existing Reset/disable/unload paths remain the sole restoration implementation.
    public bool TryRetainWhitePresentation(MonoBehaviour owner)
    {
        if (!IsControlledBy(owner) || !whiteCameraActive || !IsQualifiedPart3Session())
            return false;
        storyHeld = true;
        storyOwner = this;
        return true;
    }

    /// <summary>Shows the existing boundary, stationary, for an explicitly owned Part 3 sequence.</summary>
    public bool TryStartHeld(MonoBehaviour owner)
    {
        if (!IsQualifiedPart3Session() || owner == null || owner.gameObject.scene != gameObject.scene ||
            (storyControlled && storyOwner != owner) || player == null || whiteCameraActive ||
            crossingHandled || wall.position.x >= player.position.x + crossingOffset)
            return false;

        storyOwner = owner;
        storyControlled = true;
        storyHeld = true;
        wall.gameObject.SetActive(true);
        wallRenderer.enabled = true;
        moving = !useMaximumXEndpoint || wall.position.x < Mathf.Max(originalPosition.x, maximumXEndpoint);
        return true;
    }

    public bool IsHeldBy(MonoBehaviour owner)
    {
        return owner != null && storyControlled && storyOwner == owner && storyHeld && wall != null &&
            wall.gameObject.activeSelf && wallRenderer != null && wallRenderer.enabled;
    }

    public bool TryHold(MonoBehaviour owner)
    {
        if (owner == null || !storyControlled || storyOwner != owner || !IsQualifiedPart3Session())
            return false;
        storyHeld = true;
        return true;
    }

    public bool TryResume(MonoBehaviour owner)
    {
        if (!IsHeldBy(owner) || !IsQualifiedPart3Session())
            return false;
        storyHeld = false;
        return true;
    }

    public void CancelStoryControl(MonoBehaviour owner)
    {
        if (owner != null && storyControlled && storyOwner == owner)
            RestoreWall();
    }

    private void Awake()
    {
        if (wallRenderer == null || wallRenderer.transform.parent != transform)
            return;

        wall = wallRenderer.transform;
        wallRenderer.enabled = false;
        wall.gameObject.SetActive(false);
        Vector3 position = wall.position;
        position.x = startingXPosition;
        wall.position = position;
        wall.rotation = Quaternion.Euler(0f, 90f, 0f);
        wall.localScale = new Vector3(Mathf.Max(1f, wallWidthAcrossZ), Mathf.Max(1f, wallHeight), 1f);
        originalPosition = wall.position;
        originalRotation = wall.rotation;
        originalScale = wall.localScale;
        initialized = true;
    }

    private bool IsPart3Ready()
    {
#if UNITY_EDITOR
        return IsQualifiedPart3Session();
#else
        return false;
#endif
    }

    private bool IsQualifiedPart3Session()
    {
        return Application.isPlaying && initialized && isActiveAndEnabled &&
            gameObject.scene.path == "Assets/Scenes/NEWMAKAMISA.unity" &&
            presentation != null && presentation.gameObject.scene == gameObject.scene &&
            presentation.isActiveAndEnabled && presentation.IsPart3PresentationReady &&
            SessionStoryState.GetFlag(MaestroBenSulatinLightEncounter.ArrivalFlag);
    }

    private bool IsCurrentSessionAllowed()
    {
        return storyControlled
            ? storyOwner != null && IsQualifiedPart3Session()
            : enableWhiteWallDevelopmentTesting && IsPart3Ready();
    }

    [ContextMenu("Start White Wall")]
    private void StartWhiteWall()
    {
        if (storyControlled || !enableWhiteWallDevelopmentTesting || !IsPart3Ready() || moving)
            return;

        wall.gameObject.SetActive(true);
        wallRenderer.enabled = true;
        moving = !useMaximumXEndpoint ||
            wall.position.x < Mathf.Max(originalPosition.x, maximumXEndpoint);
    }

    [ContextMenu("Reset White Wall")]
    private void ResetWhiteWall()
    {
        if (IsPart3Ready())
            RestoreWall();
    }

    private void Update()
    {
        // Only the explicitly requested presentation needs ongoing checks.
        if (!initialized || !wall.gameObject.activeSelf)
            return;

        if (!IsCurrentSessionAllowed())
        {
            RestoreWall();
            return;
        }

        if (!moving || storyHeld)
            return;

        Vector3 position = wall.position;
        float speed = chapter11Takeover ? chapter11Speed : travelSpeed;
        if (encounterGuard)
        {
            float gap = (encounterPlayerCapsule != null ? encounterPlayerCapsule.bounds.min.x :
                player.position.x) - position.x;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(gap / encounterSlowdownDistance));
            speed = Mathf.Lerp(encounterNearSpeed, travelSpeed, blend);
        }
        position.x += Mathf.Max(0f, speed) * Time.deltaTime;
        if (encounterGuard)
        {
            // This is the authored church limit, not a proximity stop or reverse correction.
            position.x = Mathf.Max(wall.position.x, Mathf.Min(position.x, encounterLimitX));
        }
        if (approachingEncounter)
        {
            position.x = Mathf.Min(position.x, encounterTargetX);
            if (position.x >= encounterTargetX) storyHeld = true;
        }
        if (useMaximumXEndpoint)
        {
            float endpoint = Mathf.Max(originalPosition.x, maximumXEndpoint);
            position.x = Mathf.Min(position.x, endpoint);
            if (position.x >= endpoint)
                moving = false;
        }
        wall.position = position;
        if (!encounterGuard && !approachingEncounter && !crossingHandled &&
            player != null && position.x >= player.position.x + crossingOffset)
        {
            crossingHandled = true;
            EnterWhiteCamera();
            if (chapter11Takeover)
            {
                chapter11Takeover = false;
                chapter11Speed = 0f;
                storyHeld = true;
            }
        }
    }

    private void EnterWhiteCamera()
    {
        int layer = LayerMask.NameToLayer(PlayerVisualLayer);
        if (whiteCameraActive || !IsCurrentSessionAllowed())
            return;
        if (layer < 0 || gameplayCamera == null || player == null ||
            gameplayCamera.gameObject.scene != gameObject.scene ||
            player.gameObject.scene != gameObject.scene || !gameplayCamera.isActiveAndEnabled)
        {
            Debug.LogWarning("White camera presentation requires the dedicated visual layer and valid scene-local camera/player references. Wall movement is preserved.", this);
            return;
        }

        // One search at the crossing also includes the reaction sprite created in Awake.
        // Never change the player root, rig bones, colliders, Rigidbody or scripts.
        Renderer[] visuals = player.GetComponentsInChildren<Renderer>(true);
        if (visuals.Length == 0)
            return;
        foreach (Renderer visual in visuals)
        {
            if (visual.transform == player || visual.GetComponent<Collider>() != null ||
                visual.GetComponent<Rigidbody>() != null || visual.GetComponent<Collider2D>() != null ||
                visual.GetComponent<Rigidbody2D>() != null)
            {
                Debug.LogWarning("White camera presentation refused a player visual with physics components. No camera or layer override was applied.", this);
                return;
            }
        }

        isolatedVisuals = new GameObject[visuals.Length];
        originalVisualLayers = new int[visuals.Length];
        for (int i = 0; i < visuals.Length; i++)
        {
            isolatedVisuals[i] = visuals[i].gameObject;
            originalVisualLayers[i] = isolatedVisuals[i].layer;
        }
        overriddenCamera = gameplayCamera;
        originalClearFlags = overriddenCamera.clearFlags;
        originalBackgroundColor = overriddenCamera.backgroundColor;
        originalCullingMask = overriddenCamera.cullingMask;
        whiteCameraActive = true;

        for (int i = 0; i < isolatedVisuals.Length; i++)
            isolatedVisuals[i].layer = layer;
        overriddenCamera.clearFlags = CameraClearFlags.SolidColor;
        overriddenCamera.backgroundColor = Color.white;
        overriddenCamera.cullingMask = 1 << layer;
    }

    private void RestoreWhiteCamera()
    {
        if (!whiteCameraActive)
            return;

        if (overriddenCamera != null)
        {
            overriddenCamera.clearFlags = originalClearFlags;
            overriddenCamera.backgroundColor = originalBackgroundColor;
            overriddenCamera.cullingMask = originalCullingMask;
        }
        for (int i = 0; i < isolatedVisuals.Length; i++)
            if (isolatedVisuals[i] != null)
                isolatedVisuals[i].layer = originalVisualLayers[i];
        isolatedVisuals = null;
        originalVisualLayers = null;
        overriddenCamera = null;
        whiteCameraActive = false;
    }

    private void RestoreWall()
    {
        storyOwner = null;
        storyControlled = false;
        storyHeld = false;
        encounterGuard = false;
        encounterSlowdownDistance = 8f;
        encounterNearSpeed = 0.05f;
        encounterPlayerCapsule = null;
        approachingEncounter = false;
        chapter11Takeover = false;
        chapter11Speed = 0f;
        chapter11Staged = false;
        RestoreWhiteCamera();
        crossingHandled = false;
        moving = false;
        if (!initialized)
            return;

        wallRenderer.enabled = false;
        wall.gameObject.SetActive(false);
        wall.SetPositionAndRotation(originalPosition, originalRotation);
        wall.localScale = originalScale;
    }

    private void OnDisable()
    {
        RestoreWall();
    }

    private void OnDestroy()
    {
        RestoreWhiteCamera();
    }

    private void OnApplicationQuit()
    {
        RestoreWall();
    }

    private void OnValidate()
    {
        // Inspector edits must never reveal the prototype outside Play Mode.
        if (wallRenderer == null || wallRenderer.transform.parent != transform)
            return;
        if (!Application.isPlaying)
        {
            wallRenderer.enabled = false;
            wallRenderer.gameObject.SetActive(false);
        }
        else if (!enableWhiteWallDevelopmentTesting && !storyControlled)
        {
            RestoreWall();
        }
    }
}
