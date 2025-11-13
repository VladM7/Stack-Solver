using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Stack_Solver.Helpers.Rendering
{
    public static class GeometryCreator
    {
        public static Model3DGroup CreateBox(Point3D origin, double width, double height, double depth, Brush brush)
        {
            var group = new Model3DGroup();
            var material = new DiffuseMaterial(brush);

            Point3D A = new(origin.X, origin.Y, origin.Z);
            Point3D B = new(origin.X + width, origin.Y, origin.Z);
            Point3D C = new(origin.X + width, origin.Y + height, origin.Z);
            Point3D D = new(origin.X, origin.Y + height, origin.Z);
            Point3D E = new(origin.X, origin.Y, origin.Z + depth);
            Point3D F = new(origin.X + width, origin.Y, origin.Z + depth);
            Point3D G = new(origin.X + width, origin.Y + height, origin.Z + depth);
            Point3D H = new(origin.X, origin.Y + height, origin.Z + depth);

            void AddFace(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
            {
                var mesh = new MeshGeometry3D
                {
                    Positions = [p0, p1, p2, p3],
                    TriangleIndices = [0, 1, 2, 2, 3, 0]
                };

                var normal = CalculateNormal(p0, p1, p2);
                for (int i = 0; i < 4; i++) mesh.Normals.Add(normal);

                var geo = new GeometryModel3D(mesh, material) { BackMaterial = material };
                group.Children.Add(geo);
            }

            AddFace(D, C, B, A); // front
            AddFace(E, F, G, H); // back
            AddFace(B, C, G, F); // right
            AddFace(A, D, H, E); // left
            AddFace(D, C, G, H); // top
            AddFace(A, B, F, E); // bottom

            return group;
        }

        public static Model3DGroup CreateBoxWithEdges(Point3D origin, double width, double height, double depth, Brush fill, Color edgeColor, double edgeThickness = 0.3)
        {
            var group = CreateBox(origin, width, height, depth, fill);

            // Corner points
            Point3D A = new(origin.X, origin.Y, origin.Z);
            Point3D B = new(origin.X + width, origin.Y, origin.Z);
            Point3D C = new(origin.X + width, origin.Y + height, origin.Z);
            Point3D D = new(origin.X, origin.Y + height, origin.Z);
            Point3D E = new(origin.X, origin.Y, origin.Z + depth);
            Point3D F = new(origin.X + width, origin.Y, origin.Z + depth);
            Point3D G = new(origin.X + width, origin.Y + height, origin.Z + depth);
            Point3D H = new(origin.X, origin.Y + height, origin.Z + depth);

            // 12 edges
            AddEdgePrism(group, A, B, edgeColor, edgeThickness);
            AddEdgePrism(group, B, C, edgeColor, edgeThickness);
            AddEdgePrism(group, C, D, edgeColor, edgeThickness);
            AddEdgePrism(group, D, A, edgeColor, edgeThickness);

            AddEdgePrism(group, E, F, edgeColor, edgeThickness);
            AddEdgePrism(group, F, G, edgeColor, edgeThickness);
            AddEdgePrism(group, G, H, edgeColor, edgeThickness);
            AddEdgePrism(group, H, E, edgeColor, edgeThickness);

            AddEdgePrism(group, A, E, edgeColor, edgeThickness);
            AddEdgePrism(group, B, F, edgeColor, edgeThickness);
            AddEdgePrism(group, C, G, edgeColor, edgeThickness);
            AddEdgePrism(group, D, H, edgeColor, edgeThickness);

            return group;
        }

        private static void AddEdgePrism(Model3DGroup parent, Point3D p0, Point3D p1, Color edgeColor, double thickness)
        {
            Vector3D dir = p1 - p0;
            double length = dir.Length;
            if (length <= 0) return;
            dir.Normalize();

            // Choose an up vector not parallel to dir
            Vector3D up = Math.Abs(Vector3D.DotProduct(dir, new Vector3D(0, 1, 0))) > 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
            Vector3D side = Vector3D.CrossProduct(dir, up);
            side.Normalize();
            up = Vector3D.CrossProduct(side, dir); // re-orthogonalize
            up.Normalize();

            double r = thickness / 2.0;
            Vector3D upR = up * r;
            Vector3D sideR = side * r;

            // 8 corners of rectangular prism around the edge
            Point3D a = p0 - upR - sideR;
            Point3D b = p0 + upR - sideR;
            Point3D c = p0 + upR + sideR;
            Point3D d = p0 - upR + sideR;

            Point3D e = a + dir * length;
            Point3D f = b + dir * length;
            Point3D g = c + dir * length;
            Point3D h = d + dir * length;

            var brush = new SolidColorBrush(edgeColor);
            brush.Freeze();
            var material = new DiffuseMaterial(brush);

            // Faces
            AddQuad(parent, a, b, c, d, material); // start
            AddQuad(parent, e, f, g, h, material); // end
            AddQuad(parent, b, f, g, c, material); // side1
            AddQuad(parent, a, e, h, d, material); // side2
            AddQuad(parent, d, c, g, h, material); // side3
            AddQuad(parent, a, b, f, e, material); // side4
        }

        private static void AddQuad(Model3DGroup parent, Point3D p0, Point3D p1, Point3D p2, Point3D p3, Material m)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection { p0, p1, p2, p3 },
                TriangleIndices = new Int32Collection { 0, 1, 2, 2, 3, 0 }
            };
            var n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
            if (n.Length > 0) n.Normalize();
            mesh.Normals.Add(n); mesh.Normals.Add(n); mesh.Normals.Add(n); mesh.Normals.Add(n);
            var geo = new GeometryModel3D(mesh, m) { BackMaterial = m };
            parent.Children.Add(geo);
        }

        private static Vector3D CalculateNormal(Point3D p0, Point3D p1, Point3D p2)
        {
            var n = Vector3D.CrossProduct(p1 - p0, p2 - p0);
            if (n.Length > 0) n.Normalize();
            return n;
        }
    }
}
