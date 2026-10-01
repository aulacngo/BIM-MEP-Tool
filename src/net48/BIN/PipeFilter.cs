using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class PipeFilter : ISelectionFilter
{
	public bool AllowElement(Element e)
	{
		return e is Pipe;
	}

	public bool AllowReference(Reference r, XYZ p)
	{
		return false;
	}
}
