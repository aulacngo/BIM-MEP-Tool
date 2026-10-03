using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIN;

// Pure geometry: no Document access. All lengths are Revit internal feet.
internal static class FlexAvoidMath
{
    internal const double Mm = 1.0 / 304.8;
    internal const double DistanceTolerance = 0.5 * Mm;

    internal sealed class Cubic
    {
        internal readonly XYZ A, B, C, D;
        internal Cubic(XYZ a, XYZ b, XYZ c, XYZ d) { A = a; B = b; C = c; D = d; }
        internal XYZ Middle => (A + 3 * B + 3 * C + D) / 8;
        internal void Split(out Cubic left, out Cubic right)
        {
            XYZ ab = (A + B) / 2, bc = (B + C) / 2, cd = (C + D) / 2;
            XYZ abc = (ab + bc) / 2, bcd = (bc + cd) / 2, middle = (abc + bcd) / 2;
            left = new Cubic(A, ab, abc, middle);
            right = new Cubic(middle, bcd, cd, D);
        }
    }

    internal sealed class PointDistance
    {
        internal double SignedDistance;
        internal XYZ SurfacePoint;
    }

    internal sealed class Check
    {
        internal double Minimum, LowerBound;
        internal XYZ CurvePoint, SurfacePoint;
        internal int Evaluations;
        internal double Uncertainty => Minimum - LowerBound;
    }

    private sealed class Cell
    {
        internal Cubic Curve;
        internal double Lower;
        internal int Id;
    }

    // Signed distance is 1-Lipschitz. A Bezier curve lies in its control hull,
    // so distance(midpoint) - max distance(control, midpoint) is a safe lower bound.
    // This detects narrow flange hits between samples, including an inside centreline.
    internal static Check Measure(IList<Cubic> spans, Func<XYZ, PointDistance> distance)
    {
        if (spans == null || spans.Count == 0 || spans.Count > 2048)
            throw new InvalidOperationException("Không đọc được các đoạn spline thực của Flex.");
        var watch = Stopwatch.StartNew();
        var result = new Check { Minimum = double.PositiveInfinity };
        int sequence = 0;
        var queue = new SortedSet<Cell>(Comparer<Cell>.Create((x, y) =>
        {
            int order = x.Lower.CompareTo(y.Lower);
            return order == 0 ? x.Id.CompareTo(y.Id) : order;
        }));
        PointDistance Probe(XYZ point)
        {
            if (++result.Evaluations > 8192 || watch.Elapsed.TotalSeconds > 8)
                throw new InvalidOperationException("Hình học quá phức tạp: chưa xác minh được khoảng hở trong giới hạn kiểm tra. Không áp dụng thay đổi.");
            PointDistance value = distance(point);
            if (value == null || !Finite(value.SignedDistance) || value.SurfacePoint == null)
                throw new InvalidOperationException("Không xác minh được khoảng cách tới Solid.");
            if (value.SignedDistance < result.Minimum)
            {
                result.Minimum = value.SignedDistance;
                result.CurvePoint = point;
                result.SurfacePoint = value.SurfacePoint;
            }
            return value;
        }
        void Enqueue(Cubic span)
        {
            XYZ middle = span.Middle;
            double radius = new[] { span.A, span.B, span.C, span.D }.Max(p => p.DistanceTo(middle));
            double lower = Probe(middle).SignedDistance - radius;
            queue.Add(new Cell { Curve = span, Lower = lower, Id = sequence++ });
        }
        foreach (Cubic span in spans)
        {
            Probe(span.A); Probe(span.D); Enqueue(span);
        }
        while (result.Minimum - queue.Min.Lower > DistanceTolerance)
        {
            Cell cell = queue.Min;
            queue.Remove(cell);
            cell.Curve.Split(out Cubic left, out Cubic right);
            Enqueue(left); Enqueue(right);
        }
        result.LowerBound = Math.Min(result.Minimum, queue.Min.Lower);
        return result;
    }

