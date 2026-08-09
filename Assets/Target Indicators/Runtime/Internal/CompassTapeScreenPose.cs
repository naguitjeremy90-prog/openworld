using UnityEngine;

namespace TargetIndicators
{
    class CompassTapeScreenPose
    {
        const float k_twoPi = Mathf.PI * 2f;

        readonly ScreenData _screenData;

        internal CompassTapeScreenPose(ScreenData screenData)
        {
            _screenData = screenData;
        }

        internal Pose GetScreenPoseForCompassTape(Vector3 worldSpacePosition, out bool isOutsideBoundary)
        {
            isOutsideBoundary = false;

            var cameraTransform = _screenData.Camera.transform;
            var cameraPos = cameraTransform.position;
            var cameraForward = cameraTransform.forward;

            // Calculate 2D direction (X, Z), naturally ignoring the Y axis
            var dx = worldSpacePosition.x - cameraPos.x;
            var dz = worldSpacePosition.z - cameraPos.z;

            // Fast distance check without square roots
            if (dx * dx + dz * dz < 0.0001f)
                return new Pose(new Vector3(0.5f, 0, 0), Quaternion.identity);

            var cameraAngle = Mathf.Atan2(cameraForward.x, cameraForward.z);
            var targetAngle = Mathf.Atan2(dx, dz);

            var deltaAngle = targetAngle - cameraAngle;

            // Wrap the delta angle to [-PI, PI] range
            if (deltaAngle > Mathf.PI)
                deltaAngle -= k_twoPi;
            else if (deltaAngle < -Mathf.PI)
                deltaAngle += k_twoPi;

            // Shift by PI (180 degrees) and scale down to the 0.0 - 1.0 range
            var x = (deltaAngle + Mathf.PI) / k_twoPi;

            return new Pose(new Vector3(x, 0f, 0f), Quaternion.identity);
        }
    }
}
