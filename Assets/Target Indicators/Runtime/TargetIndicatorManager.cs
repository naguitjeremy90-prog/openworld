using System;
using System.Collections.Generic;
using UnityEngine;

namespace TargetIndicators
{
    /// <summary>
    /// Tracks targets and transforms their world space positions into screen space coordinates with options to clamp to
    /// the screen edges, absolute size, or a compass tape. Useful for displaying directions to targets and other
    /// positions in the environment.
    /// </summary>
    [HelpURL("https://jakemanfre.github.io/target-indicators.github.io/manual/user_guide/target-indicator-manager.html")]
    public class TargetIndicatorManager : MonoBehaviour
    {
        enum State
        {
            Added,
            Updated,
        }

        struct TargetData
        {
            public TargetIndicatorId Id;
            public Transform Target;
            public State State;
        }

        const int k_maxTargets = 100;

        [Header("Scene References")]
        [SerializeField, Tooltip("The camera used to calculate screen point coordinates. Defaults to the camera tagged " +
                                 "\"MainCamera\" and falls back to any camera in the scene.")]
        Camera _camera;

        [Header("Settings")]
        [SerializeField, Tooltip("The type of boundary that target indicators will clamp to.")]
        BoundaryType _boundaryType;

        [Header("Configuration")]
        [SerializeField, Tooltip("The shape of the boundary that target indicators will clamp to. Only Padded and Absolute" +
                                 "boundary types use this setting.")]
        BoundaryShape _boundaryShape;

        [Tooltip("The distance in pixels from the top edge of the screen to clamp to for padded boundaries.")]
        [SerializeField, Min(0)]
        float _topPadding;

        [Tooltip("The distance in pixels from the bottom edge of the screen to clamp to for padded boundaries.")]
        [SerializeField, Min(0)]
        float _bottomPadding;

        [Tooltip("The distance in pixels from the left edge of the screen to clamp to for padded boundaries.")]
        [SerializeField, Min(0)]
        float _leftPadding;

        [Tooltip("The distance in pixels from the right edge of the screen to clamp to for padded boundaries.")]
        [SerializeField, Min(0)]
        float _rightPadding;

        [Tooltip("Absolute width of the boundary. This value is ignored if boundary type is not set to Absolute or WorldSpace.")]
        [SerializeField, Min(0)]
        float _width = 300;

        [Tooltip("Absolute height of the boundary. This value is ignored if boundary type is not set to Absolute or WorldSpace.")]
        [SerializeField, Min(0)]
        float _height = 300f;

        /// <summary>
        /// Delegate that passes a <see cref="ReadOnlySpan{T}"/> of target indicators that were added or updated.
        /// </summary>
        public delegate void TargetIndicatorsUpdatedDelegate(ReadOnlySpan<TargetIndicator> span);

        /// <summary>
        /// Delegate that passes a <see cref="ReadOnlySpan{T}"/> of target indicators that were removed.
        /// </summary>
        public delegate void TargetIndicatorsRemovedDelegate(ReadOnlySpan<TargetIndicatorId> span);

        /// <summary>
        /// Invoked when target indicators have been added.
        /// </summary>
        public event TargetIndicatorsUpdatedDelegate TargetIndicatorsAdded;

        /// <summary>
        /// Invoked when target indicators have been updated.
        /// </summary>
        public event TargetIndicatorsUpdatedDelegate TargetIndicatorsUpdated;

        /// <summary>
        /// Invoked when target indicators have been removed.
        /// </summary>
        public event TargetIndicatorsRemovedDelegate TargetIndicatorsRemoved;

        /// <summary>
        /// The scene camera that is used to calculate the screen pose of tracked targets.
        /// If this is null or destroyed, the manager will safely output default pose data for all targets.
        /// </summary>
        public Camera Camera
        {
            get => _camera;
            set
            {
                if (value == null)
                    Debug.LogWarning(
                        "[TargetIndicatorManager] Camera explicitly set to null. Targets will return default poses" +
                        " until a new camera is assigned.", this);

                _camera = value;
            }
        }

        /// <summary>
        /// The definition of the currently configured rectangle if <see cref="BoundaryShape"/> is set to
        /// <see cref="BoundaryShape.Rectangle"/>.
        /// </summary>
        public Rect Rectangle
        {
            get
            {
                return _boundaryType switch
                {
                    BoundaryType.Padded => new Rect
                    {
                        min = new Vector2(_leftPadding, _bottomPadding),
                        max = new Vector2(Screen.width - _rightPadding, Screen.height - _topPadding),
                    },
                    BoundaryType.Absolute => new Rect
                    {
                        min = new Vector2(Screen.width * 0.5f - _width * 0.5f,
                            Screen.height * 0.5f - _height * 0.5f),
                        max = new Vector2(Screen.width * 0.5f + _width * 0.5f,
                            Screen.height * 0.5f + _height * 0.5f),
                    },
                    _ => default
                };
            }
        }

