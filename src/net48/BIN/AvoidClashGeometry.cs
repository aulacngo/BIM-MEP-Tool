using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BIN;

public enum BypassShapeMode { U45, U90, Z45 }
public enum BypassDirection { Up, Down, Left, Right }

/// <summary>Document-free routing math; all distances are internal feet.</summary>
internal static class AvoidClashGeometry
{
    internal const double Tolerance = 1e-8;

    internal static XYZ OffsetDirection(XYZ axis, BypassDirection direction)
    {
        if (!Enum.IsDefined(typeof(BypassDirection), direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        XYZ n = Unit(axis);
        if (direction == BypassDirection.Up || direction == BypassDirection.Down)
        {
            // A global-Z translation of a sloped/vertical line does not produce 45/90 degree elbows.
            if (Math.Abs(n.Z) > Tolerance)
                throw new InvalidOperationException("Ne Len/Xuong can tuyen nam ngang de giu dung goc co 45/90 do. Tuyen doc co the ne Trai/Phai.");
            return direction == BypassDirection.Up ? XYZ.BasisZ : -XYZ.BasisZ;
        }
        XYZ left = XYZ.BasisZ.CrossProduct(n);
        if (left.GetLength() <= Tolerance)
            throw new InvalidOperationException("Khong the ne Trai/Phai cho tuyen dung.");
        return direction == BypassDirection.Left ? left.Normalize() : -left.Normalize();
    }

    internal static XYZ[] Build(XYZ start, XYZ end, XYZ offsetDirection, double entry, double exit,
        double offset, BypassShapeMode mode, double minimumLength)
    {
        if (!Enum.IsDefined(typeof(BypassShapeMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Finite(entry) || !Finite(exit) || !Finite(offset) || !Finite(minimumLength)
            || offset <= 0 || minimumLength <= 0 || exit <= entry)
            throw new InvalidOperationException("Thong so hinh hoc bypass khong hop le.");
        if (start == null || end == null || !Finite(start.X) || !Finite(start.Y) || !Finite(start.Z)
            || !Finite(end.X) || !Finite(end.Y) || !Finite(end.Z))
            throw new InvalidOperationException("Toa do tuyen khong hop le.");
        XYZ n = Unit(end - start);
        XYZ u = Unit(offsetDirection);
        if (Math.Abs(n.DotProduct(u)) > Tolerance)
            throw new InvalidOperationException("Huong ne phai vuong goc voi truc tuyen de giu dung goc co.");
        double length = (end - start).GetLength();
        double run = mode == BypassShapeMode.U90 ? 0 : offset;
        if (entry - run <= minimumLength || (mode != BypassShapeMode.Z45 && length - exit - run <= minimumLength)
            || (mode == BypassShapeMode.Z45 && length - entry <= minimumLength))
            throw new InvalidOperationException(mode == BypassShapeMode.U90
                ? "Tuyen khong du chieu dai truoc/sau vat can de tao U90."
                : "Tuyen khong du chieu dai cho co 45 do. Moi doan xien can chieu chay bang khoang ne; hay chon U90 hoac tuyen dai hon.");
        XYZ c1 = start + n * (entry - run);
        XYZ c2 = start + n * entry + u * offset;
        XYZ[] points = mode == BypassShapeMode.Z45
            ? new[] { start, c1, c2, end + u * offset }
            : new[] { start, c1, c2, start + n * exit + u * offset, start + n * (exit + run), end };
        for (int i = 1; i < points.Length; i++)
            if (!Finite((points[i] - points[i - 1]).GetLength()) || (points[i] - points[i - 1]).GetLength() <= minimumLength)
                throw new InvalidOperationException("Mot doan bypass ngan hon chieu dai toi thieu cua Revit.");
        return points;
    }

    internal static double SectionReach(XYZ direction, XYZ sectionX, XYZ sectionY,
        double diameter, double width, double height)
    {
        XYZ d = direction.Normalize();
        double dx = d.DotProduct(sectionX.Normalize());
        double dy = d.DotProduct(sectionY.Normalize());
        return diameter > 0 ? diameter * 0.5 * Math.Sqrt(dx * dx + dy * dy)
            : (Math.Abs(dx) * width + Math.Abs(dy) * height) * 0.5;
    }

    // Clip an oriented bounding box to the transverse sweep corridor. Unlike a whole-beam
    // projection, this excludes remote beam ends; unlike a centreline hit, it includes the MEP envelope.
    // Corners use the bit order X=4, Y=2, Z=1. Clipping by two parallel planes needs only box edges.
    internal static bool BoxInCorridor(XYZ[] corners, XYZ origin, XYZ axis, XYZ offsetDirection,
        double halfWidth, out double entry, out double exit, out double low, out double high)
    {
        XYZ transverse = axis.CrossProduct(offsetDirection).Normalize();
        var clipped = new List<XYZ>();
        for (int i = 0; i < 8; i++)
        {
            double a = (corners[i] - origin).DotProduct(transverse);
            if (Math.Abs(a) <= halfWidth + Tolerance) clipped.Add(corners[i]);
            foreach (int bit in new[] { 1, 2, 4 })
            {
                int j = i ^ bit;
                if (j <= i) continue;
                double b = (corners[j] - origin).DotProduct(transverse);
                if (Math.Abs(b - a) <= Tolerance) continue;
                foreach (double boundary in new[] { -halfWidth, halfWidth })
                {
                    double t = (boundary - a) / (b - a);
                    if (t >= 0 && t <= 1) clipped.Add(corners[i] + (corners[j] - corners[i]) * t);
                }
            }
        }
        entry = low = double.MaxValue;
        exit = high = double.MinValue;
        foreach (XYZ p in clipped)
        {
            double along = (p - origin).DotProduct(axis);
            double across = (p - origin).DotProduct(offsetDirection);
            entry = Math.Min(entry, along); exit = Math.Max(exit, along);
            low = Math.Min(low, across); high = Math.Max(high, across);
        }
        return clipped.Count > 0;
    }

    internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static XYZ Unit(XYZ vector)
    {
        double length = vector?.GetLength() ?? 0;
        if (!Finite(length) || length <= 1e-12) throw new InvalidOperationException("Vector hinh hoc khong hop le.");
        return vector / length;
    }
}
