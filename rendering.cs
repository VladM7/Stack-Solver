using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using Point = System.Windows.Point;
using System.Reflection;

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

        private void genTriangle(double posinit_x, double posinit_y, double posinit_z, double length, double width, double height, Model3DGroup triangle, Brush brush)
        {
            Point3D p0 = new Point3D(posinit_x + width, posinit_y + height, posinit_z + length);
            Point3D p1 = new Point3D(posinit_x, posinit_y + height, posinit_z + length);
            Point3D p2 = new Point3D(posinit_x, posinit_y, posinit_z + length);
            Point3D p3 = new Point3D(posinit_x + width, posinit_y, posinit_z + length);
            Point3D p4 = new Point3D(posinit_x + width, posinit_y + height, posinit_z);
            Point3D p5 = new Point3D(posinit_x, posinit_y + height, posinit_z);
            Point3D p6 = new Point3D(posinit_x, posinit_y, posinit_z);
            Point3D p7 = new Point3D(posinit_x + width, posinit_y, posinit_z);

            //front
            triangle.Children.Add(geometryCreation(p0, p1, p2, brush));
            triangle.Children.Add(geometryCreation(p0, p2, p3, brush));

            //back
            triangle.Children.Add(geometryCreation(p4, p7, p6, brush));
            triangle.Children.Add(geometryCreation(p4, p6, p5, brush));

            //right
            triangle.Children.Add(geometryCreation(p4, p0, p3, brush));
            triangle.Children.Add(geometryCreation(p4, p3, p7, brush));

            //left
            triangle.Children.Add(geometryCreation(p1, p5, p6, brush));
            triangle.Children.Add(geometryCreation(p1, p6, p2, brush));

            //top
            triangle.Children.Add(geometryCreation(p1, p0, p4, brush));
            triangle.Children.Add(geometryCreation(p1, p4, p5, brush));

            //bottom
            triangle.Children.Add(geometryCreation(p2, p6, p7, brush));
            triangle.Children.Add(geometryCreation(p2, p7, p3, brush));
        }

        public void generateStack(double size_x, double size_y, double size_z, int nr_x, int nr_y, int nr_z, double offset_x, double offset_y, double offset_z, ref Model3DGroup triangle, Brush brush)
        {
            //MessageBox.Show(offset_x + "x, " + offset_y + "y");
            for (int i = 0; i < nr_x; i++)
                for (int j = 0; j < nr_y; j++)
                    for (int k = 0; k < nr_z; k++)
                        genTriangle(offset_y + j * (size_y + 0.5), offset_z + k * (size_z + 0.5), offset_x + i * (size_x + 0.5), size_x, size_y, size_z, triangle, brush);

            //genTriangle(0, 0, 0, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);

            //genTriangle(0, 0, 1.02, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);//y
            //genTriangle(1.02, 0, 0, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);//x
            //genTriangle(0, 1.02, 0, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);//z
            //genTriangle(0, 2.04, 0, Convert.ToDouble(lengthBox.Text), Convert.ToDouble(widthBox.Text), Convert.ToDouble(heightBox.Text), triangle);
        }
    }
}
