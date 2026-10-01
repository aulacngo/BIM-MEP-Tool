using Autodesk.Revit.DB;

namespace BIN;

public class SystemTypeItem
{
	public string Name { get; set; }

	public ElementId Id { get; set; }

	public override string ToString()
	{
		return Name;
	}
}
