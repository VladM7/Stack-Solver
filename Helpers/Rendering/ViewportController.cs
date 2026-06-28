using System.Windows.Media.Media3D;

namespace Stack_Solver.Helpers.Rendering
{
    public class ViewportController
    {
        private readonly PerspectiveCamera _camera;

        // Zoom moves the camera along its orbit radius toward/away from the target. It must update the
        // same spherical state (Distance) that rotation reads, otherwise a later rotate snaps the camera
        // back to a stale position.
        public void Zoom(double delta)
        {
            double zoomFactor = 1.0 + delta * -0.001;
            Distance = Math.Clamp(Distance * zoomFactor, _minDistance, _maxDistance);
            UpdateCameraPosition();
        }

        private Point _lastMousePos;

        public Point3D Target { get; set; } = new Point3D(0, 0, 0);
        private double Distance;
        private double Azimuth;
        private double Elevation;
        private double _minDistance = 1.0;
        private double _maxDistance = double.PositiveInfinity;

        public ViewportController(PerspectiveCamera camera, Point3D target)
        {
            _camera = camera;
            Target = target;

            Vector3D toTarget = camera.Position - Target;
            Distance = toTarget.Length;
            SetDistanceLimits(Distance);

            Azimuth = Math.Atan2(toTarget.X, toTarget.Z);

            double sinElev = toTarget.Y / Distance;
            sinElev = Math.Max(-1.0, Math.Min(1.0, sinElev));
            Elevation = Math.Asin(sinElev);
        }

        private void SetDistanceLimits(double framingDistance)
        {
            _minDistance = Math.Max(0.1, framingDistance * 0.1);
            _maxDistance = framingDistance * 5.0;
        }

        public void BeginPan(Point start)
        {
            _lastMousePos = start;
        }

        public void Pan(Point current)
        {
            double dx = current.X - _lastMousePos.X;
            double dy = current.Y - _lastMousePos.Y;
            _lastMousePos = current;

            double rotationSpeed = 0.01;
            Azimuth -= dx * rotationSpeed;
            Elevation += dy * rotationSpeed;


            Elevation = Math.Max(-Math.PI / 2 + 0.01, Math.Min(Math.PI / 2 - 0.01, Elevation));

            UpdateCameraPosition();
        }

        public void ResetView(Point3D target, double distance)
        {
            Target = target;
            Distance = distance;
            SetDistanceLimits(distance);
            Azimuth = Math.PI / 4;
            Elevation = Math.PI / 6;
            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            double x = Target.X + Distance * Math.Cos(Elevation) * Math.Sin(Azimuth);
            double y = Target.Y + Distance * Math.Sin(Elevation);
            double z = Target.Z + Distance * Math.Cos(Elevation) * Math.Cos(Azimuth);

            _camera.Position = new Point3D(x, y, z);
            _camera.LookDirection = Target - _camera.Position;
            _camera.UpDirection = new Vector3D(0, 1, 0);
        }
    }
}
