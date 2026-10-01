using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN.Common.Revit.Selection;

public class ColumnWallSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		long categoryId = ElementIdHelper.GetIdValue(elem.Category.Id);
		if (categoryId == -2000011 || categoryId == -2001330 || categoryId == -2000100)
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