    internal sealed class Route
    {
        internal XYZ Start, End, Axis;
        internal double Length, Entry, Exit, Low, High, Transition, Reach;
        internal List<XYZ> Points(bool up, double extra)
        {
            double z = up ? High + Reach + extra : Low - Reach - extra;
            XYZ At(double x, double height) => new XYZ(Start.X + Axis.X * x, Start.Y + Axis.Y * x, height);
            double OriginalZ(double x) => Start.Z + (End.Z - Start.Z) * x / Length;
            if (Transition == 0)
            {
                // A floor/long beam can cover the entire horizontal span while
                // both endpoints already lie on the requested safe side.
                double plane = up ? High + Reach : Low - Reach;
                if (up ? Start.Z < plane || End.Z < plane : Start.Z > plane || End.Z > plane)
                    throw new InvalidOperationException("Không đủ chỗ chuyển tiếp ở hai đầu theo hướng đã chọn. Giữ nguyên kết nối và bố trí tuyến thủ công.");
                return new List<XYZ> { Start, At(Length * 0.25, z), At(Length * 0.5, z), At(Length * 0.75, z), End };
            }
            double before = Entry - Reach, after = Exit + Reach;
            return new List<XYZ>
            {
                Start,
                At(before - Transition, OriginalZ(before - Transition)),
                At(before, z), At((before + after) / 2, z), At(after, z),
                At(after + Transition, OriginalZ(after + Transition)),
                End
            };
        }
    }

    // Clip actual face triangles to the horizontal pipe corridor. A long oblique
    // I-beam does not force the route to span the beam's entire bounding box.
    // Mesh is only a route proposal; acceptance always uses analytic Solid distance.
    internal static Route Plan(XYZ start, XYZ end, IEnumerable<XYZ[]> triangles, double reach)
    {
        if (!Finite(reach) || reach <= 0) throw new InvalidOperationException("Bán kính bao ngoài không hợp lệ.");
        XYZ horizontal = new XYZ(end.X - start.X, end.Y - start.Y, 0);
        double length = horizontal.GetLength();
        if (length < 20 * Mm)
            throw new InvalidOperationException("Hai đầu Flex gần thẳng đứng; không đủ chiều ngang để bố trí tuyến né dầm.");
        XYZ axis = horizontal / length, side = new XYZ(-axis.Y, axis.X, 0);
        var route = new Route
        {
            Start = start, End = end, Axis = axis, Length = length, Reach = reach,
            Entry = double.PositiveInfinity, Exit = double.NegativeInfinity,
            Low = double.PositiveInfinity, High = double.NegativeInfinity
        };
        foreach (XYZ[] triangle in triangles)
        {
            List<XYZ> polygon = Clip(triangle.ToList(), start, side, reach);
            polygon = Clip(polygon, start, -side, reach);
            foreach (XYZ p in polygon)
            {
                double x = (p - start).DotProduct(axis);
                route.Entry = Math.Min(route.Entry, x); route.Exit = Math.Max(route.Exit, x);
                route.Low = Math.Min(route.Low, p.Z); route.High = Math.Max(route.High, p.Z);
            }
        }
        if (!Finite(route.Entry)) throw new InvalidOperationException("Vật cản nằm ngoài hành lang nối hai đầu. Cần bố trí tuyến Flex thủ công.");
        double room = Math.Min(route.Entry - reach, length - route.Exit - reach);
        route.Transition = room < 40 * Mm ? 0 : Math.Min(Math.Max(2 * reach, 100 * Mm), room * 0.7);
        return route;
    }

    private static List<XYZ> Clip(List<XYZ> polygon, XYZ origin, XYZ normal, double limit)
    {
        var output = new List<XYZ>();
        if (polygon.Count == 0) return output;
        XYZ previous = polygon[polygon.Count - 1];
        double before = (previous - origin).DotProduct(normal) - limit;
        foreach (XYZ current in polygon)
        {
            double after = (current - origin).DotProduct(normal) - limit;
            if ((before <= 0) != (after <= 0))
                output.Add(previous + (current - previous) * (before / (before - after)));
            if (after <= 0) output.Add(current);
            previous = current; before = after;
        }
        return output;
    }

    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
