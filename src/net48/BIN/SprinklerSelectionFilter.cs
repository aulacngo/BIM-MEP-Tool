using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SprinklerSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		FamilyInstance fi = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
		if (fi != null && ((Element)fi).Category != null && ((Element)fi).Category.GetIdInt() == -2008099)
		{
			return true;
		}
		return false;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return true;
	}
}
