using Autodesk.Revit.DB;

namespace BIN.Common.Utils;

public static class ElementIdHelper
{
	public static int GetIdValue(ElementId id)
	{
		return (int)id.Value;
	}

	public static ElementId Create(int id)
	{
		return new ElementId((long)id);
	}
}
