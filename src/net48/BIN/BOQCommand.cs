using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class BOQCommand : IExternalCommand
{
	private static BOQWindow _ui;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			UIDocument uidoc = commandData.Application.ActiveUIDocument;
			Document doc = uidoc.Document;
			if (_ui != null)
			{
				_ui.Activate();
				return Result.Succeeded;
			}
			_ui = new BOQWindow(doc);
			WindowInteropHelper windowInteropHelper = new WindowInteropHelper(_ui);
			windowInteropHelper.Owner = commandData.Application.MainWindowHandle;
			_ui.Closed += delegate
			{
				_ui = null;
			};
			_ui.Show();
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}
}
