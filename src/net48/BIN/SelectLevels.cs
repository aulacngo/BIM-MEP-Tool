using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectLevels : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		if (element.Category.Name == "Levels")
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
