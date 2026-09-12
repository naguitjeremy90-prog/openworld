using UnityEngine;

/// <summary>Presentation data for a gameplay-system tutorial's initial callout.</summary>
public sealed class GameplaySystemTutorialDefinition
{
    public GameplaySystemId System { get; }
    public string Title { get; }
    public string Description { get; }
    public float PostRevealDelay { get; }
    public bool ShowInitialPointer { get; }

    public GameplaySystemTutorialDefinition(
        GameplaySystemId system,
        string title,
        string description,
        float postRevealDelay,
        bool showInitialPointer = true)
    {
        System = system;
        Title = title;
        Description = description;
        PostRevealDelay = Mathf.Max(0f, postRevealDelay);
        ShowInitialPointer = showInitialPointer;
    }
}
