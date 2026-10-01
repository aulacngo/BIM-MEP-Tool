using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace BIN.SelectByParam.Models;

internal class ElementCacheEntry
{
	public ElementId Id { get; set; }

	public BuiltInCategory Category { get; set; }

	public string CategoryName { get; set; }

	public Dictionary<string, string> ParameterValues { get; set; } = new Dictionary<string, string>();
}
