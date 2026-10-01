using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class LinkedFloorFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		return elem is RevitLinkInstance;
	}

	public bool AllowReference(Reference reference, XYZ point)
	{
		return true;
	}
}
