using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI.Selection;

namespace BIN.Common.Revit.Selection;

public class RoomSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		return elem is Room;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return false;
	}
}
