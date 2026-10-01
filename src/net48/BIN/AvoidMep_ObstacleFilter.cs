using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class AvoidMep_ObstacleFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		if (elem.Category == null)
		{
			return false;
		}
		int categoryId = elem.Category.Id.IntegerValue;
		return categoryId == -2008044 || categoryId == -2008000 || categoryId == -2008020 || categoryId == -2008130 || categoryId == -2008049 || categoryId == -2008010 || categoryId == -2008126;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return true;
	}
}
