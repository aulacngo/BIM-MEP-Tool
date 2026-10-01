using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class CheckConnectedPipeCmd : IExternalCommand
{
	private static CheckConnectedPipeWindow _view;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		if (_view == null || !_view.IsLoaded)
		{
			try
			{
				CheckConnectedPipeHandler handler = new CheckConnectedPipeHandler();
				ExternalEvent exEvent = ExternalEvent.Create((IExternalEventHandler)(object)handler);
				Document doc = commandData.Application.ActiveUIDocument.Document;
				List<string> resolvedIds = CheckConnectedPipeStorageUtil.LoadResolvedPipes(doc);
				_view = new CheckConnectedPipeWindow(commandData.Application, handler, exEvent, resolvedIds);
				WindowInteropHelper helper = new WindowInteropHelper(_view);
				try
				{
					helper.Owner = commandData.Application.MainWindowHandle;
				}
				catch
				{
				}
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
