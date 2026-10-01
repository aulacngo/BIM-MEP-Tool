using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SectionSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		if (elem is ViewSection)
		{
			return true;
		}
		if (elem.Category != null && elem.Category.GetIdInt() == -2000278)
		{
			return true;
		}
		return false;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return false;
	}
}
