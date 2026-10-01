using Autodesk.Revit.DB;

namespace BIN;

public class LevelItem
{
	public string Name { get; set; }

	public ElementId LevelId { get; set; }

	public double Elevation { get; set; }
}