        /// <summary>
        /// The definition of the currently configured ellipse if <see cref="BoundaryShape"/> is set to
        /// <see cref="BoundaryShape.Ellipse"/>.
        /// </summary>
        public Ellipse Ellipse
        {
            get
            {
                return _boundaryType switch
                {
                    BoundaryType.Padded => new Ellipse(
                        EllipseScreenPose.GetPaddedEllipseCenter(_leftPadding, _rightPadding, _topPadding, _bottomPadding),
                        (Screen.width - _leftPadding - _rightPadding) * 0.5f,
                        (Screen.height - _topPadding - _bottomPadding) * 0.5f),
                    BoundaryType.Absolute => new Ellipse(
                        EllipseScreenPose.GetAbsoluteEllipseCenter(),
                        _width * 0.5f,
                        _height * 0.5f),
                    _ => default
                };
            }
        }

        /// <summary>
        /// The type of boundary to clamp target indicators to.
        /// </summary>
        public BoundaryType BoundaryType
        {
            get => _boundaryType;
            set
            {
                var isValid = Enum.IsDefined(typeof(BoundaryType), value);
                if (!isValid)
                {
                    Debug.LogError($"Boundary type with integer value {(int)value} is not valid.");
                    return;
                }

                _boundaryType = value;
            }
        }

        /// <summary>
        /// The shape of the boundary to clamp target indicators to.
        /// </summary>
        public BoundaryShape BoundaryShape
        {
            get => _boundaryShape;
            set
            {
                var isValid = Enum.IsDefined(typeof(BoundaryShape), value);
                if (!isValid)
                {
                    Debug.LogError($"Boundary shape with integer value {(int)value} is not valid.");
                    return;
                }

                _boundaryShape = value;
            }
        }

        /// <summary>
        /// The distance in pixels from the left edge of the screen for <c>Padded</c> boundary type.
        /// </summary>
        public float LeftPadding
        {
            get => _leftPadding;
            set => _leftPadding = value;
        }

        /// <summary>
        /// The distance in pixels from the right edge of the screen for <c>Padded</c> boundary type.
        /// </summary>
        public float RightPadding
        {
            get => _rightPadding;
            set => _rightPadding = value;
        }

        /// <summary>
        /// The distance in pixels from the top edge of the screen for <c>Padded</c> boundary type.
        /// </summary>
        public float TopPadding
        {
            get => _topPadding;
            set => _topPadding = value;
        }

        /// <summary>
        /// The distance in pixels from the bottom edge of the screen for <c>Padded</c> boundary type.
        /// </summary>
        public float BottomPadding
        {
            get => _bottomPadding;
            set => _bottomPadding = value;
        }

        /// <summary>
        /// The width in pixels centered at the screen center for <c>Absolute</c> boundary type.
        /// </summary>
        public float Width
        {
            get => _width;
            set => _width = value;
        }

        /// <summary>
        /// The height in pixels centered at the screen center for <c>Absolute</c> boundary type.
        /// </summary>
        public float Height
        {
            get => _height;
            set => _height = value;
        }

        /// <summary>
        /// The max number of targets that can be tracked.
        /// </summary>
        public int MaxTargets => k_maxTargets;

        /// <summary>
        /// The current number of targets being tracked.
        /// </summary>
        public int TrackedTargetsCount => _targetDataIndexById.Count;

        readonly TargetData[] _allTargetData = new TargetData[k_maxTargets];
#if UNITY_6000_3_OR_NEWER
        readonly Dictionary<EntityId, TargetIndicatorId> _idByTargetEntityId = new(k_maxTargets);
#else
        readonly Dictionary<int, TargetIndicatorId> _idByTargetEntityId = new(k_maxTargets);
#endif
        readonly Dictionary<TargetIndicatorId, int> _targetDataIndexById = new(k_maxTargets);

        readonly TargetIndicator[] _addedTargetIndicators = new TargetIndicator[k_maxTargets];
        readonly TargetIndicator[] _updatedTargetIndicators = new TargetIndicator[k_maxTargets];
        readonly TargetIndicatorId[] _removedTargetIndicators = new TargetIndicatorId[k_maxTargets];

        int _addedSinceLastUpdate;
        int _updatedSinceLastUpdate;
        int _removedSinceLastUpdate;

