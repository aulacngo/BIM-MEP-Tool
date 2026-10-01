using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class MepCurveFilter : ISelectionFilter
{
	public ElementId PreviousElementID { get; set; }

	public bool AllowElement(Element e)
	{
		if (e == null || e.Category == null)
		{
			return false;
		}
		switch (ElementIdHelper.GetIdValue(e.Category.Id))
		{
		case -2008208L:
		case -2008193L:
		case -2008132L:
		case -2008130L:
		case -2008000L:
			return true;
		case -2008044L:
			return PreviousElementID == (ElementId)null || e.Id != PreviousElementID;
		default:
			return false;
		}
	}

	public bool AllowReference(Reference r, XYZ p)
	{
		return true;
	}
}
