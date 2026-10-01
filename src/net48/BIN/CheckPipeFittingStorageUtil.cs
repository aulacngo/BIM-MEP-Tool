using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace BIN;

public static class CheckPipeFittingStorageUtil
{
	private static readonly Guid SchemaGuid = new Guid("77BA519D-D871-46AF-BD23-64B86927AC59");

	public static Schema GetSchema()
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		Schema schema = Schema.Lookup(SchemaGuid);
		if (schema != null)
		{
			return schema;
		}
		SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
		builder.SetReadAccessLevel((AccessLevel)1);
		builder.SetWriteAccessLevel((AccessLevel)1);
		builder.SetSchemaName("BIN_CheckPipeFitting_Resolved");
		builder.AddArrayField("ResolvedIds", typeof(string));
		return builder.Finish();
	}

	public static List<string> LoadResolvedFittings(Document doc)
	{
		ProjectInfo pi = doc.ProjectInformation;
		if (pi == null)
		{
			return new List<string>();
		}
		try
		{
			Schema schema = GetSchema();
			Entity entity = ((Element)pi).GetEntity(schema);
			if (entity.IsValid())
			{
				IList<string> list = entity.Get<IList<string>>("ResolvedIds");
				return (list != null) ? list.ToList() : new List<string>();
			}
		}
		catch
		{
		}
		return new List<string>();
	}

	public static void SaveResolvedFittings(Document doc, List<string> resolved)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		ProjectInfo pi = doc.ProjectInformation;
		if (pi == null)
		{
			return;
		}
		try
		{
			Schema schema = GetSchema();
			Entity entity = new Entity(schema);
			entity.Set<IList<string>>("ResolvedIds", (IList<string>)resolved);
			((Element)pi).SetEntity(entity);
		}
		catch
		{
		}
	}
}
