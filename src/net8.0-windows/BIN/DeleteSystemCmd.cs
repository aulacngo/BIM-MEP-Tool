using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class DeleteSystemCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			List<ElementId> selectedIds = uidoc.Selection.GetElementIds().ToList();
			if (selectedIds.Count == 0)
			{
				MepConnectableNoFabFilter filter = new MepConnectableNoFabFilter();
				Reference picked = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn Pipe hoặc Duct để xóa System");
				selectedIds.Add(picked.ElementId);
			}
			Transaction tr = new Transaction(doc, "Delete MEP System");
			try
			{
				tr.Start();
				NaviateHelper.DeleteSystems(doc, selectedIds);
				tr.Commit();
			}
			finally
			{
				((IDisposable)tr)?.Dispose();
			}
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
