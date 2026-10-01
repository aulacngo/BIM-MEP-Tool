using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public class RotateElementsHandler : IExternalEventHandler
{
	public Document Doc { get; set; }
	public List<ElementId> ElementIdsToRotate { get; set; }
	public Line AxisLine { get; set; }
	public double AngleRad { get; set; }

	public void Execute(UIApplication app)
	{
		if (Doc == null || ElementIdsToRotate == null || ElementIdsToRotate.Count == 0 || AxisLine == null)
			return;

		try
		{
			using (Transaction trans = new Transaction(Doc, "BIN Rotate Elements 360"))
			{
				trans.Start();
				ElementTransformUtils.RotateElements(Doc, ElementIdsToRotate, AxisLine, AngleRad);
				trans.Commit();
			}
			app.ActiveUIDocument.RefreshActiveView();
		}
		catch (Exception ex)
		{
			TaskDialog.Show("Rotate Error", ex.Message);
		}
	}

	public string GetName()
	{
		return "BIN Rotate Elements Handler";
	}
}