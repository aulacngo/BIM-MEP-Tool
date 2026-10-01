using Autodesk.Revit.DB;

namespace BIN;

public class BOQCategoryItem
{
	public string Name { get; set; }

	public Category Category { get; set; }

	public bool IsSelected { get; set; }
}
