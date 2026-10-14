using UnityEngine;

public enum AlaalaInteractionType
{
    Kausapin,
    Suriin,
    Pumasok,
    Lumabas,
    Umakyat,
    Bumaba,
    Matulog,
    Sumakay
}

[CreateAssetMenu(
    fileName = "AlaalaInteractionSFXDefaults",
    menuName = "ALAALA/Interaction SFX Defaults")]
public sealed class AlaalaInteractionSFXDefaults : ScriptableObject
{
    [Header("Default clips by interaction type")]
    [SerializeField] private AudioClip kausapin;
    [SerializeField] private AudioClip suriin;
    [SerializeField] private AudioClip pumasok;
    [SerializeField] private AudioClip lumabas;
    [SerializeField] private AudioClip umakyat;
    [SerializeField] private AudioClip bumaba;
    [SerializeField] private AudioClip matulog;
    [SerializeField] private AudioClip sumakay;

    public AudioClip GetClip(AlaalaInteractionType interactionType)
    {
        switch (interactionType)
        {
            case AlaalaInteractionType.Kausapin: return kausapin;
            case AlaalaInteractionType.Suriin: return suriin;
            case AlaalaInteractionType.Pumasok: return pumasok;
            case AlaalaInteractionType.Lumabas: return lumabas;
            case AlaalaInteractionType.Umakyat: return umakyat;
            case AlaalaInteractionType.Bumaba: return bumaba;
            case AlaalaInteractionType.Matulog: return matulog;
            case AlaalaInteractionType.Sumakay: return sumakay;
            default: return null;
        }
    }
}
