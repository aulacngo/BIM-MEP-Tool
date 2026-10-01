using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectGrids : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		if (element.Category.Name == "Grids")
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
