using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BIN;

/// <summary>
/// Persistent smoke fixture for BIN Micro. Invoke through the existing
/// /connect-by-id endpoint with pipeId=-5. The fixture is intentionally kept
/// in the test model so the user can inspect and undo it manually.
/// </summary>
public static class MicroSmokeTest
{
    public static string Run(Document doc) => Run(doc, null);

    public static string Run(Document doc, UIDocument uidoc)
    {
        var results = new List<string>();
        var selected = new List<ElementId>();
        try
        {
            Level level = doc.ActiveView?.GenLevel ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x => x.Elevation).FirstOrDefault();
            PipeType pipeType = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>().FirstOrDefault();
            PipingSystemType pipeSystem = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().FirstOrDefault();
            if (level == null || pipeType == null || pipeSystem == null)
                return Json(results, "Không tìm thấy Level/PipeType/PipingSystemType để tạo fixture.");

            using (Transaction t = new Transaction(doc, "BIN Micro Smoke Fixture"))
            {
                t.Start();
                XYZ p0 = new XYZ(500, 500, level.Elevation + 10);
                Pipe pipeA = Pipe.Create(doc, pipeSystem.Id, pipeType.Id, level.Id, p0, p0 + new XYZ(10, 0, 0));
                Pipe pipeB = Pipe.Create(doc, pipeSystem.Id, pipeType.Id, level.Id, p0 + new XYZ(10, 0, 0), p0 + new XYZ(10, 10, 0));
                pipeA.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(0.5);
                pipeB.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(0.5);
                selected.Add(pipeA.Id); selected.Add(pipeB.Id);

                Duct ductA = CreateRoundDuct(doc, level, new XYZ(500, 530, level.Elevation + 10), new XYZ(510, 530, level.Elevation + 10));
                Duct ductB = CreateRoundDuct(doc, level, new XYZ(510, 530, level.Elevation + 10), new XYZ(510, 540, level.Elevation + 10));
                if (ductA != null) selected.Add(ductA.Id);
                if (ductB != null) selected.Add(ductB.Id);
                doc.Regenerate();

                Add(results, "Create_Pipe_Fixture", pipeA != null && pipeB != null);
                Add(results, "Create_Round_Duct_Fixture", ductA != null && ductB != null, ductA == null || ductB == null ? "Không tìm thấy DuctType tròn/SystemType phù hợp" : null);
                Add(results, "MepCurveFilter_Pipe_Duct", new MepCurveFilter().AllowElement(pipeA) && (ductA == null || new MepCurveFilter().AllowElement(ductA)));
                Add(results, "TrimFilter_Pipe_Duct", new Trim3DPipeFilter().AllowElement(pipeA) && (ductA == null || new Trim3DPipeFilter().AllowElement(ductA)));
                Add(results, "Connector_Pipe_Duct", HasConnectors(pipeA) && HasConnectors(pipeB) && (ductA == null || HasConnectors(ductA)) && (ductB == null || HasConnectors(ductB)));

                using (SubTransaction st = new SubTransaction(doc))
                {
                    st.Start();
                    try
                    {
                        ElementTransformUtils.RotateElements(doc, new List<ElementId> { pipeA.Id, pipeB.Id }, Line.CreateBound(p0, p0 + XYZ.BasisZ), Math.PI / 6.0);
                        if (ductA != null && ductB != null)
                            ElementTransformUtils.RotateElements(doc, new List<ElementId> { ductA.Id, ductB.Id }, Line.CreateBound(new XYZ(500, 530, level.Elevation), new XYZ(500, 530, level.Elevation + 1)), Math.PI / 6.0);
                        st.Commit();
                        Add(results, "Rotate_Pipe_Duct", true);
                    }
                    catch (Exception ex) { st.RollBack(); Add(results, "Rotate_Pipe_Duct", false, ex.Message); }
                }

                Add(results, "BreakCurve_Pipe", TryBreakPipe(doc, pipeA));
                Add(results, "BreakCurve_Duct", ductA == null ? false : TryBreakDuct(doc, ductA), ductA == null ? "Bỏ qua vì không tạo được Duct tròn" : null);

                if (ductA != null)
                {
                    try
                    {
                        MethodInfo m = typeof(Trim3DCmd).GetMethod("CreateMatchingSegment", BindingFlags.Instance | BindingFlags.NonPublic);
                        var trim = new Trim3DCmd();
                        Duct generated = m?.Invoke(trim, new object[] { doc, ductA, ductA, new XYZ(520, 530, level.Elevation + 10), new XYZ(525, 530, level.Elevation + 10) }) as Duct;
                        if (generated != null) selected.Add(generated.Id);
                        Add(results, "Trim3D_Create_Round_Duct", generated != null && generated.Diameter > 1E-06);
                    }
                    catch (Exception ex) { Add(results, "Trim3D_Create_Round_Duct", false, ex.InnerException?.Message ?? ex.Message); }
                }
                else Add(results, "Trim3D_Create_Round_Duct", false, "Không có fixture Duct tròn");

                doc.Regenerate();
                TryCreateElbow(doc, pipeA, pipeB, results, "Elbow_Fitting_Pipe");
                if (ductA != null && ductB != null) TryCreateElbow(doc, ductA, ductB, results, "Elbow_Fitting_Round_Duct");
                else Add(results, "Elbow_Fitting_Round_Duct", false, "Không có fixture Duct tròn");
                t.Commit();
            }

            if (uidoc != null && selected.Count > 0) uidoc.Selection.SetElementIds(selected.Distinct().ToList());
            return Json(results, "Fixture đã tạo. ID: " + string.Join(",", selected.Distinct().Select(FormatId)));
        }
        catch (Exception ex)
        {
            return Json(results, ex.Message);
        }
    }

    private static Duct CreateRoundDuct(Document doc, Level level, XYZ start, XYZ end)
    {
        var systems = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType)).Cast<MechanicalSystemType>().ToList();
        var types = new FilteredElementCollector(doc).OfClass(typeof(DuctType)).Cast<DuctType>().ToList();
        foreach (MechanicalSystemType system in systems)
            foreach (DuctType type in types)
            {
                try
                {
                    Duct d = Duct.Create(doc, ((Element)system).Id, ((Element)type).Id, level.Id, start, end);
                    d.get_Parameter((BuiltInParameter)(-1114103))?.Set(1.0);
                    doc.Regenerate();
                    if (d.Diameter > 1E-06) return d;
                    doc.Delete(d.Id);
                }
                catch { }
            }
        return null;
    }

    private static bool TryBreakPipe(Document doc, Pipe pipe)
    {
        try { PlumbingUtils.BreakCurve(doc, pipe.Id, Mid(pipe)); return true; } catch { return false; }
    }

    private static bool TryBreakDuct(Document doc, Duct duct)
    {
        try { MechanicalUtils.BreakCurve(doc, duct.Id, Mid(duct)); return true; } catch { return false; }
    }

    private static XYZ Mid(MEPCurve curve)
    {
        Line line = (curve.Location as LocationCurve)?.Curve as Line;
        return line == null ? XYZ.Zero : (line.GetEndPoint(0) + line.GetEndPoint(1)) / 2.0;
    }

    private static bool HasConnectors(MEPCurve curve) => curve?.ConnectorManager?.Connectors != null && curve.ConnectorManager.Connectors.Size >= 2;

    private static void TryCreateElbow(Document doc, MEPCurve a, MEPCurve b, List<string> results, string name)
    {
        try
        {
            Connector ca = a.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(b.ConnectorManager.Connectors.Cast<Connector>().First().Origin)).First();
            Connector cb = b.ConnectorManager.Connectors.Cast<Connector>().OrderBy(c => c.Origin.DistanceTo(ca.Origin)).First();
            doc.Create.NewElbowFitting(ca, cb);
            Add(results, name, true);
        }
        catch (Exception ex) { Add(results, name, false, ex.Message); }
    }

    private static void Add(List<string> results, string name, bool pass, string error = null)
    {
        results.Add($"{{\"test\":\"{McpExternalEventHandler.EscapeJson(name)}\",\"status\":\"{(pass ? "PASS" : "FAIL")}\"{(string.IsNullOrEmpty(error) ? "" : $",\"error\":\"{McpExternalEventHandler.EscapeJson(error)}\"")} }}".Replace("} }", "}}"));
    }

    private static string Json(List<string> results, string message)
    {
        int pass = results.Count(x => x.Contains("\"status\":\"PASS\""));
        int fail = results.Count - pass;
        return $"{{\"summary\":{{\"total\":{results.Count},\"passed\":{pass},\"failed\":{fail}}},\"message\":\"{McpExternalEventHandler.EscapeJson(message)}\",\"results\":[{string.Join(",", results)}]}}";
    }

    private static string FormatId(ElementId id)
    {
#if NET8_0_OR_GREATER
        return id.Value.ToString();
#else
        return id.IntegerValue.ToString();
#endif
    }
}
