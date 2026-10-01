using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectedPipe : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		if (element != null && element.Category.GetIdInt() == -2008044)
		{
			Location location = element.Location;
			LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
			if (locCurve != null)
			{
				Curve curve = locCurve.Curve;
				Line line = (Line)(object)((curve is Line) ? curve : null);
				if (line != null)
				{
					XYZ startPt = ((Curve)line).GetEndPoint(0);
					XYZ endPt = ((Curve)line).GetEndPoint(1);
					double horizontalLength = Math.Sqrt(Math.Pow(endPt.X - startPt.X, 2.0) + Math.Pow(endPt.Y - startPt.Y, 2.0));
					if (horizontalLength < 0.01)
					{
						return false;
					}
					double deltaZ = Math.Abs(startPt.Z - endPt.Z);
					double slope = deltaZ / ((Curve)line).Length;
					if (slope >= 0.5)
					{
						return false;
					}
					return true;
				}
			}
		}
		return false;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return false;
	}
}
