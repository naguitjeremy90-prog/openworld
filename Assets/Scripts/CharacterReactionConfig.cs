using UnityEngine;

/// <summary>Shared artwork and sizing for Miguel's character reaction visual.</summary>
[CreateAssetMenu(menuName = "ALAALA/Character Reaction Config", fileName = "CharacterReactionConfig")]
public sealed class CharacterReactionConfig : ScriptableObject
{
    [SerializeField] private Sprite reactionSprite;
    [SerializeField, Min(0.001f)] private float spriteBaseScale = 0.05f;

    public Sprite ReactionSprite => reactionSprite;
    public float SpriteBaseScale => Mathf.Max(0.001f, spriteBaseScale);
}
