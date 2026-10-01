using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class FilterPipe : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		return elem is Pipe;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return true;
	}
}
