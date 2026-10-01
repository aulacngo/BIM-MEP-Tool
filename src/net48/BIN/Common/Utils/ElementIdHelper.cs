using Autodesk.Revit.DB;

namespace BIN.Common.Utils;

public static class ElementIdHelper
{
	public static long GetIdValue(ElementId id)
	{
		return id.IntegerValue;
	}

	public static ElementId CreateId(long id)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Expected O, but got Unknown
		return new ElementId((int)id);
	}
}
