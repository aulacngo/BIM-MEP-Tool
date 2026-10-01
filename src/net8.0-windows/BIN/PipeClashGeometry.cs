using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace BIN;

public enum PipeClearanceMode { Bare, Insulated, Construction }
public enum PipeClearanceStatus { Clash, Insufficient, Safe }

public sealed class PipeClashResult
{
    public ElementId FirstId { get; set; }
    public ElementId SecondId { get; set; }
    public string FirstName { get; set; }
    public string SecondName { get; set; }
    public XYZ FirstPoint { get; set; }
    public XYZ SecondPoint { get; set; }
    public double CenterDistanceMm { get; set; }
    public double GapMm { get; set; }
    public double RequiredMm { get; set; }
    public PipeClearanceStatus Status { get; set; }
    public string StatusText { get { return Status == PipeClearanceStatus.Clash ? "CLASH" : Status == PipeClearanceStatus.Insufficient ? "THIẾU HỞ" : "ĐẠT (SAFE)"; } }
    public string Detail { get { return Status == PipeClearanceStatus.Clash ? string.Format("Đâm xuyên {0:F1} mm", -GapMm) : Status == PipeClearanceStatus.Insufficient ? string.Format("Hở {0:F1} mm; thiếu {1:F1} mm", GapMm, RequiredMm - GapMm) : string.Format("Hở {0:F1} mm", GapMm); } }
}

/// <summary>Read-only analytic clearance for straight pipe center lines. Revit Pipe.Location is normally a Line.</summary>
public static class PipeClashGeometry
{
    private const double FtToMm = 304.8;

    public static IList<PipeClashResult> Check(Document doc, IList<Pipe> pipes, PipeClearanceMode mode, double requiredMm, Func<Pipe, double> fallbackInsulationMm)
    {
        var results = new List<PipeClashResult>();
        for (int i = 0; i < pipes.Count; i++)
            for (int j = i + 1; j < pipes.Count; j++)
                results.Add(CheckPair(doc, pipes[i], pipes[j], mode, requiredMm, fallbackInsulationMm));
        return results;
    }

    public static PipeClashResult CheckPair(Document doc, Pipe a, Pipe b, PipeClearanceMode mode, double requiredMm, Func<Pipe, double> fallbackInsulationMm)
    {
        var la = a.Location as LocationCurve;
        var lb = b.Location as LocationCurve;
        if (la == null || lb == null) throw new InvalidOperationException("Pipe khong co LocationCurve.");
        XYZ pa, pb;
        double centerDistance = ClosestDistance(la.Curve, lb.Curve, out pa, out pb);
        double ra = a.Diameter / 2.0;
        double rb = b.Diameter / 2.0;
        if (mode != PipeClearanceMode.Bare)
        {
            ra += GetInsulationThickness(doc, a, fallbackInsulationMm) / FtToMm;
            rb += GetInsulationThickness(doc, b, fallbackInsulationMm) / FtToMm;
        }
        double gap = (centerDistance - ra - rb) * FtToMm;
        var status = gap < 0 ? PipeClearanceStatus.Clash : (mode == PipeClearanceMode.Construction && gap < requiredMm ? PipeClearanceStatus.Insufficient : PipeClearanceStatus.Safe);
        return new PipeClashResult { FirstId = a.Id, SecondId = b.Id, FirstName = Label(a), SecondName = Label(b), FirstPoint = pa, SecondPoint = pb, CenterDistanceMm = Math.Round(centerDistance * FtToMm, 1), GapMm = Math.Round(gap, 1), RequiredMm = requiredMm, Status = status };
    }

    public static double GetInsulationThickness(Document doc, Pipe pipe, Func<Pipe, double> fallbackMm)
    {
        var insulation = new FilteredElementCollector(doc).OfClass(typeof(PipeInsulation)).Cast<PipeInsulation>()
            .FirstOrDefault(x => x.HostElementId == pipe.Id);
        return insulation != null ? insulation.Thickness * FtToMm : Math.Max(0, fallbackMm(pipe));
    }

    private static string Label(Pipe pipe) { return string.Format("#{0} [{1}] DN{2:F0}", pipe.Id, PipeInsulationRules.SystemName(pipe), pipe.Diameter * FtToMm); }

    // Robust closest-points calculation for two finite curves; tessellation supports arcs as well as Lines.
    private static double ClosestDistance(Curve a, Curve b, out XYZ bestA, out XYZ bestB)
    {
        var ap = a.Tessellate(); var bp = b.Tessellate();
        double best = double.MaxValue; bestA = ap[0]; bestB = bp[0];
        for (int i = 0; i < ap.Count - 1; i++) for (int j = 0; j < bp.Count - 1; j++)
        {
            XYZ x, y; double d = SegmentDistance(ap[i], ap[i + 1], bp[j], bp[j + 1], out x, out y);
            if (d < best) { best = d; bestA = x; bestB = y; }
        }
        return best;
    }

    private static double SegmentDistance(XYZ p1, XYZ q1, XYZ p2, XYZ q2, out XYZ c1, out XYZ c2)
    {
        XYZ d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        double a = d1.DotProduct(d1), e = d2.DotProduct(d2), f = d2.DotProduct(r), s, t;
        const double eps = 1e-12;
        if (a <= eps && e <= eps) { c1 = p1; c2 = p2; return c1.DistanceTo(c2); }
        if (a <= eps) { s = 0; t = Clamp(f / e); }
        else { double c = d1.DotProduct(r); if (e <= eps) { t = 0; s = Clamp(-c / a); }
        else { double b = d1.DotProduct(d2), denom = a * e - b * b; s = denom != 0 ? Clamp((b * f - c * e) / denom) : 0; t = (b * s + f) / e; if (t < 0) { t = 0; s = Clamp(-c / a); } else if (t > 1) { t = 1; s = Clamp((b - c) / a); } } }
        c1 = p1 + d1 * s; c2 = p2 + d2 * t; return c1.DistanceTo(c2);
    }
    private static double Clamp(double x) { return x < 0 ? 0 : (x > 1 ? 1 : x); }
}
