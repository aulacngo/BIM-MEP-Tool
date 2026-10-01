using System;
using Autodesk.Revit.DB;
using BIN;

internal static class Program
{
    private static int assertions;
    private static int frames;
    private static readonly ElbowDirection[] Directions = (ElbowDirection[])Enum.GetValues(typeof(ElbowDirection));

    private static void Main()
    {
        // Independent known answers catch swapped directions and the historical vertical duplicate.
        Equal(ElbowGeometry.Direction(XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ, ElbowDirection.Up), XYZ.BasisZ);
        Equal(ElbowGeometry.Direction(XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ, ElbowDirection.Left), XYZ.BasisY);
        Equal(ElbowGeometry.Direction(XYZ.BasisY, -XYZ.BasisX, XYZ.BasisZ, ElbowDirection.Left), -XYZ.BasisX);
        foreach (double sign in new[] { -1.0, 1.0 })
        {
            XYZ n = XYZ.BasisZ * sign;
            XYZ x = XYZ.BasisX;
            XYZ y = n.CrossProduct(x);
            Equal(ElbowGeometry.Direction(n, x, y, ElbowDirection.Up), y);
            Equal(ElbowGeometry.Direction(n, x, y, ElbowDirection.Down), -y);
            Equal(ElbowGeometry.Direction(n, x, y, ElbowDirection.Left), x);
            Equal(ElbowGeometry.Direction(n, x, y, ElbowDirection.Right), -x);
        }
        XYZ slope = new XYZ(Math.Sqrt(3) / 2, 0, 0.5);
        Equal(ElbowGeometry.Direction(slope, XYZ.BasisY, new XYZ(-0.5, 0, Math.Sqrt(3) / 2), ElbowDirection.Up),
            new XYZ(-0.5, 0, Math.Sqrt(3) / 2));

        XYZ[] axes = { XYZ.BasisX, -XYZ.BasisX, XYZ.BasisY, -XYZ.BasisY, XYZ.BasisZ, -XYZ.BasisZ,
            slope, -slope, new XYZ(1, 1, 0).Normalize(), new XYZ(1, -2, 3).Normalize() };
        foreach (XYZ n in axes)
            foreach (double roll in new[] { 0.0, Math.PI / 6, Math.PI / 2, -Math.PI * 0.7 })
                CheckFrame(n, roll);

        // Both sides of the singularity threshold, both poles, and multiple azimuths.
        // Continuity through the pole is not asserted: global Up has no unique limit there.
        foreach (double horizontal in new[] { 0.0, 1e-10, 0.5e-8, 0.999e-8, 1.001e-8, 2e-8, 1e-5 })
            foreach (double sign in new[] { -1.0, 1.0 })
                foreach (double azimuth in new[] { 0.0, 0.7, 2.5 })
                    CheckFrame(new XYZ(horizontal * Math.Cos(azimuth), horizontal * Math.Sin(azimuth), sign).Normalize(), 0.37);

        var random = new Random(20260926);
        for (int i = 0; i < 2000; i++)
        {
            XYZ n = new XYZ(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5).Normalize();
            CheckFrame(n, random.NextDouble() * 2 * Math.PI);
        }
        Reject(() => ElbowGeometry.Direction(new XYZ(0, 0, 0), XYZ.BasisX, XYZ.BasisY, ElbowDirection.Up));
        Reject(() => ElbowGeometry.Direction(XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ, (ElbowDirection)999));
        Console.WriteLine($"PASS: {frames} source frames x 8 directions; {assertions} assertions. Actual ElbowGeometry.cs with an XYZ arithmetic test double; no Revit model/API execution.");
    }

    private static void CheckFrame(XYZ n, double roll)
    {
        XYZ reference = Math.Abs(n.Z) < 0.9 ? XYZ.BasisZ : XYZ.BasisX;
        XYZ x0 = reference.CrossProduct(n).Normalize();
        XYZ y0 = n.CrossProduct(x0);
        XYZ x = x0 * Math.Cos(roll) + y0 * Math.Sin(roll);
        XYZ y = n.CrossProduct(x);
        frames++;
        foreach (ElbowDirection direction in Directions)
        {
            XYZ d = ElbowGeometry.Direction(n, x, y, direction);
            Near(d.GetLength(), 1);
            double cosine = ElbowGeometry.Is45(direction) ? Math.Sqrt(0.5) : 0;
            Near(n.DotProduct(d), cosine);
            XYZ tx = ElbowGeometry.Transport(x, n, d);
            XYZ ty = ElbowGeometry.Transport(y, n, d);
            Near(tx.GetLength(), 1);
            Near(ty.GetLength(), 1);
            Near(tx.DotProduct(d), 0);
            Near(ty.DotProduct(d), 0);
            Near(tx.DotProduct(ty), 0);
            Equal(tx.CrossProduct(ty), d);

            // Rotating to the target roll must align the section axes, even when the
            // near connector's normal is -d and its BasisX has the opposite sign.
            foreach (double offset in new[] { -2.9, -0.4, 0.0, Math.PI / 2, 2.8 })
            {
                XYZ current = tx * Math.Cos(offset) + ty * Math.Sin(offset);
                double correction = ElbowGeometry.RollAngle(current, tx, d);
                XYZ rotated = current * Math.Cos(correction) + d.CrossProduct(current) * Math.Sin(correction);
                Near(Math.Abs(rotated.DotProduct(tx)), 1);
                Check(Math.Abs(correction) <= Math.PI / 2 + 1e-10, "Roll exceeds a quarter turn");
            }
        }
        Pair(n, x, y, ElbowDirection.Up, ElbowDirection.Down, false);
        Pair(n, x, y, ElbowDirection.Left, ElbowDirection.Right, false);
        Pair(n, x, y, ElbowDirection.Up45, ElbowDirection.Down45, true);
        Pair(n, x, y, ElbowDirection.Left45, ElbowDirection.Right45, true);
        if (new XYZ(n.X, n.Y, 0).GetLength() > ElbowGeometry.VerticalTolerance)
        {
            XYZ up = ElbowGeometry.Direction(n, x, y, ElbowDirection.Up);
            XYZ left = ElbowGeometry.Direction(n, x, y, ElbowDirection.Left);
            Check(up.Z > 0, "Up must rise for nonvertical sources");
            Near(left.Z, 0);
            Equal(left, XYZ.BasisZ.CrossProduct(n).Normalize());
        }
    }

    private static void Pair(XYZ n, XYZ x, XYZ y, ElbowDirection a, ElbowDirection b, bool diagonal)
    {
        XYZ da = ElbowGeometry.Direction(n, x, y, a);
        XYZ db = ElbowGeometry.Direction(n, x, y, b);
        Equal(da + db, diagonal ? n * Math.Sqrt(2) : new XYZ(0, 0, 0));
        Near(da.DotProduct(db), diagonal ? 0 : -1);
    }

    private static void Equal(XYZ actual, XYZ expected) => Near((actual - expected).GetLength(), 0);
    private static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < 1e-7, $"Expected {expected:R}; got {actual:R}");
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentException) { assertions++; return; }
        catch (InvalidOperationException) { assertions++; return; }
        throw new Exception("Invalid geometry was accepted");
    }
}
