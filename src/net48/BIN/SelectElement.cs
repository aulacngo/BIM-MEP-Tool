using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class SelectElement : IExternalCommand
{
	private static SelectElementWindow _ui;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			if (_ui != null)
			{
				_ui.Activate();
				return Result.Succeeded;
			}
			SelectElementEventHandler handler = new SelectElementEventHandler();
			ExternalEvent exEvent = ExternalEvent.Create((IExternalEventHandler)(object)handler);
			_ui = new SelectElementWindow(doc, uidoc, handler, exEvent);
			WindowInteropHelper helper = new WindowInteropHelper(_ui);
			helper.Owner = commandData.Application.MainWindowHandle;
			_ui.Closed += delegate
			{
				_ui = null;
			};
			_ui.Show();
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			TaskDialog.Show("BIM TOOL - Error", ex.ToString());
			return Result.Failed;
		}
	}
}
