using Autodesk.Revit.DB;

namespace BIN;

public static class Compat
{
	public static int GetIdInt(this ElementId id)
	{
		return (int)id.Value;
	}

	public static int GetIdInt(this Category cat)
	{
		return (int)cat.Id.Value;
	}

	public static int GetIdInt(this Workset ws)
	{
		return ws.Id.IntegerValue;
	}

	public static int GetIdInt(this Element elem)
	{
		return (int)elem.Id.Value;
	}

	public static ElementId FromInt(int id)
	{
		return new ElementId((long)id);
	}

	public static int GetIdInt(this WorksetId worksetId)
	{
		return worksetId.IntegerValue;
	}

	public static WorksetId FromIntWorkset(int id)
	{
		return new WorksetId(id);
	}
}
