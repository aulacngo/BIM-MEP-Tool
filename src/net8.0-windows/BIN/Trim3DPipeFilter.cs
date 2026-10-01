using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class Trim3DPipeFilter : ISelectionFilter
{
	public bool AllowElement(Element e)
	{
		return e is Pipe || e is Duct;
	}

	public bool AllowReference(Reference r, XYZ p)
	{
		return false;
	}
}
