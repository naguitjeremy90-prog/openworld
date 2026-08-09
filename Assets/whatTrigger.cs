using UnityEngine;
using System.Collections;

public class SurpriseTrigger : MonoBehaviour
{
    public GameObject exclamationMark;
    public MonoBehaviour playerMovement; // your movement script
    public Rigidbody playerRb; // if you're using physics
    public Animator playerAnimator; // optional

    private bool triggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            // ❗ ADD THIS CHECK
            if (!GameFlags.canTeleport)
                return;

            triggered = true;
            StartCoroutine(ShowSurprise());
        }
    }

    IEnumerator ShowSurprise()
    {
        // 🔴 Stop movement
        playerMovement.enabled = false;

        // 🔴 Stop physics
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
            playerRb.isKinematic = true;
        }

        // 🔴 FORCE IDLE ANIMATION (PUT IT HERE 👇)
        if (playerAnimator != null)
        {
            playerAnimator.SetFloat("X", 0);
            playerAnimator.SetFloat("Y", 0);
        }

        yield return new WaitForSeconds(0.2f);

        // ❗ Show "!"
        exclamationMark.SetActive(true);

        yield return new WaitForSeconds(1.2f);

        exclamationMark.SetActive(false);

        // 🟢 Resume everything
        if (playerRb != null)
        {
            playerRb.isKinematic = false;
        }

        playerMovement.enabled = true;
    }
}