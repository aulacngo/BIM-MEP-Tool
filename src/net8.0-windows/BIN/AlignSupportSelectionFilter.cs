using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class AlignSupportSelectionFilter : ISelectionFilter
{
	private readonly HashSet<ElementId> _selectedSymbolIds;

	public AlignSupportSelectionFilter(HashSet<ElementId> selectedSymbolIds)
	{
		_selectedSymbolIds = selectedSymbolIds;
	}

	public bool AllowElement(Element elem)
	{
		if (elem is Pipe)
		{
			return true;
		}
		FamilyInstance fi = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
		if (fi != null && fi.Symbol != null)
		{
			return _selectedSymbolIds.Contains(((Element)fi.Symbol).Id);
		}
		return false;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return false;
	}
}
