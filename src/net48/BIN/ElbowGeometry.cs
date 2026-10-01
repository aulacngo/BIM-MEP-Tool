using System;
using Autodesk.Revit.DB;

namespace BIN;

/// <summary>Direction and roll planning without document mutation. Lengths are internal feet.</summary>
internal static class ElbowGeometry
{
    // Only the singular global-Z projection uses the connector-local convention.
    internal const double VerticalTolerance = 1e-8;

    internal static XYZ Direction(XYZ sourceAxis, XYZ sourceX, XYZ sourceY, ElbowDirection direction)
    {
        XYZ n = Unit(sourceAxis);
        // Equivalent to Z - dot(Z,n)*n, but avoids cancellation of the small
        // positive Z component near vertical (1 - n.Z*n.Z can round to zero).
        XYZ up = n.CrossProduct(XYZ.BasisZ.CrossProduct(n));
        XYZ left;
        if (up.GetLength() > VerticalTolerance)
        {
            up = Unit(up);
            left = Unit(up.CrossProduct(n));
        }
        else
        {
            up = Unit(sourceY - n * sourceY.DotProduct(n));
            left = Unit(sourceX - n * sourceX.DotProduct(n));
            if (Math.Abs(up.DotProduct(left)) > 1e-6)
                throw new InvalidOperationException("Invalid source connector section frame.");
        }

        XYZ turn;
        switch (direction)
        {
            case ElbowDirection.Up:
            case ElbowDirection.Up45: turn = up; break;
            case ElbowDirection.Down:
            case ElbowDirection.Down45: turn = -up; break;
            case ElbowDirection.Left:
            case ElbowDirection.Left45: turn = left; break;
            case ElbowDirection.Right:
            case ElbowDirection.Right45: turn = -left; break;
            default: throw new ArgumentOutOfRangeException(nameof(direction));
        }
        return Is45(direction) ? Unit(n + turn) : turn;
    }

    internal static bool Is45(ElbowDirection direction) =>
        direction == ElbowDirection.Up45 || direction == ElbowDirection.Down45 ||
        direction == ElbowDirection.Left45 || direction == ElbowDirection.Right45;

    // Parallel-transport each section axis through the bend, preserving W/H and source roll.
    internal static XYZ Transport(XYZ sectionAxis, XYZ sourceAxis, XYZ targetAxis)
    {
        XYZ n = Unit(sourceAxis);
        XYZ d = Unit(targetAxis);
        XYZ normal = Unit(n.CrossProduct(d));
        double angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, n.DotProduct(d))));
        return Unit(sectionAxis * Math.Cos(angle) + normal.CrossProduct(sectionAxis) * Math.Sin(angle)
            + normal * (normal.DotProduct(sectionAxis) * (1.0 - Math.Cos(angle))));
    }

    internal static double RollAngle(XYZ currentX, XYZ desiredX, XYZ axis)
    {
        XYZ n = Unit(axis);
        XYZ a = Unit(currentX - n * currentX.DotProduct(n));
        XYZ b = Unit(desiredX - n * desiredX.DotProduct(n));
        // Section axes are lines, so a half turn describes the same rectangular/oval profile.
        double angle = Math.Atan2(n.DotProduct(a.CrossProduct(b)), a.DotProduct(b));
        if (angle > Math.PI / 2) angle -= Math.PI;
        if (angle < -Math.PI / 2) angle += Math.PI;
        return angle;
    }

    private static XYZ Unit(XYZ vector)
    {
        double length = vector.GetLength();
        if (double.IsNaN(length) || double.IsInfinity(length) || length < 1e-12)
            throw new InvalidOperationException("Cannot construct an elbow from a degenerate connector frame.");
        return vector / length;
    }
}
