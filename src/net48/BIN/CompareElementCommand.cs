using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class CompareElementCommand : IExternalCommand
{
	public static CompareElementWindow instance;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		CompareElementEventHandler handler = new CompareElementEventHandler();
		ExternalEvent exEvent = ExternalEvent.Create((IExternalEventHandler)(object)handler);
		if (instance == null || !instance.IsLoaded)
		{
			instance = new CompareElementWindow(commandData.Application.ActiveUIDocument, handler, exEvent);
			instance.Show();
		}
		else
		{
			instance.Activate();
		}
		return Result.Succeeded;
	}
}
