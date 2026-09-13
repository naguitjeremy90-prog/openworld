using UnityEngine;
using System.Collections;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("NPC Animators")]
    [SerializeField] private Animator maleAnimator;
    [SerializeField] private Animator femaleAnimator;

    [Header("New Ball")]
    [SerializeField] private Rigidbody soccerBall;

    [Header("Ball Movement")]
    [SerializeField] private float passSpeed = 8f;
    [SerializeField] private float arrivalDistance = 0.1f;

    [Header("Ball Stop Position")]
    [SerializeField] private float ballStopDistance = 1.0f;

    [Header("Ball Rotation")]
    [SerializeField] private float ballSpinSpeed = 720f;

    [Header("Male Animation")]
    [SerializeField] private float maleKickFrame = 13f;
    [SerializeField] private float maleEndFrame = 48f;

    [Header("Female Animation")]
    [SerializeField] private float femaleKickFrame = 22f;
    [SerializeField] private float femaleEndFrame = 45f;

    [Header("Animator Parameter")]
    [SerializeField] private string kickingParameter = "IsKicking";

    private bool isRunning = false;
    private Coroutine ballCoroutine;

    private void Start()
    {
        if (maleAnimator == null ||
            femaleAnimator == null ||
            soccerBall == null)
        {
            Debug.LogError("Assign both Animators and the Soccer Ball.");
            return;
        }

        soccerBall.isKinematic = true;
        soccerBall.useGravity = false;

        maleAnimator.SetBool(kickingParameter, false);
        femaleAnimator.SetBool(kickingParameter, false);

        StartCoroutine(PassingRoutine());
    }

    private IEnumerator PassingRoutine()
    {
        if (isRunning)
            yield break;

        isRunning = true;

        yield return StartCoroutine(MalePass());

        while (true)
        {
            yield return StartCoroutine(FemalePass());
            yield return StartCoroutine(MalePass());
        }
    }

    // =========================================================
    // MALE PASS
    // =========================================================

    private IEnumerator MalePass()
    {
        Debug.Log("========== MALE TURN ==========");

        maleAnimator.SetBool(kickingParameter, true);

        yield return null;

        float animationLength =
            GetAnimationLength(maleAnimator);

        if (animationLength <= 0f)
        {
            Debug.LogError("Male kick animation length could not be found.");
            maleAnimator.SetBool(kickingParameter, false);
            yield break;
        }

        float kickTime =
            animationLength *
            (maleKickFrame / maleEndFrame);

        Debug.Log("Male Animation Length: " + animationLength);
        Debug.Log("Male Kick Time: " + kickTime);

        yield return new WaitForSeconds(kickTime);

        Debug.Log("⚽ MALE KICKS!");

        // MALE → FEMALE = REVERSE SPIN
        StartBallMovement(femaleAnimator.transform, true);

        float remainingTime =
            animationLength - kickTime;

        if (remainingTime > 0f)
            yield return new WaitForSeconds(remainingTime);

        maleAnimator.SetBool(kickingParameter, false);

        Debug.Log("MALE → IDLE");

        yield return StartCoroutine(
            WaitForBallToReach(femaleAnimator.transform)
        );

        Debug.Log("⚽ BALL WITH FEMALE");
    }

    // =========================================================
    // FEMALE PASS
    // =========================================================

    private IEnumerator FemalePass()
    {
        Debug.Log("========== FEMALE TURN ==========");

        float ballTravelTime =
            GetBallTravelTime(femaleAnimator.transform);

        float femaleAnimationLength =
            GetAnimationLength(femaleAnimator);

        if (femaleAnimationLength <= 0f)
        {
            Debug.LogError("Female kick animation length could not be found.");
            yield break;
        }

        float femaleKickTime =
            femaleAnimationLength *
            (femaleKickFrame / femaleEndFrame);

        float femaleStartDelay =
            ballTravelTime - femaleKickTime;

        if (femaleStartDelay < 0f)
            femaleStartDelay = 0f;

        Debug.Log(
            "Female kick starts in: " +
            femaleStartDelay +
            " seconds"
        );

        yield return new WaitForSeconds(
            femaleStartDelay
        );

        femaleAnimator.SetBool(
            kickingParameter,
            true
        );

        Debug.Log("FEMALE → START KICK");

        yield return null;

        yield return new WaitForSeconds(
            femaleKickTime
        );

        Debug.Log("⚽ FEMALE KICKS!");

        // FEMALE → MALE = NORMAL SPIN
        StartBallMovement(
            maleAnimator.transform,
            false
        );

        float femaleRemainingTime =
            femaleAnimationLength -
            femaleKickTime;

        if (femaleRemainingTime > 0f)
            yield return new WaitForSeconds(
                femaleRemainingTime
            );

        femaleAnimator.SetBool(
            kickingParameter,
            false
        );

        Debug.Log("FEMALE → IDLE");

        yield return StartCoroutine(
            WaitForBallToReach(
                maleAnimator.transform
            )
        );

        Debug.Log("⚽ BALL WITH MALE");
    }

    // =========================================================
    // GET BALL TRAVEL TIME
    // =========================================================

    private float GetBallTravelTime(Transform target)
    {
        if (target == null ||
            soccerBall == null ||
            passSpeed <= 0f)
        {
            return 0f;
        }

        Vector3 targetPosition =
            GetBallTargetPosition(target);

        float distance =
            Vector3.Distance(
                soccerBall.position,
                targetPosition
            );

        return distance / passSpeed;
    }

    // =========================================================
    // GET BALL TARGET POSITION
    // =========================================================

    private Vector3 GetBallTargetPosition(Transform target)
    {
        Vector3 direction =
            soccerBall.position -
            target.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();
        }
        else
        {
            direction = target.forward;
            direction.y = 0f;
            direction.Normalize();
        }

        Vector3 targetPosition =
            target.position +
            direction * ballStopDistance;

        targetPosition.y =
            soccerBall.position.y;

        return targetPosition;
    }

    // =========================================================
    // GET ACTUAL ANIMATION CLIP LENGTH
    // =========================================================

    private float GetAnimationLength(Animator animator)
    {
        RuntimeAnimatorController controller =
            animator.runtimeAnimatorController;

        if (controller == null)
            return 0f;

        AnimationClip[] clips =
            controller.animationClips;

        foreach (AnimationClip clip in clips)
        {
            if (clip == null)
                continue;

            if (clip.name == "mixamo.com")
            {
                return clip.length;
            }
        }

        foreach (AnimationClip clip in clips)
        {
            if (clip != null &&
                clip.name != "Idle")
            {
                return clip.length;
            }
        }

        return 0f;
    }

    // =========================================================
    // START BALL MOVEMENT
    // =========================================================

    private void StartBallMovement(
        Transform target,
        bool reverseSpin)
    {
        if (ballCoroutine != null)
        {
            StopCoroutine(ballCoroutine);
        }

        ballCoroutine =
            StartCoroutine(
                MoveBallTo(
                    target,
                    reverseSpin
                )
            );
    }

    // =========================================================
    // BALL MOVEMENT
    // =========================================================

    private IEnumerator MoveBallTo(
        Transform target,
        bool reverseSpin)
    {
        if (target == null)
            yield break;

        while (true)
        {
            Vector3 targetPosition =
                GetBallTargetPosition(target);

            Vector3 newPosition =
                Vector3.MoveTowards(
                    soccerBall.position,
                    targetPosition,
                    passSpeed * Time.deltaTime
                );

            soccerBall.MovePosition(
                newPosition
            );

            // BALL ONLY: spin while moving.
            float spinDirection =
                reverseSpin ? -1f : 1f;

            soccerBall.MoveRotation(
                soccerBall.rotation *
                Quaternion.Euler(
                    0f,
                    ballSpinSpeed *
                    spinDirection *
                    Time.deltaTime,
                    0f
                )
            );

            float distance =
                Vector3.Distance(
                    soccerBall.position,
                    targetPosition
                );

            if (distance <= arrivalDistance)
            {
                soccerBall.MovePosition(
                    targetPosition
                );

                Debug.Log(
                    "⚽ BALL ARRIVED OUTSIDE NPC"
                );

                ballCoroutine = null;

                yield break;
            }

            yield return null;
        }
    }

    // =========================================================
    // WAIT FOR BALL
    // =========================================================

    private IEnumerator WaitForBallToReach(
        Transform target)
    {
        if (target == null)
            yield break;

        while (true)
        {
            Vector3 targetPosition =
                GetBallTargetPosition(target);

            float distance =
                Vector3.Distance(
                    soccerBall.position,
                    targetPosition
                );

            if (distance <= arrivalDistance)
                yield break;

            yield return null;
        }
    }
}
