using UnityEngine;
using UnityEngine.EventSystems;

public sealed class InventoryPickup : MonoBehaviour
{
    // TEMPORARY PICKUP DIAGNOSTICS: remove the marked logs/helpers after diagnosis.
    private const string DiagnosticPrefix = "[InventoryPickup Diagnostic]";

    [SerializeField] private InventoryItemData item;
    [SerializeField, Min(0f)] private float pickupDistance = 3f;

    private static int processedClickFrame = -1;

    private Transform playerTransform;

    private void Awake()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0) || processedClickFrame == Time.frameCount)
            return;

        processedClickFrame = Time.frameCount;
        ProcessLeftClick();
    }

    private static void ProcessLeftClick()
    {
        bool pointerOverUI = EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject();

        Debug.Log(
            $"{DiagnosticPrefix} Left click at screen position {Input.mousePosition}. " +
            $"EventSystem: {(EventSystem.current != null ? GetHierarchyPath(EventSystem.current.transform) : "<none>")}. " +
            $"Pointer over UI: {pointerOverUI}.");

        if (pointerOverUI)
        {
            Debug.Log($"{DiagnosticPrefix} REJECTED: EventSystem reports that the pointer is over UI.");
            return;
        }

        Camera gameplayCamera = Camera.main;
        if (gameplayCamera == null)
        {
            Debug.LogWarning($"{DiagnosticPrefix} REJECTED: Camera.main is null.");
            return;
        }

        Debug.Log(
            $"{DiagnosticPrefix} Camera.main resolved to '{GetHierarchyPath(gameplayCamera.transform)}' " +
            $"at {gameplayCamera.transform.position}. Active and enabled: " +
            $"{gameplayCamera.isActiveAndEnabled}.");

        Ray ray = gameplayCamera.ScreenPointToRay(Input.mousePosition);
        ProcessRay(ray);
    }

    private static void ProcessRay(Ray ray)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.Log(
                $"{DiagnosticPrefix} REJECTED: Ray hit no collider. " +
                $"Origin: {ray.origin}, direction: {ray.direction}.");
            return;
        }

        InventoryPickup pickup = hit.collider.GetComponentInParent<InventoryPickup>();
        string colliderPath = GetHierarchyPath(hit.collider.transform);
        string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);

        Debug.Log(
            $"{DiagnosticPrefix} FIRST HIT: '{colliderPath}', " +
            $"layer {hit.collider.gameObject.layer} ('{layerName}'), " +
            $"camera-to-hit distance {hit.distance:F3}. " +
            $"GetComponentInParent<InventoryPickup> found: {pickup != null}.");

        if (pickup == null)
        {
            Debug.Log(
                $"{DiagnosticPrefix} REJECTED: The first collider hit has no InventoryPickup " +
                "on itself or a parent. It occludes colliders behind it.");
            return;
        }

        pickup.TryPickup(hit.collider);
    }

    private void TryPickup(Collider clickedCollider)
    {
        if (item == null)
        {
            Debug.LogWarning($"{DiagnosticPrefix} REJECTED: No inventory item is assigned.", this);
            return;
        }

        if (playerTransform == null)
            FindPlayer();

        if (playerTransform == null)
        {
            Debug.LogWarning(
                $"{DiagnosticPrefix} REJECTED: No active GameObject tagged 'Player' was found.",
                this);
            return;
        }

        Vector3 closestPoint = clickedCollider.bounds.ClosestPoint(playerTransform.position);
        float playerDistance = Vector3.Distance(playerTransform.position, closestPoint);

        Debug.Log(
            $"{DiagnosticPrefix} Pickup '{GetHierarchyPath(transform)}' was found. " +
            $"Player-to-pickup distance: {playerDistance:F3}; allowed distance: {pickupDistance:F3}.",
            this);

        if (playerDistance > pickupDistance)
        {
            Debug.Log(
                $"{DiagnosticPrefix} REJECTED: Player-to-pickup distance exceeds Pickup Distance.",
                this);
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning(
                $"{DiagnosticPrefix} REJECTED: No active InventoryManager exists in the scene.",
                this);
            return;
        }

        if (InventoryManager.Instance.AddItem(item))
        {
            Debug.Log(
                $"{DiagnosticPrefix} ACCEPTED: AddItem succeeded. Deactivating " +
                $"'{GetHierarchyPath(transform)}'.",
                this);
            gameObject.SetActive(false);
        }
        else
        {
            Debug.Log(
                $"{DiagnosticPrefix} REJECTED: InventoryManager.AddItem returned false " +
                "(the item was not added, commonly because it is already owned).",
                this);
        }
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerTransform = player != null ? player.transform : null;
    }

    private void OnValidate()
    {
        pickupDistance = Mathf.Max(0f, pickupDistance);
    }

    // TEMPORARY PICKUP DIAGNOSTICS helper.
    private static string GetHierarchyPath(Transform target)
    {
        string path = target.name;

        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }
}
