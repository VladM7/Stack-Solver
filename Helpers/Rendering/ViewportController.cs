using System.Windows.Media.Media3D;

namespace Stack_Solver.Helpers.Rendering
{
    public class ViewportController
    {
        private readonly PerspectiveCamera _camera;

        public void Zoom(double delta)
        {
            double zoomFactor = 1.0 + delta * -0.001;
            _camera.Position = new Point3D(
                _camera.Position.X * zoomFactor,
                _camera.Position.Y * zoomFactor,
                _camera.Position.Z * zoomFactor
            );

            Vector3D toTarget = Target - _camera.Position;
            Distance = toTarget.Length;
        }

        private Point _lastMousePos;

        public Point3D Target { get; set; } = new Point3D(0, 0, 0);
        private double Distance;
        private double Azimuth;
        private double Elevation;

        public ViewportController(PerspectiveCamera camera, Point3D target)
        {
            _camera = camera;
            Target = target;

            Vector3D toTarget = camera.Position - Target;
            Distance = toTarget.Length;

            Azimuth = Math.Atan2(toTarget.X, toTarget.Z);

            double sinElev = toTarget.Y / Distance;
            sinElev = Math.Max(-1.0, Math.Min(1.0, sinElev));
            Elevation = Math.Asin(sinElev);
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
