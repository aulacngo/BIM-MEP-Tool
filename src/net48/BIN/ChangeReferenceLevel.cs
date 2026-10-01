using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ChangeReferenceLevel : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			UIDocument uidoc = commandData.Application.ActiveUIDocument;
			if (uidoc == null)
			{
				return Result.Cancelled;
			}
			MEPLevelChangerHandler handler = new MEPLevelChangerHandler();
			ExternalEvent exEvent = ExternalEvent.Create((IExternalEventHandler)(object)handler);
			MEPLevelChangerViewModel viewModel = new MEPLevelChangerViewModel(uidoc, exEvent, handler);
			MEPLevelChangerView view = new MEPLevelChangerView(viewModel);
			viewModel.SetView(view);
			view.ShowDialog();
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}
}
