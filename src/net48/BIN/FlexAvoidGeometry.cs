using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace BIN;

internal sealed class FlexAvoidGeometry : IDisposable
{
    private sealed class Body
    {
        internal Solid Solid;
        internal List<Face> Faces;
        internal List<Curve> Edges;
        internal XYZ Min, Max;
    }
    private readonly List<Body> _bodies = new List<Body>();
    internal readonly List<XYZ[]> Triangles = new List<XYZ[]>();
    internal bool HasBodies => _bodies.Count > 0;

    internal FlexAvoidGeometry(Element obstacle, Transform transform, bool requireSolid = true)
    {
        try
        {
            using (var options = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false })
            using (GeometryElement geometry = obstacle.get_Geometry(options)) Collect(geometry, transform);
            if (requireSolid && _bodies.Count == 0)
                throw new InvalidOperationException("Vật cản không có Solid 3D kín. Không dùng BoundingBox thay cho kiểm tra va chạm.");
        }
        catch { Dispose(); throw; }
    }

    private void Collect(GeometryElement geometry, Transform transform)
    {
        if (geometry == null) return;
        foreach (GeometryObject item in geometry)
        {
            if (item is GeometryInstance instance)
            {
                // Instance geometry already includes family placement. Apply the
                // link total transform once, including rotation/translation.
                using (GeometryElement nested = instance.GetInstanceGeometry()) Collect(nested, transform);
            }
            else if (item is Solid source && source.Faces.Size > 0 && source.Volume > 1e-10)
            {
                if (_bodies.Count >= 64) throw new InvalidOperationException("Vật cản có quá nhiều Solid để kiểm tra trong một thao tác.");
                var body = new Body { Solid = SolidUtils.CreateTransformed(source, transform) };
                _bodies.Add(body);
                body.Faces = body.Solid.Faces.Cast<Face>().ToList();
                body.Edges = body.Solid.Edges.Cast<Edge>().Select(e => e.AsCurve()).ToList();
                using (BoundingBoxXYZ box = body.Solid.GetBoundingBox())
                {
                    var corners = new List<XYZ>();
                    for (int i = 0; i < 8; i++) corners.Add(box.Transform.OfPoint(new XYZ(
                        (i & 4) == 0 ? box.Min.X : box.Max.X,
                        (i & 2) == 0 ? box.Min.Y : box.Max.Y,
                        (i & 1) == 0 ? box.Min.Z : box.Max.Z)));
                    body.Min = new XYZ(corners.Min(p => p.X), corners.Min(p => p.Y), corners.Min(p => p.Z));
                    body.Max = new XYZ(corners.Max(p => p.X), corners.Max(p => p.Y), corners.Max(p => p.Z));
                }
                foreach (Face face in body.Faces)
                    using (Mesh mesh = face.Triangulate(1))
                        for (int i = 0; i < mesh.NumTriangles; i++)
                        {
                            if (Triangles.Count >= 100000) throw new InvalidOperationException("Vật cản có quá nhiều mặt tam giác.");
                            MeshTriangle t = mesh.get_Triangle(i);
                            Triangles.Add(new[] { t.get_Vertex(0), t.get_Vertex(1), t.get_Vertex(2) });
                        }
            }
        }
    }

    internal FlexAvoidMath.PointDistance Distance(XYZ point)
    {
        var result = new FlexAvoidMath.PointDistance { SignedDistance = double.PositiveInfinity };
        foreach (Body body in _bodies)
        {
            double dx = Math.Max(0, Math.Max(body.Min.X - point.X, point.X - body.Max.X));
            double dy = Math.Max(0, Math.Max(body.Min.Y - point.Y, point.Y - body.Max.Y));
            double dz = Math.Max(0, Math.Max(body.Min.Z - point.Z, point.Z - body.Max.Z));
            double boxDistance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            // AABB is only a broad-phase lower bound, never collision truth.
            if (boxDistance > 0 && boxDistance >= result.SignedDistance) continue;
            double nearest = double.PositiveInfinity;
            XYZ surface = null;
            void Consider(XYZ p)
            {
                double d = point.DistanceTo(p);
                if (d < nearest) { nearest = d; surface = p; }
            }
            foreach (Face face in body.Faces)
            {
                // Null means outside the trimmed face. Check its boundary edges
                // too, instead of using an infinite supporting plane.
                IntersectionResult projection = face.Project(point);
                if (projection != null) Consider(projection.XYZPoint);
            }
            foreach (Curve edge in body.Edges)
            {
                Consider(edge.GetEndPoint(0)); Consider(edge.GetEndPoint(1));
                IntersectionResult projection = edge.Project(point);
                if (projection != null && projection.Parameter >= edge.GetEndParameter(0) - 1e-9
                    && projection.Parameter <= edge.GetEndParameter(1) + 1e-9) Consider(projection.XYZPoint);
            }
            if (surface == null) throw new InvalidOperationException("Không tìm được mặt/biên gần nhất của Solid.");
            bool inside = false;
            if (boxDistance == 0 && nearest > 1e-7)
            {
                XYZ direction = new XYZ(0.873, 0.371, 0.318).Normalize();
                double rayLength = 2 * body.Min.DistanceTo(body.Max) + 1;
                using (Line ray = Line.CreateBound(point, point + direction * rayLength))
                using (SolidCurveIntersection hit = body.Solid.IntersectWithCurve(ray, null))
                    for (int i = 0; i < hit.SegmentCount; i++)
                        using (Curve segment = hit.GetCurveSegment(i))
                            if (segment.GetEndPoint(0).DistanceTo(point) < 1e-7
                                || segment.GetEndPoint(1).DistanceTo(point) < 1e-7) inside = true;
            }
            double signed = inside ? -nearest : nearest;
            if (signed < result.SignedDistance) { result.SignedDistance = signed; result.SurfacePoint = surface; }
        }
        return result;
    }

    internal FlexAvoidMath.Check Check(MEPCurve flex)
    {
        // Read the actual regenerated centreline, never an endpoint chord or a
        // newly interpolated approximation of Flex.Points.
        Curve curve = (flex.Location as LocationCurve)?.Curve;
        if (curve == null)
        {
            using (var options = new Options { DetailLevel = ViewDetailLevel.Fine })
            using (GeometryElement geometry = flex.get_Geometry(options))
            {
                IList<XYZ> points = Points(flex);
                var all = Curves(geometry).ToList();
                var candidates = all.Where(c => c.IsBound &&
                    ((c.GetEndPoint(0).DistanceTo(points[0]) < 1e-5 && c.GetEndPoint(1).DistanceTo(points[points.Count - 1]) < 1e-5)
                    || (c.GetEndPoint(1).DistanceTo(points[0]) < 1e-5 && c.GetEndPoint(0).DistanceTo(points[points.Count - 1]) < 1e-5))).ToList();
                foreach (Curve other in all.Except(candidates)) other.Dispose();
                if (candidates.Count != 1)
                {
                    foreach (Curve candidate in candidates) candidate.Dispose();
                    throw new InvalidOperationException("Revit chưa cung cấp một đường tâm spline duy nhất để xác minh Flex.");
                }
                curve = candidates[0];
            }
        }
        try { return FlexAvoidMath.Measure(Spans(curve), Distance); }
        finally { curve.Dispose(); }
    }

    private static IEnumerable<Curve> Curves(GeometryElement geometry)
    {
        if (geometry == null) yield break;
        foreach (GeometryObject item in geometry)
        {
            if (item is Curve curve && (curve is HermiteSpline || curve is Line)) yield return curve.Clone();
            if (item is GeometryInstance instance)
                using (GeometryElement nested = instance.GetInstanceGeometry())
                    foreach (Curve child in Curves(nested)) yield return child;
        }
    }

    private static IList<FlexAvoidMath.Cubic> Spans(Curve curve)
    {
        if (curve is Line)
        {
            XYZ a = curve.GetEndPoint(0), d = curve.GetEndPoint(1);
            return new[] { new FlexAvoidMath.Cubic(a, a + (d - a) / 3, a + (d - a) * (2.0 / 3), d) };
        }
        if (!(curve is HermiteSpline spline) || spline.IsPeriodic || !spline.IsBound)
            throw new InvalidOperationException("Chỉ xác minh đường tâm Hermite spline không tuần hoàn của Flex.");
        double first = spline.GetEndParameter(0), last = spline.GetEndParameter(1);
        var knots = spline.Parameters.Cast<double>().Where(t => t > first && t < last).Concat(new[] { first, last }).Distinct().OrderBy(t => t).ToList();
        var spans = new List<FlexAvoidMath.Cubic>();
        for (int i = 1; i < knots.Count; i++)
        {
            double a = knots[i - 1], b = knots[i], scale = (b - a) / 3;
            using (Transform da = spline.ComputeDerivatives(a, false))
            using (Transform db = spline.ComputeDerivatives(b, false))
            {
                var span = new FlexAvoidMath.Cubic(da.Origin, da.Origin + da.BasisX * scale,
                    db.Origin - db.BasisX * scale, db.Origin);
                // Convex-hull bounds are valid only for the actual cubic spans.
                foreach (double t in new[] { 0.25, 0.5, 0.75 })
                {
                    double u = 1 - t;
                    XYZ p = span.A * (u * u * u) + span.B * (3 * u * u * t)
                        + span.C * (3 * u * t * t) + span.D * (t * t * t);
                    if (p.DistanceTo(spline.Evaluate(a + (b - a) * t, false)) > 1e-6)
                        throw new InvalidOperationException("Spline Revit không khớp các đoạn Hermite cubic; chưa thể xác minh khoảng hở.");
                }
                spans.Add(span);
            }
        }
        return spans;
    }

    internal static IList<XYZ> Points(MEPCurve flex)
        => flex is FlexDuct duct ? duct.Points : ((FlexPipe)flex).Points;
    internal static void SetPoints(MEPCurve flex, IList<XYZ> points)
    {
        if (flex is FlexDuct duct) duct.Points = points; else ((FlexPipe)flex).Points = points;
    }

    public void Dispose()
    {
        foreach (Body body in _bodies)
        {
            if (body.Edges != null) foreach (Curve edge in body.Edges) edge.Dispose();
            body.Solid?.Dispose();
        }
        _bodies.Clear();
    }
}
