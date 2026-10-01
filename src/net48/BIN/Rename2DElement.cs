using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class Rename2DElement : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		UIApplication uiapp = commandData.Application;
		UIDocument activeUIDocument = uiapp.ActiveUIDocument;
		Document doc = ((activeUIDocument != null) ? activeUIDocument.Document : null);
		if (doc == null)
		{
			message = "No active document.";
			return Result.Failed;
		}
		try
		{
			RenameElementWindow window = new RenameElementWindow(uiapp);
			window.ShowDialog();
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.ToString();
			return Result.Failed;
		}
	}
}
