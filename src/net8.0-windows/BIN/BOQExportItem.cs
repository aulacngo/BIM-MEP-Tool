using Autodesk.Revit.DB;

namespace BIN;

public class BOQExportItem
{
	public Element Element { get; set; }

	public Category Category { get; set; }

	public string GroupName { get; set; }

	public string FamilyName { get; set; }

	public string TypeName { get; set; }

	public string Size { get; set; }

	public string Angle { get; set; }

	public bool IsElbow { get; set; }

	public string ServiceType { get; set; }

	public string Level { get; set; }

	public bool IsBusway { get; set; }

	public bool IsBuswayPcs { get; set; }

	public int SubSystemOrder { get; set; }

	public double DuctThickness { get; set; }

	public string EiType { get; set; }
}
