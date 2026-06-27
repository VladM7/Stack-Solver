using System.Windows;
using System.Windows.Media.Media3D;

namespace Stack_Solver.Helpers.Rendering
{
    public static class ViewportProjection
    {
        /// <summary>
        /// Projects a world-space point to Viewport3D pixel coordinates.
        /// Returns null when the point is behind the camera or the viewport has no size.
        /// WPF's PerspectiveCamera.FieldOfView is the horizontal FOV in degrees.
        /// </summary>
        public static Point? ProjectToScreen(Point3D worldPoint, PerspectiveCamera camera, double vpWidth, double vpHeight)
        {
            if (vpWidth <= 0 || vpHeight <= 0) return null;

            Vector3D lookDir = camera.LookDirection;
            lookDir.Normalize();
            // zAxis points away from the scene (right-handed view space)
            Vector3D zAxis = -lookDir;
            Vector3D xAxis = Vector3D.CrossProduct(camera.UpDirection, zAxis);
            xAxis.Normalize();
            Vector3D yAxis = Vector3D.CrossProduct(zAxis, xAxis);

            Vector3D toPoint = worldPoint - camera.Position;
            double vx = Vector3D.DotProduct(toPoint, xAxis);
            double vy = Vector3D.DotProduct(toPoint, yAxis);
            double vz = Vector3D.DotProduct(toPoint, zAxis); // negative for in-front points

            if (vz >= -0.001) return null;

            double tanHalfHFov = Math.Tan(camera.FieldOfView * Math.PI / 360.0);
            double ndcX = vx / (-vz * tanHalfHFov);
            double ndcY = vy * (vpWidth / vpHeight) / (-vz * tanHalfHFov);

            double screenX = (ndcX + 1.0) / 2.0 * vpWidth;
            double screenY = (1.0 - ndcY) / 2.0 * vpHeight;

            return new Point(screenX, screenY);
        }
    }
}
