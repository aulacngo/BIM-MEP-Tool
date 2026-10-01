using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class AvoidMep_FlexDuctFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		return elem is FlexDuct;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return true;
	}
}
