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

        /// <summary>
        /// Builds a box as just two <see cref="GeometryModel3D"/>s — one merged fill mesh (only the
        /// <paramref name="visibleFaces"/>) and one merged mesh holding all 12 edge prisms — instead of the
        /// ~78 models <see cref="CreateBoxWithEdges"/> produces. Visual result is identical from outside;
        /// both meshes are returned in a group so per-box hit-test mapping keeps working.
        /// </summary>
        public static Model3DGroup CreateBoxMerged(
            Point3D origin, double width, double height, double depth,
            Brush fill, Color edgeColor, double edgeThickness = 0.3,
            BoxFaces visibleFaces = BoxFaces.All)
        {
            var group = new Model3DGroup();

            Point3D A = new(origin.X, origin.Y, origin.Z);
            Point3D B = new(origin.X + width, origin.Y, origin.Z);
            Point3D C = new(origin.X + width, origin.Y + height, origin.Z);
            Point3D D = new(origin.X, origin.Y + height, origin.Z);
            Point3D E = new(origin.X, origin.Y, origin.Z + depth);
            Point3D F = new(origin.X + width, origin.Y, origin.Z + depth);
            Point3D G = new(origin.X + width, origin.Y + height, origin.Z + depth);
            Point3D H = new(origin.X, origin.Y + height, origin.Z + depth);

            // Fill: one mesh, visible faces only (winding matches CreateBox)
            if (visibleFaces != BoxFaces.None)
            {
                var fillMesh = new MeshGeometry3D();
                if (visibleFaces.HasFlag(BoxFaces.Front))  AppendQuad(fillMesh, D, C, B, A);
                if (visibleFaces.HasFlag(BoxFaces.Back))   AppendQuad(fillMesh, E, F, G, H);
                if (visibleFaces.HasFlag(BoxFaces.Right))  AppendQuad(fillMesh, B, C, G, F);
                if (visibleFaces.HasFlag(BoxFaces.Left))   AppendQuad(fillMesh, A, D, H, E);
                if (visibleFaces.HasFlag(BoxFaces.Top))    AppendQuad(fillMesh, D, C, G, H);
                if (visibleFaces.HasFlag(BoxFaces.Bottom)) AppendQuad(fillMesh, A, B, F, E);

                if (fillMesh.Positions.Count > 0)
                {
                    var material = new DiffuseMaterial(fill);
                    group.Children.Add(new GeometryModel3D(fillMesh, material) { BackMaterial = material });
                }
            }

            // Edges: all 12 prisms merged into one mesh (kept regardless of culling for identical look)
            var edgeMesh = new MeshGeometry3D();
            AppendEdgePrism(edgeMesh, A, B, edgeThickness);
            AppendEdgePrism(edgeMesh, B, C, edgeThickness);
            AppendEdgePrism(edgeMesh, C, D, edgeThickness);
            AppendEdgePrism(edgeMesh, D, A, edgeThickness);
            AppendEdgePrism(edgeMesh, E, F, edgeThickness);
            AppendEdgePrism(edgeMesh, F, G, edgeThickness);
            AppendEdgePrism(edgeMesh, G, H, edgeThickness);
            AppendEdgePrism(edgeMesh, H, E, edgeThickness);
            AppendEdgePrism(edgeMesh, A, E, edgeThickness);
            AppendEdgePrism(edgeMesh, B, F, edgeThickness);
            AppendEdgePrism(edgeMesh, C, G, edgeThickness);
            AppendEdgePrism(edgeMesh, D, H, edgeThickness);

            if (edgeMesh.Positions.Count > 0)
            {
                var brush = new SolidColorBrush(edgeColor);
                brush.Freeze();
                var edgeMaterial = new DiffuseMaterial(brush);
                group.Children.Add(new GeometryModel3D(edgeMesh, edgeMaterial) { BackMaterial = edgeMaterial });
            }

            return group;
        }

        /// <summary>
        /// Draws a dimension annotation: main line, perpendicular end ticks, and optional thin extension
        /// lines from object-boundary points to the line endpoints.
        /// </summary>
        public static void CreateDimAnnotation(
            Model3DGroup target,
            Point3D lineStart, Point3D lineEnd,
            Vector3D tickDir, double tickLen,
            Point3D? objStart = null, Point3D? objEnd = null,
            Color? dimColor = null, Color? extColor = null,
            double dimThickness = 0.5, double extThickness = 0.2)
        {
            var dColor = dimColor ?? Colors.White;
            var eColor = extColor ?? Color.FromRgb(160, 160, 160);

            AddEdgePrism(target, lineStart, lineEnd, dColor, dimThickness);

            var td = tickDir; td.Normalize();
            AddEdgePrism(target, lineStart - td * (tickLen / 2), lineStart + td * (tickLen / 2), dColor, dimThickness);
            AddEdgePrism(target, lineEnd - td * (tickLen / 2), lineEnd + td * (tickLen / 2), dColor, dimThickness);

            if (objStart.HasValue)
                AddEdgePrism(target, objStart.Value, lineStart, eColor, extThickness);
            if (objEnd.HasValue)
                AddEdgePrism(target, objEnd.Value, lineEnd, eColor, extThickness);
        }

        internal static void AddEdgePrism(Model3DGroup parent, Point3D p0, Point3D p1, Color edgeColor, double thickness)
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

        /// <summary>Appends one quad (two triangles) to an existing mesh, offsetting indices.</summary>
        private static void AppendQuad(MeshGeometry3D mesh, Point3D p0, Point3D p1, Point3D p2, Point3D p3)
        {
            int b = mesh.Positions.Count;
            mesh.Positions.Add(p0); mesh.Positions.Add(p1); mesh.Positions.Add(p2); mesh.Positions.Add(p3);
            var n = CalculateNormal(p0, p1, p2);
            mesh.Normals.Add(n); mesh.Normals.Add(n); mesh.Normals.Add(n); mesh.Normals.Add(n);
            mesh.TriangleIndices.Add(b + 0); mesh.TriangleIndices.Add(b + 1); mesh.TriangleIndices.Add(b + 2);
            mesh.TriangleIndices.Add(b + 2); mesh.TriangleIndices.Add(b + 3); mesh.TriangleIndices.Add(b + 0);
        }

        /// <summary>Appends a rectangular edge prism (6 quads) to an existing mesh. Mirrors <see cref="AddEdgePrism"/>.</summary>
        private static void AppendEdgePrism(MeshGeometry3D mesh, Point3D p0, Point3D p1, double thickness)
        {
            Vector3D dir = p1 - p0;
            double length = dir.Length;
            if (length <= 0) return;
            dir.Normalize();

            Vector3D up = Math.Abs(Vector3D.DotProduct(dir, new Vector3D(0, 1, 0))) > 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
            Vector3D side = Vector3D.CrossProduct(dir, up);
            side.Normalize();
            up = Vector3D.CrossProduct(side, dir);
            up.Normalize();

            double r = thickness / 2.0;
            Vector3D upR = up * r;
            Vector3D sideR = side * r;

            Point3D a = p0 - upR - sideR;
            Point3D b = p0 + upR - sideR;
            Point3D c = p0 + upR + sideR;
            Point3D d = p0 - upR + sideR;
            Point3D e = a + dir * length;
            Point3D f = b + dir * length;
            Point3D g = c + dir * length;
            Point3D h = d + dir * length;

            AppendQuad(mesh, a, b, c, d);
            AppendQuad(mesh, e, f, g, h);
            AppendQuad(mesh, b, f, g, c);
            AppendQuad(mesh, a, e, h, d);
            AppendQuad(mesh, d, c, g, h);
            AppendQuad(mesh, a, b, f, e);
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
