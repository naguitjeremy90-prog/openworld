using UnityEngine;

[DisallowMultipleComponent]
public sealed class ClarityDocumentSource : MonoBehaviour
{
    [Header("Document")]
    [SerializeField] private string documentId;
    [SerializeField] private Sprite documentSprite;
    [Tooltip("Optional obscured/unreadable sprite rendered above the clear document and revealed by Clarity.")]
    [SerializeField] private Sprite obscuredDocumentSprite;
    [Tooltip("Optional prefab containing ClarityDocumentRegion children laid out over this document.")]
    [SerializeField] private GameObject regionLayoutPrefab;

    [Header("Viewer")]
    [SerializeField] private ClarityDocumentViewer documentViewer;

    public string DocumentId => documentId;
    public Sprite DocumentSprite => documentSprite;
    public Sprite ObscuredDocumentSprite => obscuredDocumentSprite;
    public GameObject RegionLayoutPrefab => regionLayoutPrefab;

    public void Inspect()
    {
        if (documentViewer == null)
            documentViewer = FindAnyObjectByType<ClarityDocumentViewer>();

        if (documentViewer == null)
        {
            Debug.LogWarning(
                "ClarityDocumentSource could not find a ClarityDocumentViewer.",
                this);
            return;
        }

        documentViewer.OpenDocument(this);
    }
}
