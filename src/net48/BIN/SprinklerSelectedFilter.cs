using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SprinklerSelectedFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		Category category = elem.Category;
		return category != null && category.GetIdInt() == -2008099;
	}

	public bool AllowReference(Reference r, XYZ p)
	{
		return false;
	}
}
