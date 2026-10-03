using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using BIN;

internal static class Program
{
    private static int _checks;
    [STAThread]
    private static int Main()
    {
        try
        {
            Geometry(); Route(); Ui(); Artifacts();
            Console.WriteLine("PASS " + _checks + " assertions · analytic geometry + WPF only; Revit kernel/model/transactions untested.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Artifacts()
    {
        foreach (string file in new[] { "FlexDuctAvoidMepCmd.cs", "AvoidMepWindow.cs", "AvoidMep_ObstacleFilter.cs",
            "AvoidMep_FlexDuctFilter.cs", "FlexAvoidMath.cs", "FlexAvoidGeometry.cs" })
            Check(File.ReadAllBytes("src/net48/BIN/" + file).SequenceEqual(File.ReadAllBytes("src/net8.0-windows/BIN/" + file)),
                "Byte-for-byte target parity: " + file);
        foreach (string target in new[] { "net48", "net8.0-windows" })
        {
            using var stream = File.OpenRead("src/" + target + "/bin/Release/" + target + "/BIN.dll");
            using var pe = new PEReader(stream);
            MetadataReader metadata = pe.GetMetadataReader();
            Check(!metadata.ManifestResources.Select(h => metadata.GetString(metadata.GetManifestResource(h).Name))
                .Any(name => name.Contains("avoidmepwindow", StringComparison.OrdinalIgnoreCase)), "Obsolete BAML absent in " + target + " assembly");
        }
    }
    private static FlexAvoidMath.Cubic Line(XYZ a, XYZ b)
        => new FlexAvoidMath.Cubic(a, a + (b - a) / 3, a + (b - a) * (2.0 / 3), b);
    private static FlexAvoidMath.PointDistance Box(XYZ p, XYZ center, XYZ half)
    {
        XYZ q = p - center;
        double x = Math.Abs(q.X) - half.X, y = Math.Abs(q.Y) - half.Y, z = Math.Abs(q.Z) - half.Z;
        double outside = Math.Sqrt(Math.Pow(Math.Max(x, 0), 2) + Math.Pow(Math.Max(y, 0), 2) + Math.Pow(Math.Max(z, 0), 2));
        double distance = outside + Math.Min(0, Math.Max(x, Math.Max(y, z)));
        XYZ surface = new XYZ(Math.Clamp(q.X, -half.X, half.X), Math.Clamp(q.Y, -half.Y, half.Y), Math.Clamp(q.Z, -half.Z, half.Z));
        if (distance < 0)
        {
            if (x >= y && x >= z) surface = new XYZ(q.X < 0 ? -half.X : half.X, q.Y, q.Z);
            else if (y >= z) surface = new XYZ(q.X, q.Y < 0 ? -half.Y : half.Y, q.Z);
            else surface = new XYZ(q.X, q.Y, q.Z < 0 ? -half.Z : half.Z);
        }
        return new FlexAvoidMath.PointDistance { SignedDistance = distance, SurfacePoint = surface + center };
    }
    private static FlexAvoidMath.PointDistance IBeam(XYZ p)
        => new[] { Box(p, new XYZ(0, 0, -0.9), new XYZ(0.4, 50, 0.1)),
            Box(p, new XYZ(0, 0, 0.9), new XYZ(0.4, 50, 0.1)),
            Box(p, new XYZ(0, 0, 0), new XYZ(0.06, 50, 0.8)) }.OrderBy(d => d.SignedDistance).First();
    private static void Bounds(FlexAvoidMath.Check check, double exact)
    {
        Check(check.LowerBound <= exact + 1e-10, "Lower bound contains exact minimum");
        Check(check.Minimum >= exact - 1e-10, "Sample upper bound contains exact minimum");
        Check(check.Uncertainty <= FlexAvoidMath.DistanceTolerance + 1e-10, "0.5 mm distance uncertainty");
    }
    private static void Geometry()
    {
        var below = Line(new XYZ(-3, 0, -1.25), new XYZ(3, 0, -1.25));
        var check = FlexAvoidMath.Measure(new[] { below }, IBeam);
        Bounds(check, 0.25);
        Check(check.LowerBound > 0.05 + 50 * FlexAvoidMath.Mm, "Bare pipe with 50 mm clearance passes");
        Check(check.Minimum < 0.05 + 0.25, "Insulation-only clash is detected");
        var inside = FlexAvoidMath.Measure(new[] { Line(new XYZ(-3, 0, 0), new XYZ(3, 0, 0)) }, IBeam);
        Bounds(inside, -0.06);
        Check(inside.Minimum < 0, "Centreline inside web must be negative, not clear");
        // A narrow solid between ordinary uniform sample positions.
        var thin = FlexAvoidMath.Measure(new[] { Line(new XYZ(-3, 0, 0), new XYZ(3, 0, 0)) },
            p => Box(p, new XYZ(0.231, 0, 0), new XYZ(0.004, 2, 2)));
        Bounds(thin, -0.004);
        Check(thin.Minimum < 0, "Thin flange between samples is detected");
        var curved = new FlexAvoidMath.Cubic(new XYZ(-3, 0, 2), new XYZ(-1, 0, -4), new XYZ(1, 0, -4), new XYZ(3, 0, 2));
        var curvedClash = FlexAvoidMath.Measure(new[] { curved },
            p => Box(p, new XYZ(-1.5, 0, -1.375), new XYZ(0.10, 0.10, 0.10)));
        Check(curvedClash.Minimum < 0, "Actual cubic collides although endpoint chord is clear");
        var chord = FlexAvoidMath.Measure(new[] { Line(curved.A, curved.D) },
            p => Box(p, new XYZ(-1.5, 0, -1.375), new XYZ(0.10, 0.10, 0.10)));
        Check(chord.LowerBound > 3, "Chord control case is clear");
        foreach (double offset in new[] { -8.0, -0.31, 0.7, 13.0 })
        {
            XYZ shift = new XYZ(100 + offset, -27, 12);
            var shifted = Line(below.A + shift, below.D + shift);
            Bounds(FlexAvoidMath.Measure(new[] { shifted }, p => IBeam(p - shift)), 0.25);
        }
        Check(FlexAvoidMath.Measure(new[] { Line(new XYZ(0, 0, -2), new XYZ(0.01, 0, -2)) }, IBeam).LowerBound > 0.9,
            "Small clear curve works");
        Reject(() => FlexAvoidMath.Measure(new FlexAvoidMath.Cubic[0], IBeam));
        Reject(() => FlexAvoidMath.Measure(new[] { below }, p => new FlexAvoidMath.PointDistance { SignedDistance = double.NaN, SurfacePoint = p }));
        var sphere = FlexAvoidMath.Measure(new[] { Line(new XYZ(-1, 2, 0), new XYZ(1, 2, 0)) }, p =>
        {
            double length = p.GetLength();
            return new FlexAvoidMath.PointDistance { SignedDistance = length - 1, SurfacePoint = p / length };
        });
        Bounds(sphere, 1);
    }
    private static void Route()
    {
        // Long diagonal beam, triangular surface clipping, and transformed coordinates.
        double s = 1 / Math.Sqrt(2);
        XYZ Rotate(XYZ p) => new XYZ(s * p.X - s * p.Y, s * p.X + s * p.Y, p.Z);
        var triangles = BoxTriangles(new XYZ(0, 0, 0), new XYZ(0.4, 50, 1)).Select(t => t.Select(Rotate).ToArray()).ToList();
        var route = FlexAvoidMath.Plan(new XYZ(-5, 0, 0), new XYZ(5, 0, 0), triangles, 0.3);
        Check(route.Exit - route.Entry < 2, "Oblique beam clipped to corridor instead of full bounding box");
        Near(route.Low, -1); Near(route.High, 1);
        var down = route.Points(false, 0); var up = route.Points(true, 0);
        Near(down.First().DistanceTo(route.Start), 0); Near(down.Last().DistanceTo(route.End), 0);
        Near(down[3].Z, -1.3); Near(up[3].Z, 1.3);
        Check(down.Zip(down.Skip(1), (a, b) => a.DistanceTo(b)).All(d => d > 1e-5), "No duplicate route points");
        XYZ shift = new XYZ(44, -22, 11);
        var shifted = FlexAvoidMath.Plan(route.Start + shift, route.End + shift,
            triangles.Select(t => t.Select(p => p + shift).ToArray()), 0.3);
        Near(shifted.Entry, route.Entry); Near(shifted.Exit, route.Exit); Near(shifted.Low, 10);
        Near(shifted.Points(false, 0)[3].DistanceTo(down[3] + shift), 0);
        Reject(() => FlexAvoidMath.Plan(new XYZ(0, 0, -4), new XYZ(0, 0, 4), triangles, 0.3));
        Reject(() => FlexAvoidMath.Plan(new XYZ(-0.5, 0, 0), new XYZ(0.5, 0, 0), triangles, 0.3).Points(false, 0));
        Reject(() => FlexAvoidMath.Plan(route.Start, route.End, triangles, double.NaN));
        Reject(() => FlexAvoidMath.Plan(route.Start, route.End, BoxTriangles(new XYZ(0, 20, 0), new XYZ(1, 1, 1)), 0.3));
        var floor = FlexAvoidMath.Plan(new XYZ(-5, 0, -2), new XYZ(5, 0, -2),
            BoxTriangles(new XYZ(0, 0, 0), new XYZ(50, 50, 1)), 0.3);
        var floorPoints = floor.Points(false, 0.01);
        Near(floorPoints.First().DistanceTo(floor.Start), 0); Near(floorPoints.Last().DistanceTo(floor.End), 0);
        Near(floorPoints[2].Z, -1.31);
        Reject(() => floor.Points(true, 0));
    }
    private static IEnumerable<XYZ[]> BoxTriangles(XYZ center, XYZ half)
    {
        var p = new XYZ[8];
        for (int i = 0; i < 8; i++) p[i] = center + new XYZ((i & 4) == 0 ? -half.X : half.X,
            (i & 2) == 0 ? -half.Y : half.Y, (i & 1) == 0 ? -half.Z : half.Z);
        int[][] faces = { new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 }, new[] { 0, 4, 5, 1 },
            new[] { 2, 3, 7, 6 }, new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 } };
        foreach (int[] f in faces) { yield return new[] { p[f[0]], p[f[1]], p[f[2]] }; yield return new[] { p[f[0]], p[f[2]], p[f[3]] }; }
    }
    private static void Ui()
    {
        foreach (string culture in new[] { "vi-VN", "en-US" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (string invalid in new[] { "", "-1", "NaN", "Infinity", "1e99", "5001", "abc" })
                Check(!AvoidMepWindow.TryValue(invalid, 5000, out _), "Reject invalid input: " + invalid);
            foreach (string valid in new[] { "0", "50", "50.5", "50,5", "5e1" })
                Check(AvoidMepWindow.TryValue(valid, 5000, out _), "Accept valid finite input: " + valid);
        }
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        int calls = 0; bool lastAuto = false; FlexAvoidSettings lastSettings = null;
        var window = new AvoidMepWindow("FLEX DUCT", "LINK · Structural Framing · Dầm I", 150, 25, "Type · test fixture", (settings, auto) =>
        {
            calls++; lastAuto = auto; lastSettings = settings;
            return new FlexAvoidReport { Text = "TEST FIXTURE · VA CHẠM\nKhoảng hở vỏ ngoài: -12,0 mm\nĐộ ăn sâu: 12,0 mm\nSai số khoảng cách ≤ 0,5 mm", Warning = true };
        });
        var clearance = (TextBox)Field(window, "_clearance");
        var insulation = (TextBox)Field(window, "_insulation");
        var direction = (ComboBox)Field(window, "_direction");
        Check(clearance.Text == "50" && insulation.Text == "25" && direction.SelectedIndex == 0, "Default 50 mm, detected insulation and Down");
        Call(window, "Run", false);
        Check(calls == 1 && !lastAuto && lastSettings.InsulationMm == 25, "CHECK dispatch uses detected insulation");
        clearance.Text = "NaN"; Call(window, "Run", true);
        Check(calls == 1 && window.LastError != null, "Invalid input rejected before model callback");
        clearance.Text = "50,5"; insulation.Text = "30"; direction.SelectedIndex = 1;
        Call(window, "Run", true);
        Check(calls == 2 && lastAuto && lastSettings.Up && lastSettings.ClearanceMm == 50.5 && lastSettings.InsulationMm == 30,
            "AUTO dispatch uses override and Up");
        Check(window.HasExecuted && window.LastError == null, "UI records successful callback");
        var key = KeyArgs(Key.Enter); window.RaiseEvent(key);
        Check(calls == 3 && lastAuto && key.Handled, "ENTER dispatches AUTO-AVOID");
        var output = Path.GetFullPath("scratch/flex-avoid-qa"); Directory.CreateDirectory(output);
        Render(window, Path.Combine(output, "up-check.png"));
        direction.SelectedIndex = 0;
        Check(((TextBlock)Field(window, "_status")).Text.Contains("Thông số đã thay đổi"), "Changed settings invalidate stale result");
        Call(window, "Run", false); Render(window, Path.Combine(output, "down-check.png"));
        bool closed = false; window.Closed += (s, e) => closed = true;
        key = KeyArgs(Key.Escape); window.RaiseEvent(key);
        Check(closed && key.Handled, "ESC closes dialog");
        var failure = new AvoidMepWindow("FLEX PIPE", "HOST · Floor", 25, 0, "test", (settings, auto) => throw new InvalidOperationException("rollback fixture"));
        Call(failure, "Run", true);
        Check(!failure.HasExecuted && failure.LastError == "rollback fixture", "Failure does not report success");
        Check(((Button)Field(failure, "_avoid")).IsEnabled, "Buttons restored after rollback error");
        failure.Close();
        Console.WriteLine("UI previews: " + output);
    }
    private static KeyEventArgs KeyArgs(Key key) => new KeyEventArgs(Keyboard.PrimaryDevice, new DetachedPresentationSource(), 0, key)
        { RoutedEvent = Keyboard.PreviewKeyDownEvent };
    private sealed class DetachedPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null;
    }
    private static void Render(Window window, string path)
    {
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(594, 1100)); root.Arrange(new Rect(0, 0, 594, root.DesiredSize.Height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(594, (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Near(double a, double b) => Check(Math.Abs(a - b) < 1e-8, $"Expected {b}, got {a}");
    private static void Check(bool value, string message) { _checks++; if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { _checks++; return; }
        throw new Exception("Unsafe/unknown geometry accepted");
    }
}
