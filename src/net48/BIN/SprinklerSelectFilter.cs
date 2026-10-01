using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SprinklerSelectFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		return elem != null && elem.Category?.GetIdInt() == -2008099;
	}

	public bool AllowReference(Reference r, XYZ p)
	{
		return true;
	}
}
