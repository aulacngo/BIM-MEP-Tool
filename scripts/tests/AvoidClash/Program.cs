using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIN;

internal static class Program
{
    private static int checks, routes, previews;
    private static readonly BypassShapeMode[] Modes = (BypassShapeMode[])Enum.GetValues(typeof(BypassShapeMode));
    private static readonly BypassDirection[] Directions = (BypassDirection[])Enum.GetValues(typeof(BypassDirection));

    [STAThread]
    private static void Main(string[] args)
    {
        Geometry();
        Preview(args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "AvoidClash-preview"));
        Console.WriteLine($"PASS: {checks} assertions; {routes} route cases; {previews} rendered mode/direction previews.");
        Console.WriteLine("Offline only: no Revit fitting, transaction, linked-model or hot-load execution.");
    }

    private static void Geometry()
    {
        XYZ start = new XYZ(0, 0, 0), end = new XYZ(20, 0, 0);
        XYZ[] z = AvoidClashGeometry.Build(start, end, -XYZ.BasisZ, 8, 12, 2, BypassShapeMode.Z45, 0.05);
        Equal(z[1], new XYZ(6, 0, 0)); Equal(z[2], new XYZ(8, 0, -2)); Equal(z[3], new XYZ(20, 0, -2));
        foreach (BypassShapeMode mode in Modes)
        {
            XYZ[] p = AvoidClashGeometry.Build(start, end, -XYZ.BasisZ, 8, 12, 2, mode, 0.05);
            Check(p.Length == (mode == BypassShapeMode.Z45 ? 4 : 6), "Elbow count");
            Equal(p[0], start); Equal(p.Last(), mode == BypassShapeMode.Z45 ? new XYZ(20, 0, -2) : end);
            CheckAngles(p, mode);
        }
        var random = new Random(20261003);
        for (int i = 0; i < 600; i++)
        {
            double azimuth = random.NextDouble() * Math.PI * 2;
            XYZ axis = new XYZ(Math.Cos(azimuth), Math.Sin(azimuth), 0);
            XYZ origin = new XYZ(random.NextDouble() * 5000, random.NextDouble() * 5000, random.NextDouble() * 50);
            double length = 20 + random.NextDouble() * 50, offset = 0.2 + random.NextDouble() * 3;
            foreach (BypassDirection direction in Directions)
            foreach (BypassShapeMode mode in Modes)
            {
                XYZ u = AvoidClashGeometry.OffsetDirection(axis, direction);
                XYZ[] p = AvoidClashGeometry.Build(origin, origin + axis * length, u, length * 0.4, length * 0.6, offset, mode, 0.05);
                CheckAngles(p, mode);
                Equal(p.Last() - p[0], axis * length + (mode == BypassShapeMode.Z45 ? u * offset : new XYZ(0, 0, 0)));
                for (int j = 1; j < p.Length; j++)
                {
                    Check((p[j] - origin).CrossProduct(axis).DotProduct(u) < 1e-6, "Route stays in bypass plane");
                    Check((p[j] - p[j - 1]).DotProduct(axis) >= -1e-8, "No reversed segment");
                }
                routes++;
            }
        }
        Equal(AvoidClashGeometry.OffsetDirection(XYZ.BasisX, BypassDirection.Left), XYZ.BasisY);
        Equal(AvoidClashGeometry.OffsetDirection(-XYZ.BasisX, BypassDirection.Left), -XYZ.BasisY);
        Reject(() => AvoidClashGeometry.OffsetDirection(XYZ.BasisZ, BypassDirection.Up));
        Reject(() => AvoidClashGeometry.OffsetDirection(new XYZ(1, 0, 0.1), BypassDirection.Down));
        Reject(() => AvoidClashGeometry.OffsetDirection(XYZ.BasisZ, BypassDirection.Left));
        Reject(() => AvoidClashGeometry.Build(start, end, XYZ.BasisX, 8, 12, 2, BypassShapeMode.U45, 0.05));
        Reject(() => AvoidClashGeometry.Build(start, end, XYZ.BasisZ, 1, 12, 2, BypassShapeMode.U45, 0.05));
        Reject(() => AvoidClashGeometry.Build(start, end, XYZ.BasisZ, 8, 19, 2, BypassShapeMode.U45, 0.05));
        Check(AvoidClashGeometry.Build(start, end, XYZ.BasisZ, 8, 19, 2, BypassShapeMode.Z45, 0.05).Length == 4, "Z needs no return run");
        Reject(() => AvoidClashGeometry.Build(start, end, XYZ.BasisZ, 8, 12, double.NaN, BypassShapeMode.Z45, 0.05));
        Reject(() => AvoidClashGeometry.Build(start, end, XYZ.BasisZ, 8, 12, 2, (BypassShapeMode)99, 0.05));
        Reject(() => AvoidClashGeometry.Build(new XYZ(double.NaN, 0, 0), end, XYZ.BasisZ, 8, 12, 2, BypassShapeMode.Z45, 0.05));
        Reject(() => AvoidClashGeometry.Build(start, start, XYZ.BasisZ, 8, 12, 2, BypassShapeMode.Z45, 0.05));
        XYZ slope = new XYZ(1, 0, 0.2).Normalize();
        CheckAngles(AvoidClashGeometry.Build(start, slope * 20, XYZ.BasisY, 8, 12, 2, BypassShapeMode.U45, 0.05), BypassShapeMode.U45);

        // Independent profile values: unrolled H/2, sideways W/2, and a rotated section.
        Near(AvoidClashGeometry.SectionReach(XYZ.BasisZ, XYZ.BasisY, XYZ.BasisZ, 0, 4, 2), 1);
        Near(AvoidClashGeometry.SectionReach(XYZ.BasisY, XYZ.BasisY, XYZ.BasisZ, 0, 4, 2), 2);
        XYZ x = (XYZ.BasisY + XYZ.BasisZ).Normalize(), y = (-XYZ.BasisY + XYZ.BasisZ).Normalize();
        Near(AvoidClashGeometry.SectionReach(XYZ.BasisZ, x, y, 0, 4, 2), 3 / Math.Sqrt(2));
        Near(AvoidClashGeometry.SectionReach(XYZ.BasisZ, x, y, 2, 0, 0), 1);
        Near(AvoidClashGeometry.SectionReach(XYZ.BasisX, x, y, 2, 0, 0), 0);

        // A long, oblique beam must be clipped to the MEP width corridor rather than projected in full.
        double c = 1 / Math.Sqrt(2);
        XYZ[] beam = Box(new XYZ(5, 0, 0), new XYZ(c, c, 0), new XYZ(-c, c, 0), XYZ.BasisZ, 100, 0.4, 1);
        Check(AvoidClashGeometry.BoxInCorridor(beam, start, XYZ.BasisX, XYZ.BasisZ, 0.25,
            out double entry, out double exit, out double low, out double high), "Oblique beam corridor");
        Near(entry, 5 - 0.25 - 0.4 * Math.Sqrt(2)); Near(exit, 5 + 0.25 + 0.4 * Math.Sqrt(2)); Near(low, -1); Near(high, 1);
        XYZ shift = new XYZ(200, -70, 15);
        Check(AvoidClashGeometry.BoxInCorridor(beam.Select(p => p + shift).ToArray(), shift, XYZ.BasisX, -XYZ.BasisZ, 0.25,
            out double shiftedEntry, out double shiftedExit, out double shiftedLow, out double shiftedHigh), "Transformed beam");
        Near(shiftedEntry, entry); Near(shiftedExit, exit); Near(shiftedLow, -high); Near(shiftedHigh, -low);
        XYZ[] far = Box(new XYZ(5, 3, 0), XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ, 1, 0.1, 1);
        Check(!AvoidClashGeometry.BoxInCorridor(far, start, XYZ.BasisX, XYZ.BasisZ, 0.25, out _, out _, out _, out _), "Reject remote obstacle");
        XYZ[] touching = Box(new XYZ(5, 0.3, 0), XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ, 1, 0.1, 1);
        Check(AvoidClashGeometry.BoxInCorridor(touching, start, XYZ.BasisX, XYZ.BasisZ, 0.25, out _, out _, out _, out _), "Section hits when centreline misses");
    }

    private static void Preview(string output)
    {
        Directory.CreateDirectory(output);
        new Application();
        var curve = new MEPCurve { Location = new LocationCurve { Curve = new Autodesk.Revit.DB.Line {
            Start = new XYZ(0, 0, 3), End = new XYZ(20, 0, 3) } } };
        curve.ConnectorManager.Connectors.Add(new Connector { Origin = new XYZ(0, 0, 3) });
        curve.ConnectorManager.Connectors.Add(new Connector { Origin = new XYZ(20, 0, 3), IsConnected = true });
        var window = new AvoidClashWindow(new UIDocument(), curve, new Element());
        Check((BypassShapeMode)Field(window, "_shapeMode") == BypassShapeMode.U45, "U45 default");
        foreach (BypassShapeMode mode in Modes)
        foreach (BypassDirection direction in Directions)
        {
            Call(window, "SelectShape", mode); Call(window, "SelectDirection", direction);
            var canvas = (Canvas)Field(window, "_canvas");
            var path = canvas.Children.OfType<System.Windows.Shapes.Path>().Single();
            PathFigure figure = ((PathGeometry)path.Data).Figures.Single();
            var p = new List<System.Windows.Point> { figure.StartPoint };
            p.AddRange(figure.Segments.Cast<System.Windows.Media.LineSegment>().Select(s => s.Point));
            Check(p.Count == (mode == BypassShapeMode.Z45 ? 4 : 6), "Preview co count");
            for (int i = 1; i < p.Count - 1; i++)
            {
                Vector a = p[i] - p[i - 1], b = p[i + 1] - p[i];
                Near(Math.Abs(Vector.AngleBetween(a, b)), mode == BypassShapeMode.U90 ? 90 : 45);
            }
            Near(p.Last().Y, mode == BypassShapeMode.Z45 ? p[2].Y : p[0].Y);
            Check(canvas.Children.OfType<System.Windows.Shapes.Ellipse>().Count() == p.Count - 2, "Preview elbow markers");
            var checkbox = (CheckBox)Field(window, "_allowDisconnect");
            Check(checkbox.Visibility == (mode == BypassShapeMode.Z45 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed), "Z disconnect control");
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(464, double.PositiveInfinity));
            content.Arrange(new Rect(new Size(464, content.DesiredSize.Height))); content.UpdateLayout();
            foreach (TextBlock text in Descendants(content).OfType<TextBlock>())
                Check(!text.Text.Contains('?'), "No corrupted UI text");
            var bitmap = new RenderTargetBitmap(464, (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(output, mode + "-" + direction + ".png"))) encoder.Save(stream);
            previews++;
        }
        var clearance = (System.Windows.Controls.TextBox)Field(window, "_txtClearance");
        foreach (string invalid in new[] { "NaN", "Infinity", "abc", "0", "-1" })
        {
            clearance.Text = invalid; int before = AvoidClashCmd.Calls; Call(window, "ApplyBypass");
            Check(AvoidClashCmd.Calls == before, "Reject invalid clearance before model call");
        }
        Call(window, "SelectShape", BypassShapeMode.Z45);
        ((CheckBox)Field(window, "_allowDisconnect")).IsChecked = true;
        clearance.Text = "50"; Call(window, "ApplyBypass");
        Check(AvoidClashCmd.LastMode == BypassShapeMode.Z45 && AvoidClashCmd.LastDisconnect, "Z UI dispatch");
        Call(window, "SelectShape", BypassShapeMode.U45); Call(window, "SelectShape", BypassShapeMode.Z45);
        Check(((CheckBox)Field(window, "_allowDisconnect")).IsChecked == false, "Reset stale disconnect selection");
        Call(window, "SelectDirection", BypassDirection.Down);
        var key = KeyArgs(Key.Space);
        Call(window, "AvoidClashWindow_PreviewKeyDown", window, key);
        Check((BypassDirection)Field(window, "_direction") == BypassDirection.Up && key.Handled, "SPACE toggles direction");
        int calls = AvoidClashCmd.Calls;
        Call(window, "AvoidClashWindow_PreviewKeyDown", window, KeyArgs(Key.Enter));
        Check(AvoidClashCmd.Calls == calls + 1, "ENTER applies");
        Call(window, "AvoidClashWindow_PreviewKeyDown", window, KeyArgs(Key.Escape));
        Console.WriteLine("UI previews: " + Path.GetFullPath(output));
    }

    private static KeyEventArgs KeyArgs(Key key) => new KeyEventArgs(Keyboard.PrimaryDevice, new DetachedPresentationSource(), 0, key)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent };

    private sealed class DetachedPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        { DependencyObject child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static XYZ[] Box(XYZ center, XYZ x, XYZ y, XYZ z, double hx, double hy, double hz)
    {
        var points = new XYZ[8];
        for (int i = 0; i < 8; i++) points[i] = center + x * ((i & 4) == 0 ? -hx : hx)
            + y * ((i & 2) == 0 ? -hy : hy) + z * ((i & 1) == 0 ? -hz : hz);
        return points;
    }
    private static void CheckAngles(XYZ[] points, BypassShapeMode mode)
    {
        for (int i = 1; i < points.Length - 1; i++)
        {
            XYZ a = (points[i] - points[i - 1]).Normalize(), b = (points[i + 1] - points[i]).Normalize();
            Near(Math.Acos(Math.Clamp(a.DotProduct(b), -1, 1)) * 180 / Math.PI, mode == BypassShapeMode.U90 ? 90 : 45);
        }
    }
    private static void Equal(XYZ a, XYZ b) => Near((a - b).GetLength(), 0);
    private static void Near(double a, double b) => Check(!double.IsNaN(a) && Math.Abs(a - b) < 1e-7, $"Expected {b:R}, got {a:R}");
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { checks++; return; } catch (ArgumentOutOfRangeException) { checks++; return; }
        throw new Exception("Invalid geometry was accepted.");
    }
}
