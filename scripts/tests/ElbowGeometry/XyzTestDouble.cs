// Only vector arithmetic for the offline geometry tests. This is NOT a Revit API fixture.
using System;

namespace Autodesk.Revit.DB;

public sealed class XYZ
{
    public readonly double X, Y, Z;
    public XYZ(double x, double y, double z) { X = x; Y = y; Z = z; }
    public static XYZ BasisX => new XYZ(1, 0, 0);
    public static XYZ BasisY => new XYZ(0, 1, 0);
    public static XYZ BasisZ => new XYZ(0, 0, 1);
    public double DotProduct(XYZ b) => X * b.X + Y * b.Y + Z * b.Z;
    public XYZ CrossProduct(XYZ b) => new XYZ(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double GetLength() => Math.Sqrt(DotProduct(this));
    public XYZ Normalize() => this / GetLength();
    public static XYZ operator +(XYZ a, XYZ b) => new XYZ(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static XYZ operator -(XYZ a, XYZ b) => new XYZ(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static XYZ operator -(XYZ a) => new XYZ(-a.X, -a.Y, -a.Z);
    public static XYZ operator *(XYZ a, double b) => new XYZ(a.X * b, a.Y * b, a.Z * b);
    public static XYZ operator /(XYZ a, double b) => a * (1 / b);
}