        ScreenData _screenData;
        RectangleScreenPose _rectangleScreenPose;
        EllipseScreenPose _ellipseScreenPose;
        CompassTapeScreenPose _compassTapeScreenPose;

        void Reset()
        {
            TryFindCamera();
        }

        void Awake()
        {
            TryFindCamera();
            if (_camera == null)
            {
                Debug.LogWarning(
                    "[TargetIndicatorManager] Camera is missing. Returning default target data until a camera is " +
                    "assigned via the Camera property.", this);
            }

            _screenData = new(this);
            _rectangleScreenPose = new(_screenData);
            _ellipseScreenPose = new(_screenData);
            _compassTapeScreenPose = new(_screenData);
        }

        void TryFindCamera()
        {
            if (_camera == null)
                _camera = Camera.main;

            if (_camera == null)
                _camera = FindAnyObjectByType<Camera>();
        }

        /// <summary>
        /// Manually processes world-to-screen transformations for all tracked targets and dispatches
        /// the corresponding added, updated, and removed events.
        /// </summary>
        /// <remarks>
        /// Use this to synchronize indicator data and trigger events outside the standard
        /// <c>Update</c> loop (for example, if this component is disabled, or you require mid-frame updates).
        /// </remarks>
        public void GetChanges()
        {
            Update();
        }

        void Update()
        {
            var hasCamera = _camera != null;

            for (var i = _targetDataIndexById.Count - 1; i >= 0; i--)
            {
                var targetData = _allTargetData[i];
                switch (targetData.State)
                {
                    case State.Added:
                        if (targetData.Target == null)
                        {
                            RemoveTargetAtIndex(i);
                            break;
                        }

                        _addedTargetIndicators[_addedSinceLastUpdate++] = hasCamera
                            ? CreateTargetIndicator(targetData.Id, targetData.Target, true)
                            : new TargetIndicator(targetData.Id, targetData.Target, Pose.identity, false);

                        _allTargetData[i].State = State.Updated;
                        break;
                    case State.Updated:
                        if (targetData.Target == null)
                        {
                            RemoveTargetAtIndex(i);
                            break;
                        }

                        _updatedTargetIndicators[_updatedSinceLastUpdate++] = hasCamera
                            ? CreateTargetIndicator(targetData.Id, targetData.Target, true)
                            : new TargetIndicator(targetData.Id, targetData.Target, Pose.identity, false);
                        break;
                }
            }

            DispatchChangeEvents();
        }

        void SwapBackAndRemove(int index)
        {
            var dataToRemove = _allTargetData[index];
            var lastIndex = _targetDataIndexById.Count - 1;

            if (index != lastIndex)
            {
                _allTargetData[index] = _allTargetData[lastIndex];
                _targetDataIndexById[_allTargetData[index].Id] = index;
            }

            _targetDataIndexById.Remove(dataToRemove.Id);
            if (dataToRemove.Target != null)
            {
#if UNITY_6000_3_OR_NEWER
                _idByTargetEntityId.Remove(dataToRemove.Target.GetEntityId());
#else
                _idByTargetEntityId.Remove(dataToRemove.Target.GetInstanceID());
#endif
            }
        }

        void DispatchChangeEvents()
        {
            if (_addedSinceLastUpdate > 0)
            {
                var added = new ReadOnlySpan<TargetIndicator>(_addedTargetIndicators, 0, _addedSinceLastUpdate);
                _addedSinceLastUpdate = 0;
                TargetIndicatorsAdded?.Invoke(added);
            }

            if (_updatedSinceLastUpdate > 0)
            {
                var updated = new ReadOnlySpan<TargetIndicator>(_updatedTargetIndicators, 0, _updatedSinceLastUpdate);
                _updatedSinceLastUpdate = 0;
                TargetIndicatorsUpdated?.Invoke(updated);
            }

            if (_removedSinceLastUpdate > 0)
            {
                var removed = new ReadOnlySpan<TargetIndicatorId>(_removedTargetIndicators, 0, _removedSinceLastUpdate);
                _removedSinceLastUpdate = 0;
                TargetIndicatorsRemoved?.Invoke(removed);
            }
        }

