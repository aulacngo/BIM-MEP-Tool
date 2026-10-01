using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class AxisSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		return AxisHelper.IsValidAxisElement(elem);
	}

	public bool AllowReference(Reference reference, XYZ position) => true;
}