using Autodesk.Revit.DB;

namespace BIN;

public static class Compat
{
	public static int GetIdInt(this ElementId id)
	{
		return id.IntegerValue;
	}

	public static int GetIdInt(this Element e)
	{
		return e.Id.GetIdInt();
	}

	public static int GetIdInt(this Category cat)
	{
		return cat.Id.GetIdInt();
	}

	public static int GetIdInt(this Workset ws)
	{
		return ((WorksetPreview)ws).Id.IntegerValue;
	}

	public static int GetIdInt(this WorksetId id)
	{
		return id.IntegerValue;
	}
}
