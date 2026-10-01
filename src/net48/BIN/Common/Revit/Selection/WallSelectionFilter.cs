using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN.Common.Revit.Selection;

public class WallSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		if (elem.Category != null && ElementIdHelper.GetIdValue(elem.Category.Id) == -2000011)
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
