using UnityEngine;

namespace TargetIndicators
{
    /// <summary>
    /// The contextual data that defines a tracked target in screen space.
    /// </summary>
    public readonly struct TargetIndicator
    {
        /// <summary>
        /// Gets a default-initialized `TargetIndicator`. This may be different from the zero-initialized version
        /// (for example, the pose is `Pose.identity` instead of zero initialized).
        /// </summary>
        public static TargetIndicator Default => new(default, null, Pose.identity, false);

        /// <summary>
        /// The ID that defines the target indicator.
        /// </summary>
        public TargetIndicatorId Id { get; }

        /// <summary>
        /// The target that is being tracked by the target indicator.
        /// </summary>
        public Transform Target { get; }

        /// <summary>
        /// The screen space coordinates and rotation of the target.
        /// `ScreenPose.position.x` corresponds to the horizontal
        /// axis coordinate in pixels where 0 is the left end of the axis and <see cref="Screen.width"/> is the right end of the axis.
        /// `ScreenPose.position.y` corresponds to the vertical axis coordinate in pixels where 0 is the bottom end of the axis and
        /// <see cref="Screen.height"/> is the top end of the axis.
        /// `ScreenPose.position.z` corresponds to the depth from the camera in world space in meters.
        /// `ScreenPose.rotation` corresponds to the rotation in screen space relative to the right vector to rotate
        /// the indicator to point at the target.
        /// </summary>
        public Pose ScreenPose { get; }

        /// <summary>
        /// `True` if the target's screen pose is outside the <see cref="TargetIndicatorManager"/> configured boundary.
        /// Otherwise, `false`.
        /// </summary>
        public bool IsOutsideBoundary { get; }

        /// <summary>
        /// Constructs a `TargetIndicator`.
        /// </summary>
        /// <param name="id">The ID that defines the indicator.</param>
        /// <param name="target">The target that is being tracked by the target indicator.</param>
        /// <param name="screenPose">The screen space coordinates and rotation of the target.</param>
        /// <param name="isOutsideBoundary">The current state of the target's screen pose if it's outside the boundary.</param>
        public TargetIndicator(TargetIndicatorId id, Transform target, Pose screenPose, bool isOutsideBoundary)
        {
            Id = id;
            Target = target;
            ScreenPose = screenPose;
            IsOutsideBoundary = isOutsideBoundary;
        }

        /// <summary>
        /// Constructs a `TargetIndicator`.
        /// </summary>
        /// <param name="id">The ID that defines the indicator.</param>
        /// <param name="target">The target that is being tracked by the target indicator.</param>
        /// <param name="screenPoint">The screen space coordinates of the target.</param>
        /// <param name="rotation">The screen space rotation of the target.</param>
        /// <param name="isOutsideBoundary">The current state of the target's screen pose if it's outside the boundary.</param>
        public TargetIndicator(TargetIndicatorId id, Transform target, Vector3 screenPoint, Quaternion rotation, bool isOutsideBoundary)
        {
            Id = id;
            Target = target;
            ScreenPose = new Pose(screenPoint, rotation);
            IsOutsideBoundary = isOutsideBoundary;
        }
    }
}
