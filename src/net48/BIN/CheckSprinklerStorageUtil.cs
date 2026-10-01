using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace BIN;

public static class CheckSprinklerStorageUtil
{
	private static readonly Guid SchemaGuid = new Guid("B185C9DF-5F1C-4156-9968-7BAAE4142531");

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
		builder.SetSchemaName("BIN_CheckSprinkler_Resolved");
		builder.AddArrayField("ResolvedIds", typeof(string));
		return builder.Finish();
	}

	public static List<string> LoadResolvedSprinklers(Document doc)
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

	public static void SaveResolvedSprinklers(Document doc, List<string> resolved)
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
