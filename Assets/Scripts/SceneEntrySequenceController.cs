using System;
using System.Collections;
using Supercyan.FreeSample;
using UnityEngine;

/// <summary>
/// Runs a small, reusable scene-entry sequence: reaction, automatic walk,
/// facing, and an optional local self-dialogue.
/// </summary>
public sealed class SceneEntrySequenceController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;
    [SerializeField] private SimpleSampleCharacterControl playerMovement;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private CharacterReactionController playerReaction;

    [Header("Walk Staging")]
    [SerializeField] private Transform movementTarget;
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [SerializeField, Min(0f)] private float stoppingDistance = 0.05f;
    [SerializeField] private Transform facingTarget;

    [Header("Conversation")]
    [SerializeField] private SelfDialogueTrigger conversationSource;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;

    private bool hasPlayed;

    private void Start()
    {
        if (playOnStart)
            StartCoroutine(PlaySequence());
    }

    public void PlaySequenceExternally()
    {
        if (!hasPlayed)
            StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        if (hasPlayed)
            yield break;

        hasPlayed = true;

        bool movementWasEnabled = playerMovement != null && playerMovement.enabled;

        if (playerMovement != null)
            playerMovement.enabled = false;

        SetMoveAnimation(0f);

        if (playerReaction != null)
            yield return playerReaction.PlayReaction();

        yield return MovePlayerToTarget();
        FaceTarget();
        yield return PlayConversation();

        SetMoveAnimation(0f);

        if (playerMovement != null)
            playerMovement.enabled = movementWasEnabled;
    }

    private IEnumerator MovePlayerToTarget()
    {
        if (player == null || movementTarget == null)
            yield break;

        Rigidbody rigidbody = player.GetComponent<Rigidbody>();
        float speed = Mathf.Max(0f, moveSpeed);

        while (HorizontalDistance(player.position, movementTarget.position) > stoppingDistance)
        {
            Vector3 targetPosition = movementTarget.position;
            targetPosition.y = player.position.y;

            Vector3 nextPosition = Vector3.MoveTowards(
                player.position,
                targetPosition,
                speed * Time.deltaTime);

            if (rigidbody != null)
                rigidbody.MovePosition(nextPosition);
            else
                player.position = nextPosition;

            SetMoveAnimation(speed > 0f ? 1f : 0f);
            yield return null;
        }

        if (rigidbody != null)
        {
            Vector3 velocity = rigidbody.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rigidbody.linearVelocity = velocity;
        }

        SetMoveAnimation(0f);
    }

    private void FaceTarget()
    {
        if (player == null || facingTarget == null)
            return;

        Vector3 direction = facingTarget.position - player.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
            player.rotation = Quaternion.LookRotation(direction);
    }

    private IEnumerator PlayConversation()
    {
        if (conversationSource == null)
            yield break;

        bool conversationFinished = false;
        Action onConversationFinished = () => conversationFinished = true;

        conversationSource.ConversationFinished += onConversationFinished;

        try
        {
            conversationSource.StartSelfDialogue();

            while (!conversationFinished)
                yield return null;
        }
        finally
        {
            conversationSource.ConversationFinished -= onConversationFinished;
        }
    }

    private void SetMoveAnimation(float value)
    {
        if (playerAnimator != null)
            playerAnimator.SetFloat("MoveSpeed", value);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
