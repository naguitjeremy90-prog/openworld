using System.Collections;
using UnityEngine;
using DialogueEditor;

public class NPCConversationTrigger : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private NPCConversation myConversation;
    [SerializeField] private GameObject talkText;

    [Header("Camera Focus (Optional)")]
    [SerializeField] private CameraFocusManager focusManager;
    [SerializeField] private CameraFocusPoint focusPoint;

    [Header("Face Player (Optional)")]
    [SerializeField] private bool facePlayerWhenTalking = true;
    [SerializeField] private Transform npcTransform;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private bool returnToOriginalDirection = true;

    private bool playerNear = false;
    private bool isTalking = false;

    private Quaternion originalRotation;
    private Coroutine turnCoroutine;

    private void Start()
    {
        if (npcTransform != null)
            originalRotation = npcTransform.rotation;
    }

    private void OnEnable()
    {
        ConversationManager.OnConversationEnded += OnConversationEnded;
    }

    private void OnDisable()
    {
        ConversationManager.OnConversationEnded -= OnConversationEnded;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;

            if (!isTalking && talkText != null)
                talkText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;

            if (talkText != null)
                talkText.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerNear && !isTalking && Input.GetKeyDown(KeyCode.E))
        {
            StartConversation();
        }
    }

    private void StartConversation()
    {
        isTalking = true;

        if (talkText != null)
            talkText.SetActive(false);

        // Turn NPC toward Peter
        if (facePlayerWhenTalking &&
            npcTransform != null &&
            playerTransform != null)
        {
            if (turnCoroutine != null)
                StopCoroutine(turnCoroutine);

            turnCoroutine = StartCoroutine(TurnTowardPlayer());
        }

        // Optional camera focus
        if (focusManager != null && focusPoint != null)
            focusManager.FocusOn(focusPoint);

        ConversationManager.Instance.StartConversation(myConversation);
    }

    private IEnumerator TurnTowardPlayer()
    {
        Vector3 direction = playerTransform.position - npcTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            yield break;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        while (Quaternion.Angle(
            npcTransform.rotation,
            targetRotation) > 1f)
        {
            npcTransform.rotation = Quaternion.Slerp(
                npcTransform.rotation,
                targetRotation,
                Time.deltaTime * turnSpeed);

            yield return null;
        }

        npcTransform.rotation = targetRotation;
    }

    private IEnumerator ReturnToOriginalRotation()
    {
        while (Quaternion.Angle(
            npcTransform.rotation,
            originalRotation) > 1f)
        {
            npcTransform.rotation = Quaternion.Slerp(
                npcTransform.rotation,
                originalRotation,
                Time.deltaTime * turnSpeed);

            yield return null;
        }

        npcTransform.rotation = originalRotation;
    }

    private void OnConversationEnded()
    {
        if (!isTalking)
            return;

        isTalking = false;

        // Return camera
        if (focusManager != null && focusPoint != null)
            focusManager.ReturnToNormal(focusPoint);

        // Turn NPC back
        if (facePlayerWhenTalking &&
            returnToOriginalDirection &&
            npcTransform != null)
        {
            if (turnCoroutine != null)
                StopCoroutine(turnCoroutine);

            turnCoroutine = StartCoroutine(
                ReturnToOriginalRotation());
        }

        if (playerNear && talkText != null)
            talkText.SetActive(true);
    }
}
