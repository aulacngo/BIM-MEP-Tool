// Test-only primitive vector. These tests do not load or simulate Revit's kernel.
using System;
namespace Autodesk.Revit.DB;
public sealed class XYZ
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }
    public XYZ(double x, double y, double z) { X = x; Y = y; Z = z; }
    public double DotProduct(XYZ other) => X * other.X + Y * other.Y + Z * other.Z;
    public double GetLength() => Math.Sqrt(DotProduct(this));
    public double DistanceTo(XYZ other) => (this - other).GetLength();
    public static XYZ operator +(XYZ a, XYZ b) => new XYZ(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static XYZ operator -(XYZ a, XYZ b) => new XYZ(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static XYZ operator -(XYZ a) => new XYZ(-a.X, -a.Y, -a.Z);
    public static XYZ operator *(XYZ a, double b) => new XYZ(a.X * b, a.Y * b, a.Z * b);
    public static XYZ operator *(double a, XYZ b) => b * a;
    public static XYZ operator /(XYZ a, double b) => a * (1 / b);
}
