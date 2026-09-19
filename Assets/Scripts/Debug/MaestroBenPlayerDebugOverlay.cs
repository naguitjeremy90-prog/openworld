using UnityEngine;
using UnityEngine.Playables;
using Supercyan.FreeSample;

/// <summary>
/// Temporary, scene-local diagnostics for the Maestro Ben player handoff.
/// This component only reads state and draws an OnGUI panel; it never changes gameplay state.
/// </summary>
public sealed class MaestroBenPlayerDebugOverlay : MonoBehaviour
{
    [SerializeField] private GameObject playerObject;
    [SerializeField] private PlayableDirector maestroBenDirector;
    [SerializeField] private bool displayPanel = true;

    private SimpleSampleCharacterControl controller;
    private Rigidbody body;
    private Animator animator;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (!playerObject)
            playerObject = GameObject.Find("FInalChar1");

        if (playerObject)
        {
            controller = playerObject.GetComponent<SimpleSampleCharacterControl>();
            body = playerObject.GetComponent<Rigidbody>();
            animator = playerObject.GetComponent<Animator>();
        }

        if (!maestroBenDirector)
        {
            var directorObject = GameObject.Find("MaestroBenTimeline");
            if (directorObject)
                maestroBenDirector = directorObject.GetComponent<PlayableDirector>();
        }

        if (!maestroBenDirector)
        {
            var directors = FindObjectsOfType<PlayableDirector>();
            for (var i = 0; i < directors.Length; i++)
            {
                if (directors[i].playableAsset && directors[i].playableAsset.name == "MaestroBenTimeline")
                {
                    maestroBenDirector = directors[i];
                    break;
                }
            }
        }
    }

    private void OnGUI()
    {
        if (!displayPanel)
            return;

        if (!controller || !body || !animator)
            ResolveReferences();

        var state = animator ? animator.GetCurrentAnimatorStateInfo(0) : default;
        var stateLabel = "n/a";
        if (animator)
        {
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            var clipName = clips.Length > 0 && clips[0].clip ? clips[0].clip.name : "unknown clip";
            stateLabel = clipName + " (fullPathHash=" + state.fullPathHash + ", shortNameHash=" + state.shortNameHash + ")";
        }
        var directorState = maestroBenDirector ? maestroBenDirector.state.ToString() : "None";
        var directorTime = maestroBenDirector ? maestroBenDirector.time.ToString("F3") : "n/a";
        var panelWidth = 390f;
        var panelHeight = 300f;
        var panel = new Rect(Screen.width - panelWidth - 16f, 16f, panelWidth, panelHeight);

        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;

        var lines =
            "MAESTRO BEN PLAYER DEBUG\\n" +
            "Story Sequence Active: " + StorySequenceCoordinator.IsStorySequenceActive + "\\n" +
            "Controller Enabled: " + (controller && controller.enabled) + "\\n" +
            "m_isGrounded: " + (controller ? controller.DebugIsGrounded.ToString() : "n/a") + "\\n" +
            "Animator Grounded: " + (animator ? animator.GetBool("Grounded").ToString() : "n/a") + "\\n" +
            "Animator State: " + stateLabel + "\\n" +
            "Animator Normalized Time: " + (animator ? state.normalizedTime.ToString("F3") : "n/a") + "\\n" +
            "Rigidbody Velocity: " + (body ? body.linearVelocity.ToString("F3") : "n/a") + "\\n" +
            "Rigidbody Position: " + (body ? body.position.ToString("F3") : "n/a") + "\\n" +
            "Rigidbody IsKinematic: " + (body ? body.isKinematic.ToString() : "n/a") + "\\n" +
            "Rigidbody UseGravity: " + (body ? body.useGravity.ToString() : "n/a") + "\\n" +
            "Animator Enabled: " + (animator && animator.enabled) + "\\n" +
            "Animator Speed: " + (animator ? animator.speed.ToString("F3") : "n/a") + "\\n" +
            "PlayableDirector State: " + directorState + "\\n" +
            "PlayableDirector Time: " + directorTime + "\\n" +
            "Ground Contacts: " + (controller ? controller.DebugGroundContactCount.ToString() : "n/a");

        GUI.Label(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f), lines);
    }
}
