// Offline arithmetic/UI substitutes. This fixture never loads Revit or exercises its fitting engine.
using System;
using System.Collections.Generic;

namespace Autodesk.Revit.DB
{
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
        public double DistanceTo(XYZ b) => (this - b).GetLength();
        public static XYZ operator +(XYZ a, XYZ b) => new XYZ(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static XYZ operator -(XYZ a, XYZ b) => new XYZ(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static XYZ operator -(XYZ a) => new XYZ(-a.X, -a.Y, -a.Z);
        public static XYZ operator *(XYZ a, double b) => new XYZ(a.X * b, a.Y * b, a.Z * b);
        public static XYZ operator /(XYZ a, double b) => a * (1 / b);
    }
    public class Document { }
    public class Category { public string Name => "Beam"; }
    public class Element { public Category Category => new Category(); public string Name => "Offline fixture"; public object Location { get; set; } }
    public class MEPCurve : Element { public ConnectorManager ConnectorManager { get; } = new ConnectorManager(); }
    public class ConnectorManager { public List<Connector> Connectors { get; } = new List<Connector>(); }
    public enum ConnectorType { End, Logical }
    public class Connector { public ConnectorType ConnectorType => ConnectorType.End; public XYZ Origin { get; set; } public bool IsConnected { get; set; } }
    public class LocationCurve { public object Curve { get; set; } }
    public class Line { public XYZ Start, End; public XYZ GetEndPoint(int i) => i == 0 ? Start : End; }
    public class Transform { public static Transform Identity => new Transform(); }
}
namespace Autodesk.Revit.UI
{
    public class UIDocument { public Autodesk.Revit.DB.Document Document => new Autodesk.Revit.DB.Document(); }
}
namespace BIN
{
    internal static class AvoidClashCmd
    {
        internal static int Calls;
        internal static BypassShapeMode LastMode;
        internal static bool LastDisconnect;
        public static bool ExecuteBypass(Autodesk.Revit.DB.Document doc, Autodesk.Revit.DB.Element curve,
            Autodesk.Revit.DB.Element obstacle, Autodesk.Revit.DB.Transform transform, BypassShapeMode mode,
            BypassDirection direction, double clearance, out string error, bool disconnectZEnd = false)
        { Calls++; LastMode = mode; LastDisconnect = disconnectZEnd; error = "Offline UI fixture: model operation not executed."; return false; }
    }
}
