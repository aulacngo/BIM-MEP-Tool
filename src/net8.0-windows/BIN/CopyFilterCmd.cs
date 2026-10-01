using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class CopyFilterCmd : IExternalCommand
{
	private static CopyFilterWindow _view;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		if (_view == null || !_view.IsLoaded)
		{
			try
			{
				CopyFilterHandler handler = new CopyFilterHandler();
				ExternalEvent exEvent = ExternalEvent.Create((IExternalEventHandler)(object)handler);
				_view = new CopyFilterWindow(commandData.Application, handler, exEvent);
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
