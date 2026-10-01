using Autodesk.Revit.DB;

namespace BIN.SelectByParam.Models;

public class SBP_CategoryItem
{
	public BuiltInCategory BuiltInCategory { get; set; }

	public string Name { get; set; }

	public int ElementCount { get; set; }

	public string DisplayName => $"{Name} ({ElementCount})";

	public override string ToString()
	{
		return DisplayName;
	}
}
