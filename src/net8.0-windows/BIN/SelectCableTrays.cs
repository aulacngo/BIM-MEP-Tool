using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectCableTrays : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		if (element.Category.Name == "Cable Trays")
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