        /// <summary>
        /// Attempts to add a new target to be tracked. This can only fail if the <see cref="TrackedTargetsCount"/>
        /// equals <see cref="MaxTargets"/>. If the <see cref="Camera"/> is null, the returned
        /// <paramref name="targetIndicator"/> will contain default pose data.
        /// </summary>
        /// <param name="target">The transform of the target to track.</param>
        /// <param name="targetIndicator">The created <see cref="TargetIndicator"/> for the current frame of the
        /// target's position.</param>
        /// <returns><c>true</c> if the target was added, otherwise <c>false</c>.</returns>
        public bool TryAddTarget(Transform target, out TargetIndicator targetIndicator)
        {
            targetIndicator = TargetIndicator.Default;

            if (target == null)
                return false;

            if (_targetDataIndexById.Count >= k_maxTargets)
                return false;

#if UNITY_6000_3_OR_NEWER
            var entityId = target.GetEntityId();
#else
            var entityId = target.GetInstanceID();
#endif
            if (_idByTargetEntityId.TryGetValue(entityId, out var existingId))
                return TryGetTargetIndicator(existingId, out targetIndicator);

            var targetData = new TargetData
            {
                Id = new TargetIndicatorId(Guid.NewGuid()),
                Target = target,
                State = State.Added,
            };

            _allTargetData[TrackedTargetsCount] = targetData;
            _targetDataIndexById.Add(targetData.Id, TrackedTargetsCount);
            _idByTargetEntityId.Add(entityId, targetData.Id);

            targetIndicator = CreateTargetIndicator(targetData.Id, targetData.Target, _camera != null);
            return true;
        }

        /// <summary>
        /// Attempts to get the <see cref="TargetIndicator"/> for the corresponding <c>TargetIndicatorId</c>.
        /// If the <see cref="Camera"/> is null, the returned <paramref name="targetIndicator"/> will contain default pose data.
        /// </summary>
        /// <param name="targetIndicatorId">The ID of the target indicator.</param>
        /// <param name="targetIndicator">The <see cref="TargetIndicator"/> associated with
        /// <paramref name="targetIndicatorId"/>.</param>
        /// <returns><c>true</c> if the <paramref name="targetIndicatorId"/> is valid and being tracked. Otherwise,
        /// <c>false</c>.</returns>
        public bool TryGetTargetIndicator(TargetIndicatorId targetIndicatorId, out TargetIndicator targetIndicator)
        {
            targetIndicator = TargetIndicator.Default;

            if (!_targetDataIndexById.TryGetValue(targetIndicatorId, out var index))
                return false;

            var targetData = _allTargetData[index];
            if (targetData.Target == null)
                return false;

            targetIndicator = CreateTargetIndicator(targetData.Id, targetData.Target, _camera != null);
            return true;
        }

        /// <summary>
        /// Attempts to remove a target indicator.
        /// </summary>
        /// <param name="targetIndicatorId">The ID of the target indicator to remove.</param>
        /// <returns><c>true</c> if the <paramref name="targetIndicatorId"/> is valid and being tracked.
        /// Otherwise <c>false</c>.</returns>
        public bool TryRemoveTarget(TargetIndicatorId targetIndicatorId)
        {
            if (!_targetDataIndexById.TryGetValue(targetIndicatorId, out var index))
                return false;

            RemoveTargetAtIndex(index);
            return true;
        }

        /// <summary>
        /// Removes all targets that are being tracked.
        /// </summary>
        public void RemoveAllTargets()
        {
            for (var i = _targetDataIndexById.Count - 1; i >= 0; i--)
            {
                RemoveTargetAtIndex(i);
            }
        }

        void RemoveTargetAtIndex(int index)
        {
            var targetData = _allTargetData[index];
            if (targetData.State == State.Updated)
                _removedTargetIndicators[_removedSinceLastUpdate++] = targetData.Id;

            SwapBackAndRemove(index);
        }


        /// <summary>
        /// Gets the screen pose of a world space position. If the <see cref="Camera"/> is null,
        /// this safely returns <see cref="Pose.identity"/> and sets <paramref name="isOutsideBoundary"/> to <c>false</c>.
        /// </summary>
        /// <param name="worldSpacePosition">The world space position to convert to the screen pose.</param>
        /// <param name="isOutsideBoundary"><c>true</c> if the screen pose of <paramref name="worldSpacePosition"/> is
        /// outside of the configured boundary. Otherwise, <c>false</c>.</param>
        /// <returns>The screen space pose of <paramref name="worldSpacePosition"/>.</returns>
        public Pose GetScreenPose(Vector3 worldSpacePosition, out bool isOutsideBoundary)
        {
            if (_camera == null)
            {
                isOutsideBoundary = false;
                return Pose.identity;
            }

            return GetScreenPoseCore(worldSpacePosition, out isOutsideBoundary);
        }

