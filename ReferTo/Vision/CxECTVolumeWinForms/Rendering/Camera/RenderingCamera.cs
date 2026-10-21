using System;
using System.Drawing;
using CxECTVolumeCore.Utils;
using OpenTK;

namespace CxECTVolumeWinForms.Rendering
{
    // 相机状态独立于 GPU 资源，渐进式替换纹理时可完整保留用户视角。
    /// <summary>只负责轨迹球旋转、缩放和按体积物理尺寸自适应相机。</summary>
    internal sealed class RenderingCamera
    {
        private const float FieldOfViewDegrees = 45.0f;
        private Quaternion _rotation;
        private float _distance;
        private float _fitDistance;
        private float _boundingRadius;

        public RenderingCamera()
        {
            Reset(Vector3.One, 1.0f);
        }

        public Matrix4 RotationMatrix => Matrix4.CreateFromQuaternion(_rotation);

        public Vector3 WorldPosition => new Vector3(0.0f, 0.0f, _distance);

        public Matrix4 ViewMatrix => Matrix4.LookAt(WorldPosition, Vector3.Zero, Vector3.UnitY);

        public Matrix4 CreateProjection(float aspect)
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(FieldOfViewDegrees),
                Math.Max(0.01f, aspect),
                0.02f,
                20.0f);
        }

        public void Reset(Vector3 volumeScale, float aspect)
        {
            _rotation = CreateDefaultRotation();
            _boundingRadius = 0.5f * volumeScale.Length;
            float verticalHalfAngle = MathHelper.DegreesToRadians(FieldOfViewDegrees * 0.5f);
            float horizontalHalfAngle = (float)Math.Atan(Math.Tan(verticalHalfAngle) * Math.Max(0.01f, aspect));
            float limitingHalfAngle = Math.Min(verticalHalfAngle, horizontalHalfAngle);
            _fitDistance = _boundingRadius / Math.Max(0.05f, (float)Math.Sin(limitingHalfAngle)) * 1.08f;
            _distance = FrameworkCompat.Clamp(_fitDistance, 0.7f, 6.0f);
        }

        public void Zoom(int wheelDelta)
        {
            float minimum = Math.Max(_boundingRadius * 1.05f, _fitDistance * 0.38f);
            float maximum = Math.Max(6.0f, _fitDistance * 4.0f);
            _distance = FrameworkCompat.Clamp(_distance - wheelDelta * 0.0015f, minimum, maximum);
        }

        public void Rotate(Point previous, Point current, Size viewport)
        {
            Vector3 from = MapToTrackball(previous, viewport);
            Vector3 to = MapToTrackball(current, viewport);
            Vector3 axis = Vector3.Cross(from, to);
            float axisLength = axis.Length;
            if (axisLength <= 0.0001f)
            {
                return;
            }

            axis /= axisLength;
            float cosine = FrameworkCompat.Clamp(Vector3.Dot(from, to), -1.0f, 1.0f);
            Quaternion delta = Quaternion.FromAxisAngle(axis, (float)Math.Acos(cosine));
            _rotation = delta * _rotation;
            _rotation.Normalize();
        }

        private static Quaternion CreateDefaultRotation()
        {
            Quaternion pitch = Quaternion.FromAxisAngle(Vector3.UnitX, MathHelper.DegreesToRadians(24.0f));
            Quaternion yaw = Quaternion.FromAxisAngle(Vector3.UnitY, MathHelper.DegreesToRadians(-35.0f));
            return pitch * yaw;
        }

        private static Vector3 MapToTrackball(Point mouse, Size viewport)
        {
            float radius = Math.Max(1.0f, Math.Min(viewport.Width, viewport.Height) * 0.5f);
            float x = (mouse.X - viewport.Width * 0.5f) / radius;
            float y = (viewport.Height * 0.5f - mouse.Y) / radius;
            float lengthSquared = x * x + y * y;
            if (lengthSquared >= 1.0f)
            {
                float inverseLength = 1.0f / (float)Math.Sqrt(lengthSquared);
                return new Vector3(x * inverseLength, y * inverseLength, 0.0f);
            }

            return new Vector3(x, y, (float)Math.Sqrt(1.0f - lengthSquared));
        }
    }
}
