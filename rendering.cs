using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace rendering
{
    class Rendering
    {
        public Vector3D CalculateTriangleNormal(Point3D p0, Point3D p1, Point3D p2)
        {
            Vector3D v0 = new Vector3D(
                p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3D v1 = new Vector3D(
                p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
            return Vector3D.CrossProduct(v0, v1);
        }

        public Model3D geometryCreation(Point3D p0, Point3D p1, Point3D p2, Brush brush)
        {
            GeometryModel3D myGeometryModel = new GeometryModel3D();
            ModelVisual3D myModelVisual3D = new ModelVisual3D();

            MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();

            Point3DCollection myPositionCollection = new Point3DCollection();

            myPositionCollection.Add(p0);
            myPositionCollection.Add(p1);
            myPositionCollection.Add(p2);

            myMeshGeometry3D.Positions = myPositionCollection;

            Int32Collection myTriangleIndicesCollection = new Int32Collection();

            myTriangleIndicesCollection.Add(0);
            myTriangleIndicesCollection.Add(1);
            myTriangleIndicesCollection.Add(2);
            myMeshGeometry3D.TriangleIndices = myTriangleIndicesCollection;

            Vector3D Normal = CalculateTriangleNormal(p0, p1, p2);
            myMeshGeometry3D.Normals.Add(Normal);
            myMeshGeometry3D.Normals.Add(Normal);
            myMeshGeometry3D.Normals.Add(Normal);

            myGeometryModel.Geometry = myMeshGeometry3D;

            LinearGradientBrush myHorizontalGradient = new LinearGradientBrush();
            myHorizontalGradient.StartPoint = new Point(0, 0.5);
            myHorizontalGradient.EndPoint = new Point(1, 0.5);
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Yellow, 0.0));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Red, 0.25));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Blue, 0.75));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.LimeGreen, 1.0));

            //DiffuseMaterial myMaterial = new DiffuseMaterial(myHorizontalGradient);

            DiffuseMaterial myMaterial = new DiffuseMaterial(brush);
            myGeometryModel.Material = myMaterial;

            return myGeometryModel;
        }
    }
}
