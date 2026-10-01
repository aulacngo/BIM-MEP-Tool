using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public sealed class CheckPipeClashCmd : IExternalCommand
{
    public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
    {
        try
        {
            UIDocument ui = data.Application.ActiveUIDocument; Document doc = ui.Document;
            var pipes = ui.Selection.GetElementIds().Select(doc.GetElement).OfType<Pipe>().ToList();
            if (pipes.Count < 2) pipes = ui.Selection.PickObjects(ObjectType.Element, new PipeOnlyFilter(), "Chon 2 hoac 3 ong can kiem tra").Select(x => doc.GetElement(x)).OfType<Pipe>().Take(3).ToList();
if (pipes.Count < 2) { TaskDialog.Show("BIM TOOL - Check Clash", "Can chon it nhat 2 Pipe (toi da 3)." ); return Result.Cancelled; }
            if (pipes.Count > 3) pipes = pipes.Take(3).ToList();
            PipeClearanceMode mode = PipeClearanceMode.Construction; double required = 25;
            var w = new CheckPipeClashWindow((selectedMode, selectedRequired) => PipeClashGeometry.Check(doc, pipes, selectedMode, selectedRequired, p => PipeInsulationRules.GetThicknessMm(p)), mode, required); if (w.ShowDialog() != true) return Result.Cancelled;
            var result = PipeClashGeometry.Check(doc, pipes, w.Mode, w.RequiredMm, p => PipeInsulationRules.GetThicknessMm(p));
            // Reopen final result if mode/clearance changed and no action was selected.
            var selected = w.SelectedResult ?? result.OrderBy(x => x.GapMm).FirstOrDefault();
            if (selected == null) return Result.Cancelled;
            var ids = new List<ElementId> { selected.FirstId, selected.SecondId };
            if (w.ZoomRequested) { ui.Selection.SetElementIds(ids); ui.ShowElements(ids); }
            if (w.MarkerRequested) CreateMarker(doc, selected);
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
        catch (Exception ex) { message = ex.Message; return Result.Failed; }
    }
    private static void CreateMarker(Document doc, PipeClashResult r)
    {
        // Persistent marker is a tiny DirectShape, isolated in its own transaction and rolled back on every failure.
        using (var tx = new Transaction(doc, "BIN Clash clearance marker"))
        {
            try { tx.Start(); var point = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel)); point.ApplicationId = "BIN"; point.ApplicationDataId = "PipeClash:" + r.FirstId + ":" + r.SecondId; point.SetShape(new List<GeometryObject> { Point.Create(r.FirstPoint) }); tx.Commit(); }
            catch { if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack(); throw; }
        }
    }
    private sealed class PipeOnlyFilter : ISelectionFilter { public bool AllowElement(Element e) { return e is Pipe; } public bool AllowReference(Reference r, XYZ p) { return false; } }
}