        Pose GetScreenPoseCore(Vector3 worldSpacePosition, out bool isOutsideBoundary)
        {
            return _boundaryType switch
            {
                BoundaryType.Padded => GetPaddedScreenPose(worldSpacePosition, out isOutsideBoundary),
                BoundaryType.Absolute => GetAbsoluteScreenPose(worldSpacePosition, out isOutsideBoundary),
                BoundaryType.CompassTape => _compassTapeScreenPose.GetScreenPoseForCompassTape(worldSpacePosition, out isOutsideBoundary),
                BoundaryType.Unbounded => GetUnboundedScreenPose(worldSpacePosition, out isOutsideBoundary),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        /// <summary>
        /// Checks if the screen space position is outside the configured boundary.
        /// If the <see cref="Camera"/> is null, this will safely return <c>false</c>.
        /// </summary>
        /// <param name="screenPoint">The screen point to test. Requires the point as a <c>Vector3</c> to
        /// allow for the inclusion of the depth from the camera in world space for faster calculations. If unknown, set
        /// <c>screenPoint.z</c> to 0.</param>
        /// <returns><c>true</c> if <paramref name="screenPoint"/> is outside the configured boundary.
        /// Otherwise, <c>false</c>.</returns>
        public bool IsOutsideBoundary(Vector3 screenPoint)
        {
            if (_camera == null)
                return false;

            if (_boundaryType is BoundaryType.Padded or BoundaryType.Absolute
                && screenPoint.z < 0)
                return true;

            return _boundaryType switch
            {
                BoundaryType.Padded => IsOutsidePaddedBoundary(screenPoint),
                BoundaryType.Absolute => IsOutsideAbsoluteBoundary(screenPoint),
                BoundaryType.CompassTape => false,
                BoundaryType.Unbounded => false,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        TargetIndicator CreateTargetIndicator(TargetIndicatorId id, Transform target, bool hasCamera)
        {
            if (!hasCamera)
                return new TargetIndicator(id, target, Pose.identity, false);

            var screenPose = GetScreenPoseCore(target.position, out var isOutsideBoundary);
            return new TargetIndicator(id, target, screenPose, isOutsideBoundary);
        }

        Pose GetPaddedScreenPose(Vector3 worldSpacePosition, out bool isOutsideBoundary)
        {
            return _boundaryShape switch
            {
                BoundaryShape.Rectangle => _rectangleScreenPose.GetPaddedScreenPose(worldSpacePosition,
                    out isOutsideBoundary),
                BoundaryShape.Ellipse => _ellipseScreenPose.GetPaddedScreenPose(worldSpacePosition,
                    out isOutsideBoundary),
                _ => throw new ArgumentOutOfRangeException(
                    $"Boundary shape with integer value {(int)_boundaryShape} is not supported.")
            };
        }

        Pose GetAbsoluteScreenPose(Vector3 worldSpacePosition, out bool isOutsideBoundary)
        {
            return _boundaryShape switch
            {
                BoundaryShape.Rectangle => _rectangleScreenPose.GetAbsoluteScreenPose(worldSpacePosition, out isOutsideBoundary),
                BoundaryShape.Ellipse => _ellipseScreenPose.GetAbsoluteScreenPose(worldSpacePosition, out isOutsideBoundary),
                _ => throw new ArgumentOutOfRangeException(
                    $"Boundary shape with integer value {(int)_boundaryShape} is not supported.")
            };
        }

        Pose GetUnboundedScreenPose(Vector3 worldSpacePosition, out bool isOutsideBoundary)
        {
            var screenPoint = _camera.WorldToScreenPoint(worldSpacePosition);

            if (screenPoint.z < 0)
                screenPoint.x = float.MaxValue;

            isOutsideBoundary = false;

            return new Pose(screenPoint, Quaternion.identity);
        }

        bool IsOutsidePaddedBoundary(Vector3 screenPoint)
        {
            return _boundaryShape switch
            {
                BoundaryShape.Rectangle => _rectangleScreenPose.IsOutsidePaddedBoundary(screenPoint),
                BoundaryShape.Ellipse => _ellipseScreenPose.IsOutsidePaddedBoundary(screenPoint),
                _ => throw new ArgumentOutOfRangeException(
                    $"Boundary shape with integer value {(int)_boundaryShape} is not supported.")
            };
        }

        bool IsOutsideAbsoluteBoundary(Vector3 screenPoint)
        {
            return _boundaryShape switch
            {
                BoundaryShape.Rectangle => _rectangleScreenPose.IsOutsideAbsoluteBoundary(screenPoint),
                BoundaryShape.Ellipse => _ellipseScreenPose.IsOutsideAbsoluteBoundary(screenPoint),
                _ => throw new ArgumentOutOfRangeException(
                    $"Boundary shape with integer value {(int)_boundaryShape} is not supported.")
            };
        }
    }
}
