using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ClashDetectiveCmd : IExternalCommand
{
	private static ClashDetectiveWindow _view;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		if (_view == null || !_view.IsLoaded)
		{
			try
			{
				Document doc = commandData.Application.ActiveUIDocument.Document;
				List<string> resolvedClashes = ClashStorageUtil.LoadResolvedClashes(doc);
				ClashActionHandler handler = new ClashActionHandler();
				ExternalEvent exEvent = ExternalEvent.Create((IExternalEventHandler)(object)handler);
				_view = new ClashDetectiveWindow(commandData.Application, handler, exEvent, resolvedClashes);
				_view.Closed += delegate
				{
					_view = null;
				};
				_view.Show();
			}
			catch (Exception ex)
			{
				message = ex.Message;
				return Result.Failed;
			}
		}
		else
		{
			_view.Activate();
		}
		return Result.Succeeded;
	}
}
