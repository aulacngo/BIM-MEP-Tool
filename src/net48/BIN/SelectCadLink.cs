using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectCadLink : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		if (element is ImportInstance)
		{
			return true;
		}
		return false;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return false;
	}
}
