using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Stack_Solver
{
    class ViewClass
    {
        private Point3D cameraPosition1 = new Point3D(0, 40, 0);
        private Vector3D vector3DLookDirection1 = new Vector3D(0, -1, 0);
        private Vector3D vector3DUpDirection1 = new Vector3D(0, 0, -1);

        private Point3D cameraPosition2 = new Point3D(11, 10, 9);
        private Vector3D vector3DLookDirection2 = new Vector3D(-12, -11, -10);
        private Vector3D vector3DUpDirection2 = new Vector3D(0, 1, 0);

        private Point3D cameraPosition3 = new Point3D(40, 0, 0);
        private Vector3D vector3DLookDirection3 = new Vector3D(-1, 0, 0);
        private Vector3D vector3DUpDirection3 = new Vector3D(0, 1, 0);

        private Point3D cameraPosition4 = new Point3D(0, 0, 40);
        private Vector3D vector3DLookDirection4 = new Vector3D(0, 0, -1);
        private Vector3D vector3DUpDirection4 = new Vector3D(0, 1, 0);

        rendering.Rendering render = new rendering.Rendering();

        public void TopDownView(OrthographicCamera orthographicCamera, PerspectiveCamera threedCamera)
        {
            threedCamera.Position = cameraPosition1;
            threedCamera.LookDirection = vector3DLookDirection1;
            threedCamera.UpDirection = vector3DUpDirection1;

            if (orthographicCamera == null)
                return;
            orthographicCamera.Position = new Point3D(0, 60, 0);
            orthographicCamera.LookDirection = vector3DLookDirection1;
            orthographicCamera.UpDirection = vector3DUpDirection1;
        }

        public void FreeView(OrthographicCamera orthographicCamera, PerspectiveCamera threedCamera)
        {
            threedCamera.Position = cameraPosition2;
            threedCamera.LookDirection = vector3DLookDirection2;
            threedCamera.UpDirection = vector3DUpDirection2;

            if (orthographicCamera == null)
                return;
            orthographicCamera.Position = new Point3D(51, 50, 49);
            orthographicCamera.LookDirection = vector3DLookDirection2;
            orthographicCamera.UpDirection = vector3DUpDirection2;
        }

        public void SideViewX(OrthographicCamera orthographicCamera, PerspectiveCamera threedCamera)
        {
            threedCamera.Position = cameraPosition3;
            threedCamera.LookDirection = vector3DLookDirection3;
            threedCamera.UpDirection = vector3DUpDirection3;

            if (orthographicCamera == null)
                return;
            orthographicCamera.Position = new Point3D(60, 0, 0);
            orthographicCamera.LookDirection = vector3DLookDirection3;
            orthographicCamera.UpDirection = vector3DUpDirection3;
        }

        public void SideViewY(OrthographicCamera orthographicCamera, PerspectiveCamera threedCamera)
        {
            threedCamera.Position = cameraPosition4;
            threedCamera.LookDirection = vector3DLookDirection4;
            threedCamera.UpDirection = vector3DUpDirection4;

            if (orthographicCamera == null)
                return;
            orthographicCamera.Position = new Point3D(0, 0, 60);
            orthographicCamera.LookDirection = vector3DLookDirection4;
            orthographicCamera.UpDirection = vector3DUpDirection4;
        }

        public void ViewAxes(PalletClass pallet, ref Viewport3D MainViewPort, double eff_height)
        {
            var lineX1 = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                        new Point3D(-500, render.zPositionOffset + pallet.Height, 1 + pallet.Width/2),
                        new Point3D( 500, render.zPositionOffset + pallet.Height, 1 + pallet.Width/2),
                        new Point3D(-500, render.zPositionOffset + pallet.Height, pallet.Width/2),
                       new Point3D( 500, render.zPositionOffset + pallet.Height, pallet.Width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineX2 = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                        new Point3D(-500, render.zPositionOffset + pallet.Height, 1 - pallet.Width/2),
                        new Point3D( 500, render.zPositionOffset + pallet.Height, 1 - pallet.Width/2),
                        new Point3D(-500, render.zPositionOffset + pallet.Height, -pallet.Width/2),
                       new Point3D( 500, render.zPositionOffset + pallet.Height, -pallet.Width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineX1Above = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, render.zPositionOffset + eff_height, 1 + pallet.Width/2),
                       new Point3D( 500, render.zPositionOffset + eff_height, 1 + pallet.Width/2),
                       new Point3D(-500, render.zPositionOffset + eff_height, pallet.Width/2),
                       new Point3D( 500, render.zPositionOffset + eff_height, pallet.Width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineX2Above = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, render.zPositionOffset + eff_height, 1 - pallet.Width/2),
                       new Point3D( 500, render.zPositionOffset + eff_height, 1 - pallet.Width/2),
                       new Point3D(-500, render.zPositionOffset + eff_height, -pallet.Width/2),
                       new Point3D( 500, render.zPositionOffset + eff_height, -pallet.Width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };

            RotateTransform3D rotateTransformX = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 90));
            lineX1.Transform = rotateTransformX;
            lineX2.Transform = rotateTransformX;
            lineX1Above.Transform = rotateTransformX;
            lineX2Above.Transform = rotateTransformX;

            var lineY1 = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                        new Point3D(-500, render.zPositionOffset + pallet.Height, 1 + pallet.Length/2),
                        new Point3D( 500, render.zPositionOffset + pallet.Height, 1 + pallet.Length/2),
                        new Point3D(-500, render.zPositionOffset + pallet.Height, pallet.Length/2),
                       new Point3D( 500, render.zPositionOffset + pallet.Height, pallet.Length/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineY2 = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                        new Point3D(-500, render.zPositionOffset + pallet.Height, 1 - pallet.Length/2),
                        new Point3D( 500, render.zPositionOffset + pallet.Height, 1 - pallet.Length/2),
                        new Point3D(-500, render.zPositionOffset + pallet.Height, -pallet.Length / 2),
                       new Point3D( 500, render.zPositionOffset + pallet.Height, -pallet.Length / 2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineY1Above = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, render.zPositionOffset + eff_height, 1 + pallet.Length/2),
                       new Point3D( 500, render.zPositionOffset + eff_height, 1 + pallet.Length/2),
                       new Point3D(-500, render.zPositionOffset + eff_height, pallet.Length/2),
                       new Point3D( 500, render.zPositionOffset + eff_height, pallet.Length/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };
            var lineY2Above = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                       new Point3D(-500, render.zPositionOffset + eff_height, 1 - pallet.Length/2),
                       new Point3D( 500, render.zPositionOffset + eff_height, 1 - pallet.Length/2),
                       new Point3D(-500, render.zPositionOffset + eff_height, -pallet.Length / 2),
                       new Point3D( 500, render.zPositionOffset + eff_height, -pallet.Length / 2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Gray)
            };

            var lineXMeasurement = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                        new Point3D(-pallet.Length/2, render.zPositionOffset + pallet.Height, 50 + 2 + pallet.Width/2),
                        new Point3D( pallet.Length/2, render.zPositionOffset + pallet.Height, 50 + 2 + pallet.Width/2),
                        new Point3D(-pallet.Length/2, render.zPositionOffset + pallet.Height, 50 + pallet.Width/2),
                        new Point3D( pallet.Length/2, render.zPositionOffset + pallet.Height, 50 + pallet.Width/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Blue)
            };
            lineXMeasurement.Transform = rotateTransformX;
            var lineYMeasurement = new GeometryModel3D()
            {
                Geometry = new MeshGeometry3D()
                {
                    Positions = {
                        new Point3D(-pallet.Width/2, render.zPositionOffset + pallet.Height, 50 + 2 + pallet.Length/2),
                        new Point3D( pallet.Width/2, render.zPositionOffset + pallet.Height, 50 + 2 + pallet.Length/2),
                        new Point3D(-pallet.Width/2, render.zPositionOffset + pallet.Height, 50 + pallet.Length/2),
                        new Point3D( pallet.Width/2, render.zPositionOffset + pallet.Height, 50 + pallet.Length/2)
                   },
                    TriangleIndices = { 0, 1, 2, 2, 1, 3 }
                },
                Material = new DiffuseMaterial(Brushes.Blue)
            };

            var cube = new GeometryModel3D();
            var mat = new DiffuseMaterial();
            var vb = new VisualBrush();
            var st = new StackPanel();
            st.Children.Add(new System.Windows.Controls.TextBlock(new Run("Length: " + pallet.Length + " cm")));
            vb.Visual = st;
            mat.Brush = vb;
            cube.Material = mat;
            cube.Geometry = new MeshGeometry3D()
            {
                Positions = {
                    new Point3D(10, 10, 10),
                    new Point3D(-10, 10, 10),
                    new Point3D(-10, -10, 10),
                    new Point3D(10, -10, 10),
                },
                TriangleIndices = {
                    0, 1, 2,
                    0, 2, 3
                },
                TextureCoordinates = {
                    new System.Windows.Point(1, 1),
                    new System.Windows.Point(0, 1),
                    new System.Windows.Point(0, 0),
                    new System.Windows.Point(1, 0)
                }
            };
            cube.Transform = new ScaleTransform3D(50, 50, 50);

            foreach (object o in MainViewPort.Children)
                if (o is ModelVisual3D)
                {
                    ModelVisual3D? mdv = o as ModelVisual3D;
                    Model3DGroup? mdg = mdv.Content as Model3DGroup;
                    if (mdv == null || mdg == null)
                        continue;
                    mdg.Children.Add(lineX1);
                    mdg.Children.Add(lineX2);
                    mdg.Children.Add(lineX1Above);
                    mdg.Children.Add(lineX2Above);
                    mdg.Children.Add(lineY1);
                    mdg.Children.Add(lineY2);
                    mdg.Children.Add(lineY1Above);
                    mdg.Children.Add(lineY2Above);
                    mdg.Children.Add(lineXMeasurement);
                    mdg.Children.Add(lineYMeasurement);
                    mdg.Children.Add(cube);
                    return;
                }
        }
    }
}
