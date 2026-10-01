using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectSprinklers : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		string id1 = $"{elem.Category.Id}";
		string id2 = $"{-2008099}";
		if (id1 == id2)
		{
			return true;
		}
		return false;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		throw new NotImplementedException();
	}
}
