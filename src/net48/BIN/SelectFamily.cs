using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectFamily : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		if (element is FamilyInstance)
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
