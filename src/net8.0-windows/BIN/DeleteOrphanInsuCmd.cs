using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class DeleteOrphanInsuCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Expected O, but got Unknown
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Invalid comparison between Unknown and I4
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Invalid comparison between Unknown and I4
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Expected O, but got Unknown
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			TaskDialog taskDialog = new TaskDialog("Delete Orphan Insu");
			taskDialog.MainInstruction = "Chọn phạm vi để xóa Insulation không có vật chủ (Orphan Insulation):";
			taskDialog.AddCommandLink((TaskDialogCommandLinkId)1001, "Active View (Mặc định)");
			taskDialog.AddCommandLink((TaskDialogCommandLinkId)1002, "Toàn dự án (Entire Project)");
			taskDialog.CommonButtons = (TaskDialogCommonButtons)8;
			taskDialog.DefaultButton = (TaskDialogResult)1001;
			TaskDialogResult tResult = taskDialog.Show();
			bool entireProject = false;
			if ((int)tResult == 1001)
			{
				entireProject = false;
			}
			else
			{
				if ((int)tResult != 1002)
				{
					return Result.Cancelled;
				}
				entireProject = true;
			}
			List<ElementId> idsToDelete = new List<ElementId>();
			FilteredElementCollector pipeInsuCollector;
			FilteredElementCollector ductInsuCollector;
			if (entireProject)
			{
				pipeInsuCollector = new FilteredElementCollector(doc).OfClass(typeof(PipeInsulation));
				ductInsuCollector = new FilteredElementCollector(doc).OfClass(typeof(DuctInsulation));
			}
			else
			{
				pipeInsuCollector = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id).OfClass(typeof(PipeInsulation));
				ductInsuCollector = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id).OfClass(typeof(DuctInsulation));
			}
			foreach (InsulationLiningBase insu in ((IEnumerable)pipeInsuCollector).Cast<InsulationLiningBase>().Concat(((IEnumerable)ductInsuCollector).Cast<InsulationLiningBase>()))
			{
				if (insu.HostElementId == ElementId.InvalidElementId || doc.GetElement(insu.HostElementId) == null)
				{
					idsToDelete.Add(((Element)insu).Id);
				}
			}
			if (idsToDelete.Count == 0)
			{
				TaskDialog.Show("Delete Orphan Insu", "Không tìm thấy Insulation nào không có vật chủ trong phạm vi đã chọn.");
				return Result.Succeeded;
			}
			Transaction tr = new Transaction(doc, "Delete Orphan Insulation");
			try
			{
				tr.Start();
				doc.Delete((ICollection<ElementId>)idsToDelete);
				tr.Commit();
			}
			finally
			{
				((IDisposable)tr)?.Dispose();
			}
			TaskDialog.Show("Delete Orphan Insu", $"Đã xóa thành công {idsToDelete.Count} Orphan Insulation.");
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}
}
