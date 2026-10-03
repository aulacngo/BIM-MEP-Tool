using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class AvoidMep_FlexDuctFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
        // Only Host Flex is editable; linked obstacles use the obstacle filter.
		return elem is FlexDuct || elem is FlexPipe;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return reference != null && (reference.LinkedElementId == null
            || ElementId.InvalidElementId.Equals(reference.LinkedElementId));
	}
}
