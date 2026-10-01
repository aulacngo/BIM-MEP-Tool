using Autodesk.Revit.DB;

namespace BIN;

public class ChangeTargetItem
{
	public string DisplayName { get; set; }

	public ElementId ElementId { get; set; }

	public int WorksetIdInt { get; set; }

	public override string ToString()
	{
		return DisplayName;
	}
}
