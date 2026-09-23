using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class ChurchNPCDepartureController : MonoBehaviour
{
    [Serializable]
    private sealed class DepartureEntry
    {
        public Behaviour movement;
        public Animator animator;
        [Min(0f)] public float delay;
    }

    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int StopState = Animator.StringToHash("Stop_com");

    [SerializeField] private DepartureEntry[] departures = Array.Empty<DepartureEntry>();

    private bool departureStarted;

    private void Awake()
    {
        // Awake runs before any scene Start, so the existing walker Start methods cannot run yet.
        foreach (DepartureEntry entry in departures)
        {
            if (entry == null || entry.movement == null || entry.animator == null)
            {
                Debug.LogWarning("A church NPC departure entry is missing a movement component or Animator.", this);
                continue;
            }

            entry.movement.enabled = false;
            entry.animator.SetBool(IsWalking, false);
        }
    }

    private void Start()
    {
        // The stop clips end in standing poses. Hold those poses while the walkers wait.
        foreach (DepartureEntry entry in departures)
        {
            if (entry == null || entry.movement == null || entry.animator == null)
                continue;

            entry.animator.Play(StopState, 0, 1f);
            entry.animator.Update(0f);
        }
    }

    public void BeginDeparture()
    {
        if (departureStarted)
            return;

        departureStarted = true;

        foreach (DepartureEntry entry in departures)
        {
            if (entry == null || entry.movement == null || entry.animator == null)
                continue;

            if (entry.delay <= 0f)
                entry.movement.enabled = true;
            else
                StartCoroutine(ReleaseAfterDelay(entry));
        }
    }

    private IEnumerator ReleaseAfterDelay(DepartureEntry entry)
    {
        yield return new WaitForSeconds(entry.delay);

        if (entry.movement != null)
            entry.movement.enabled = true;
    }
}
