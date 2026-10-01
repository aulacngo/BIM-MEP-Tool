using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace BIN;

public static class ClashStorageUtil
{
	private static readonly Guid SchemaGuid = new Guid("B5D68B40-7C00-4822-B1A1-801A4B02E5F2");

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
		builder.SetSchemaName("BIN_ClashDetective_Resolved");
		builder.AddArrayField("ResolvedClashes", typeof(string));
		return builder.Finish();
	}

	public static List<string> LoadResolvedClashes(Document doc)
	{
		ProjectInfo pi = doc.ProjectInformation;
		if (pi == null)
		{
			return new List<string>();
		}
		Schema schema = GetSchema();
		Entity entity = ((Element)pi).GetEntity(schema);
		if (entity.IsValid())
		{
			return entity.Get<IList<string>>("ResolvedClashes").ToList();
		}
		return new List<string>();
	}

	public static void SaveResolvedClashes(Document doc, List<string> resolved)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		ProjectInfo pi = doc.ProjectInformation;
		if (pi != null)
		{
			Schema schema = GetSchema();
			Entity entity = new Entity(schema);
			entity.Set<IList<string>>("ResolvedClashes", (IList<string>)resolved);
			((Element)pi).SetEntity(entity);
		}
	}
}
