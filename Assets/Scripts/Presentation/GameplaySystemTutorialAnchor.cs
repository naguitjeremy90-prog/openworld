using UnityEngine;

/// <summary>Registers a live HUD RectTransform as the pointer target for one gameplay system.</summary>
[DisallowMultipleComponent]
public sealed class GameplaySystemTutorialAnchor : MonoBehaviour
{
    [SerializeField] private GameplaySystemId system;
    [SerializeField] private RectTransform target;

    public GameplaySystemId System => system;
    public RectTransform Target => target != null ? target : transform as RectTransform;

    public static GameplaySystemTutorialAnchor AttachTo(
        GameObject root,
        GameplaySystemId system)
    {
        if (root == null)
            return null;

        GameplaySystemTutorialAnchor anchor =
            root.GetComponent<GameplaySystemTutorialAnchor>();
        if (anchor == null)
            anchor = root.AddComponent<GameplaySystemTutorialAnchor>();

        anchor.system = system;
        anchor.target = root.transform as RectTransform;
        if (anchor.isActiveAndEnabled)
            GameplaySystemTutorialManager.Instance.RegisterAnchor(anchor);
        return anchor;
    }

    private void OnEnable()
    {
        GameplaySystemTutorialManager.Instance.RegisterAnchor(this);
    }

    private void OnDisable()
    {
        if (GameplaySystemTutorialManager.HasInstance)
            GameplaySystemTutorialManager.Instance.UnregisterAnchor(this);
    }
}
