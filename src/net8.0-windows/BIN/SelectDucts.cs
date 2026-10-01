using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectDucts : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		return element is Duct;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return false;
	}
}
