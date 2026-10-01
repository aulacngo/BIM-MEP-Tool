using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BIN;

public class FilterItem
{
	public string Name { get; set; }

	public int ElementCount { get; set; }

	public List<ElementId> ElementIds { get; set; } = new List<ElementId>();
}
