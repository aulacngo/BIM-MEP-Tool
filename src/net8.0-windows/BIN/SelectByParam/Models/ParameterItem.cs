using Autodesk.Revit.DB;

namespace BIN.SelectByParam.Models;

public class ParameterItem
{
	public string Name { get; set; }

	public bool IsInstance { get; set; }

	public StorageType StorageType { get; set; }

	public string DisplayName => IsInstance ? Name : (Name + " (Type)");

	public override string ToString()
	{
		return DisplayName;
	}
}
