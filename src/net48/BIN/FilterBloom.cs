using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class FilterBloom : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		if (elem.Category == null)
		{
			return false;
		}
		int catId = elem.Category.GetIdInt();
		return catId == -2008049 || catId == -2008055 || catId == -2008010 || catId == -2008016 || catId == -2008126;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return true;
	}
}
