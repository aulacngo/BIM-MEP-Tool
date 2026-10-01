using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectPipes : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		if (element.Category.Name == "Pipes")
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
