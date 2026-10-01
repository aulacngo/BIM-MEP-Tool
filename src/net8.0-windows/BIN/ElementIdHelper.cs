using System;
using System.Reflection;
using Autodesk.Revit.DB;

namespace BIN;

public static class ElementIdHelper
{
	private static PropertyInfo _valueProperty;

	private static ConstructorInfo _longConstructor;

	static ElementIdHelper()
	{
		_valueProperty = typeof(ElementId).GetProperty("Value");
		if (_valueProperty == null)
		{
			_valueProperty = typeof(ElementId).GetProperty("IntegerValue");
		}
		_longConstructor = typeof(ElementId).GetConstructor(new Type[1] { typeof(long) });
	}

	public static long GetIdValue(ElementId id)
	{
		if (id == (ElementId)null)
		{
			return -1L;
		}
		return Convert.ToInt64(_valueProperty.GetValue(id));
	}

	public static ElementId CreateId(long id)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		if (_longConstructor != null)
		{
			return (ElementId)_longConstructor.Invoke(new object[1] { id });
		}
		return new ElementId((int)id);
	}

	public static bool IsValid(ElementId id)
	{
		if (id == (ElementId)null)
		{
			return false;
		}
		return GetIdValue(id) != -1;
	}
}
