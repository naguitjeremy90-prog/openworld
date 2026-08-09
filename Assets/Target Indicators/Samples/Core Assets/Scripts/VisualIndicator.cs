using System;
using UnityEngine;

namespace TargetIndicators.Samples
{
    /// <summary>
    /// Control the instance of a <c>padded</c>, <c>absolute</c>, and <c>unbounded</c> visual indicator.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class VisualIndicator : MonoBehaviour
    {
        [SerializeField, Tooltip("The core content of the visual indicator. All visual images and text should be parented" +
                                 "to this RectTransform.")]
        protected RectTransform _coreContent;

        [SerializeField, Tooltip("The content of the visual indicator that is rotated to point at the target. All visual" +
                                 "images and text should be parented to this RectTransform.")]
        protected RectTransform _rotationContent;

        [SerializeField, Tooltip("The condition for when the core content should be visible.")]
        protected IndicatorVisibility _coreContentVisibility = IndicatorVisibility.Always;

        [SerializeField, Tooltip("The condition for when the rotation content should be visible.")]
        protected IndicatorVisibility _rotationContentVisibility = IndicatorVisibility.OutsideBoundary;

        /// <summary>
        /// The scale of the canvas used to calculate the position to place the visual indicator.
        /// </summary>
        public float CanvasScale { get; set; } = 1;

        /// <summary>
        /// The ID that represents the target indicator this visual indicator is associated with.
        /// </summary>
        public TargetIndicatorId TargetIndicatorId { get; set; }

        /// <summary>
        /// Get and set the condition for when the core content should be visible.
        /// </summary>
        public IndicatorVisibility CoreContentVisibility
        {
            get => _coreContentVisibility;
            set => _coreContentVisibility = value;
        }

        /// <summary>
        /// Get and set the condition for when the rotation content should be visible.
        /// </summary>
        public IndicatorVisibility RotationContentVisibility
        {
            get => _rotationContentVisibility;
            set => _rotationContentVisibility = value;
        }

        /// <summary>
        /// The name of the GameObject that represents the rotation content that is searched for on <see cref="Reset"/>.
        /// </summary>
        protected string _rotationPivotDefaultName = "RotationContent";

        /// <summary>
        /// The <see cref="RectTransform"/> of this GameObject.
        /// </summary>
        protected RectTransform _rectTransform;

        /// <summary>
        /// The GameObject of the core content. This is used to cache GameObject lookups from the core content
        /// <see cref="RectTransform"/>.
        /// </summary>
        protected GameObject _contentGO;

        /// <summary>
        /// The GameObject of the rotation content. This is used to cache GameObject lookups from the rotation content
        /// <see cref="RectTransform"/>.
        /// </summary>
        protected GameObject _rotationContentGO;

        protected virtual void Reset()
        {
            _rectTransform = GetComponent<RectTransform>();

            for (var i = 0; i < transform.childCount; i += 1)
            {
                var child = transform.GetChild(i);
                if (child.name == _rotationPivotDefaultName)
                    _rotationContent = child.GetComponent<RectTransform>();
            }
        }

        protected virtual void Awake()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            if (_rotationContent == null)
                Debug.LogException(new NullReferenceException($"{nameof(_rotationContent)} is null"), this);

            _contentGO = _coreContent.gameObject;
            _rotationContentGO = _rotationContent.gameObject;

            SetAnchorsAndPivot();
        }

        /// <summary>
        /// Sets the min anchor and max anchor of this GameObject's RectTransform to (0, 0) and sets the pivot to (0.5f, 0.5f).
        /// </summary>
        protected virtual void SetAnchorsAndPivot()
        {
            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.zero;
            _rectTransform.pivot = Vector2.one * 0.5f;
        }

        /// <summary>
        /// Updates the visual indicator with the data from a <see cref="TargetIndicator"/>.
        /// </summary>
        /// <param name="targetIndicator">The target indicator data to apply to the visual indicator.</param>
        public virtual void UpdateVisualIndicator(TargetIndicator targetIndicator)
        {
            UpdateVisualIndicator(targetIndicator.ScreenPose, targetIndicator.IsOutsideBoundary);
        }

        /// <summary>
        /// Sets this GameObject active.
        /// </summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Sets this GameObject inactive.
        /// </summary>
        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Sets the pose and visibility of the core content and rotation content.
        /// </summary>
        /// <param name="screenPose">The screen pose to apply to the visual indicator.</param>
        /// <param name="isOutsideBoundary">The state of the screen pose if it is outside the boundary.</param>
        public virtual void UpdateVisualIndicator(Pose screenPose, bool isOutsideBoundary)
        {
            _rectTransform.anchoredPosition = screenPose.position / CanvasScale;
            _rotationContent.rotation = screenPose.rotation;

            switch (_coreContentVisibility)
            {
                case IndicatorVisibility.Never:
                    _contentGO.SetActive(false);
                    break;
                case IndicatorVisibility.Always:
                    _contentGO.SetActive(true);
                    break;
                case IndicatorVisibility.OutsideBoundary:
                    _contentGO.SetActive(isOutsideBoundary);
                    break;
                case IndicatorVisibility.InsideBoundary:
                    _contentGO.SetActive(!isOutsideBoundary);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            switch (_rotationContentVisibility)
            {
                case IndicatorVisibility.Never:
                    _rotationContentGO.SetActive(false);
                    break;
                case IndicatorVisibility.Always:
                    _rotationContentGO.SetActive(true);
                    break;
                case IndicatorVisibility.OutsideBoundary:
                    _rotationContentGO.SetActive(isOutsideBoundary);
                    break;
                case IndicatorVisibility.InsideBoundary:
                    _rotationContentGO.SetActive(!isOutsideBoundary);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
