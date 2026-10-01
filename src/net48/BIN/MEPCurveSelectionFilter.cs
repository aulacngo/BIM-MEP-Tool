using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class MEPCurveSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		return elem is MEPCurve;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return true;
	}
}
