namespace Stack_Solver.Helpers.Rendering
{
    /// <summary>Identifies the six axis-aligned faces of a box, for selective rendering.</summary>
    [Flags]
    public enum BoxFaces
    {
        None   = 0,
        Front  = 1 << 0, // -Z
        Back   = 1 << 1, // +Z
        Right  = 1 << 2, // +X
        Left   = 1 << 3, // -X
        Top    = 1 << 4, // +Y
        Bottom = 1 << 5, // -Y
        All    = Front | Back | Right | Left | Top | Bottom
    }

    /// <summary>Axis-aligned bounding box of a single rendered box, in scene coordinates.</summary>
    public readonly record struct BoxBounds(
        double MinX, double MaxX,
        double MinY, double MaxY,
        double MinZ, double MaxZ);

    /// <summary>
    /// Computes, per box, which faces are potentially visible (i.e. not fully covered by a neighbouring
    /// box or the pallet top). View-independent and conservative: a face is culled only when the
    /// rectangle just outside it is provably covered by the union of the opposing faces touching that
    /// plane, so a face that could ever be seen from any camera angle is always kept.
    /// </summary>
    public static class FaceCuller
    {
        private const double Eps = 1e-6;

        private enum Plane { X, Y, Z }
        private readonly record struct Rect(double U0, double U1, double V0, double V1);

        /// <param name="boxes">All boxes that can occlude one another (e.g. one pallet's boxes).</param>
        /// <param name="palletTopY">Y of the pallet's top surface; bottom faces resting on it are culled. Null disables.</param>
        /// <param name="palletMinX">Pallet footprint, used only to cover bottom faces resting on the pallet.</param>
        public static BoxFaces[] Compute(
            IReadOnlyList<BoxBounds> boxes,
            double? palletTopY = null,
            double palletMinX = 0, double palletMaxX = 0,
            double palletMinZ = 0, double palletMaxZ = 0)
        {
            int n = boxes.Count;
            var result = new BoxFaces[n];

            for (int i = 0; i < n; i++)
            {
                var b = boxes[i];
                BoxFaces vis = BoxFaces.None;

                if (!IsCovered(boxes, i, Plane.X, neighborMin: true,  b.MaxX, b.MinY, b.MaxY, b.MinZ, b.MaxZ, null)) vis |= BoxFaces.Right;
                if (!IsCovered(boxes, i, Plane.X, neighborMin: false, b.MinX, b.MinY, b.MaxY, b.MinZ, b.MaxZ, null)) vis |= BoxFaces.Left;
                if (!IsCovered(boxes, i, Plane.Y, neighborMin: true,  b.MaxY, b.MinX, b.MaxX, b.MinZ, b.MaxZ, null)) vis |= BoxFaces.Top;

                Rect? palletRect = palletTopY is double pty && Math.Abs(b.MinY - pty) <= Eps
                    ? new Rect(palletMinX, palletMaxX, palletMinZ, palletMaxZ)
                    : null;
                if (!IsCovered(boxes, i, Plane.Y, neighborMin: false, b.MinY, b.MinX, b.MaxX, b.MinZ, b.MaxZ, palletRect)) vis |= BoxFaces.Bottom;

                if (!IsCovered(boxes, i, Plane.Z, neighborMin: true,  b.MaxZ, b.MinX, b.MaxX, b.MinY, b.MaxY, null)) vis |= BoxFaces.Back;
                if (!IsCovered(boxes, i, Plane.Z, neighborMin: false, b.MinZ, b.MinX, b.MaxX, b.MinY, b.MaxY, null)) vis |= BoxFaces.Front;

                result[i] = vis;
            }

            return result;
        }

        /// <summary>
        /// True when the self face — lying on <paramref name="plane"/> at <paramref name="coord"/> and spanning
        /// [u0,u1]×[v0,v1] — is fully covered by opposing faces of other boxes (plus an optional pallet rect).
        /// </summary>
        private static bool IsCovered(
            IReadOnlyList<BoxBounds> boxes, int self,
            Plane plane, bool neighborMin, double coord,
            double u0, double u1, double v0, double v1, Rect? extra)
        {
            var covers = new List<Rect>();
            if (extra is Rect ex) covers.Add(ex);

            for (int j = 0; j < boxes.Count; j++)
            {
                if (j == self) continue;
                var b = boxes[j];

                double face = plane switch
                {
                    Plane.X => neighborMin ? b.MinX : b.MaxX,
                    Plane.Y => neighborMin ? b.MinY : b.MaxY,
                    _       => neighborMin ? b.MinZ : b.MaxZ,
                };
                if (Math.Abs(face - coord) > Eps) continue;

                covers.Add(plane switch
                {
                    Plane.X => new Rect(b.MinY, b.MaxY, b.MinZ, b.MaxZ),
                    Plane.Y => new Rect(b.MinX, b.MaxX, b.MinZ, b.MaxZ),
                    _       => new Rect(b.MinX, b.MaxX, b.MinY, b.MaxY),
                });
            }

            return RectCovered(u0, u1, v0, v1, covers);
        }

        /// <summary>True when the union of <paramref name="covers"/> fully contains the rectangle [u0,u1]×[v0,v1].</summary>
        private static bool RectCovered(double u0, double u1, double v0, double v1, List<Rect> covers)
        {
            if (u1 - u0 <= Eps || v1 - v0 <= Eps) return true; // degenerate, nothing to draw
            if (covers.Count == 0) return false;

            // Sweep along U: split into slabs at every covering rect's U edge, then check each slab is
            // fully covered along V by the rects spanning it.
            var xs = new SortedSet<double> { u0, u1 };
            foreach (var c in covers)
            {
                if (c.U0 > u0 + Eps && c.U0 < u1 - Eps) xs.Add(c.U0);
                if (c.U1 > u0 + Eps && c.U1 < u1 - Eps) xs.Add(c.U1);
            }

            var xl = xs.ToList();
            for (int k = 0; k + 1 < xl.Count; k++)
            {
                double xa = xl[k], xb = xl[k + 1];
                if (xb - xa <= Eps) continue;

                var ivs = new List<(double a, double b)>();
                foreach (var c in covers)
                {
                    if (c.U0 <= xa + Eps && c.U1 >= xb - Eps)
                        ivs.Add((Math.Max(c.V0, v0), Math.Min(c.V1, v1)));
                }

                if (!IntervalsCover(ivs, v0, v1)) return false;
            }

            return true;
        }

        private static bool IntervalsCover(List<(double a, double b)> ivs, double lo, double hi)
        {
            if (ivs.Count == 0) return false;
            ivs.Sort((p, q) => p.a.CompareTo(q.a));

            double cursor = lo;
            foreach (var (a, b) in ivs)
            {
                if (a > cursor + Eps) return false; // gap before this interval
                if (b > cursor) cursor = b;
                if (cursor >= hi - Eps) return true;
            }
            return cursor >= hi - Eps;
        }
    }
}
