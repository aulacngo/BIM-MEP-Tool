using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
using AcGe = Autodesk.AutoCAD.Geometry;

namespace BIN;

public class PlaceFamilyUtils
{
	public static string LastBlockScanDiagnostics { get; private set; } = "Not scanned";

	private sealed class DwgBlockScanStats
	{
		public int References;
		public int DynamicReferences;
		public int AnonymousReferences;
		public int Matches;
		public int XrefReferences;
		public int ResolvedXrefReferences;
		public int UnresolvedXrefReferences;
		public string XrefResolveError;
	}

	private static List<XYZ> GetBlockPointsFromDwg(ImportInstance importInstance, CADLinkType cadLinkType, string blockName, double manualUnitScale = 0.0, bool useTrueCenter = true)
	{
		List<XYZ> points = new List<XYZ>();
		ExternalFileReference reference = ExternalFileUtils.GetExternalFileReference(importInstance.Document, ((Element)cadLinkType).Id);
		string dwgPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath());
		if (string.IsNullOrWhiteSpace(dwgPath) || !File.Exists(dwgPath))
		{
			LastBlockScanDiagnostics = $"DWG unavailable; Path={dwgPath ?? "(null)"}";
			return points;
		}

		DwgBlockScanStats stats = new DwgBlockScanStats();
		AcDb.UnitsValue drawingUnits = AcDb.UnitsValue.Undefined;
		using (AcDb.Database database = new AcDb.Database(false, true))
		{
			database.ReadDwgFile(dwgPath, AcDb.FileOpenMode.OpenForReadAndAllShare, true, null);
			try
			{
				// ID drawings commonly keep repeated symbols in nested Xrefs. ReadDwgFile
				// loads the host database only; resolve the Xref graph before walking blocks.
				database.ResolveXrefs(true, false);
			}
			catch (Exception ex)
			{
				// Continue scanning the host drawing and expose the reason in diagnostics.
				stats.XrefResolveError = ex.GetType().Name + ": " + ex.Message;
			}
			drawingUnits = database.Insunits;
			using (AcDb.Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
			{
				AcDb.BlockTable table = (AcDb.BlockTable)transaction.GetObject(database.BlockTableId, AcDb.OpenMode.ForRead);
				AcDb.BlockTableRecord modelSpace = (AcDb.BlockTableRecord)transaction.GetObject(table[AcDb.BlockTableRecord.ModelSpace], AcDb.OpenMode.ForRead);
				CollectDwgBlockPoints(modelSpace, transaction, blockName, AcGe.Matrix3d.Identity, points, new HashSet<AcDb.ObjectId>(), stats, useTrueCenter);
			}
		}

		Transform importTransform = importInstance.GetTotalTransform();
		bool undefinedUnitLooksMetric = drawingUnits == AcDb.UnitsValue.Undefined &&
			points.Any(point => Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z))) > 50.0);
		double unitToFeet = manualUnitScale > 0.0
			? manualUnitScale
			: GetDrawingUnitToFeetFactor(drawingUnits);
		double importScale = importTransform.Scale;
		double pointScale = unitToFeet;
		LastBlockScanDiagnostics =
			$"Source=DWG; Target={blockName}; Refs={stats.References}; Dynamic={stats.DynamicReferences}; " +
			$"Anonymous={stats.AnonymousReferences}; Matches={stats.Matches}; Units={drawingUnits}; " +
			$"Xrefs={stats.XrefReferences}; ResolvedXrefs={stats.ResolvedXrefReferences}; UnresolvedXrefs={stats.UnresolvedXrefReferences}; " +
			$"XrefResolveError={stats.XrefResolveError ?? "(none)"}; " +
			$"UnitToFeet={unitToFeet:0.########}; UndefinedUnitHeuristic={(undefinedUnitLooksMetric ? "MetricByCoordinate" : "SafeDefault")}; " +
			$"ImportScale={importScale:0.########}; PointScale={pointScale:0.########}; Path={dwgPath}";
		return points.Select(point => importTransform.OfPoint(point.Multiply(pointScale))).ToList();
	}

	private static void CollectDwgBlockPoints(AcDb.BlockTableRecord record, AcDb.Transaction transaction, string targetName, AcGe.Matrix3d parentTransform, List<XYZ> points, HashSet<AcDb.ObjectId> path, DwgBlockScanStats stats, bool useTrueCenter)
	{
		if (!path.Add(record.ObjectId)) return;
		foreach (AcDb.ObjectId objectId in record)
		{
			AcDb.BlockReference blockReference = transaction.GetObject(objectId, AcDb.OpenMode.ForRead) as AcDb.BlockReference;
			if (blockReference == null) continue;
			stats.References++;
			AcDb.BlockTableRecord definition = (AcDb.BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, AcDb.OpenMode.ForRead);
			if (definition.IsFromExternalReference)
			{
				stats.XrefReferences++;
				if (definition.IsResolved) stats.ResolvedXrefReferences++;
				else stats.UnresolvedXrefReferences++;
			}
			if (definition.IsAnonymous) stats.AnonymousReferences++;
			string effectiveName = definition.Name;
			if (blockReference.IsDynamicBlock)
			{
				stats.DynamicReferences++;
				try
				{
					AcDb.BlockTableRecord dynamicDefinition = transaction.GetObject(blockReference.DynamicBlockTableRecord, AcDb.OpenMode.ForRead) as AcDb.BlockTableRecord;
					if (dynamicDefinition != null) effectiveName = dynamicDefinition.Name;
				}
				catch
				{
					// Keep the concrete record name if the effective dynamic definition is unavailable.
				}
			}
			AcGe.Matrix3d worldTransform = blockReference.BlockTransform * parentTransform;
			if (BlockNameMatches(effectiveName, targetName))
			{
				AcGe.Point3d localAnchor = useTrueCenter ? GetBlockTrueCenter(definition, transaction) : AcGe.Point3d.Origin;
				AcGe.Point3d point = localAnchor.TransformBy(worldTransform);
				points.Add(new XYZ(point.X, point.Y, point.Z));
				stats.Matches++;
			}
			CollectDwgBlockPoints(definition, transaction, targetName, worldTransform, points, new HashSet<AcDb.ObjectId>(path), stats, useTrueCenter);
		}
	}

	public static AcGe.Point3d GetBlockTrueCenter(AcDb.BlockTableRecord definition, AcDb.Transaction transaction)
	{
		if (definition == null || transaction == null)
		{
			return AcGe.Point3d.Origin;
		}

		List<AcGe.Point3d> circleCenters = new List<AcGe.Point3d>();
		bool hasBounds = false;
		double minX = 0.0;
		double minY = 0.0;
		double maxX = 0.0;
		double maxY = 0.0;
		foreach (AcDb.ObjectId objectId in definition)
		{
			AcDb.Entity entity = transaction.GetObject(objectId, AcDb.OpenMode.ForRead) as AcDb.Entity;
			if (entity == null || IsAnnotationEntity(entity) || !IsTrueCenterGeometry(entity))
			{
				continue;
			}

			if (entity is AcDb.Circle circle)
			{
				circleCenters.Add(circle.Center);
			}
			else if (entity is AcDb.Arc arc)
			{
				circleCenters.Add(arc.Center);
			}

			TryExpandEntityBounds(entity, ref hasBounds, ref minX, ref minY, ref maxX, ref maxY);
		}

		if (circleCenters.Count == 1 || AreCentersCoincident(circleCenters, GetOneMillimeterInDrawingUnits(definition)))
		{
			return circleCenters[0];
		}
		if (hasBounds && maxX > minX && maxY > minY)
		{
			return new AcGe.Point3d((minX + maxX) / 2.0, (minY + maxY) / 2.0, 0.0);
		}
		return AcGe.Point3d.Origin;
	}

	private static bool IsAnnotationEntity(AcDb.Entity entity)
	{
		return entity is AcDb.DBText || entity is AcDb.MText || entity is AcDb.Dimension ||
			entity is AcDb.Leader || entity is AcDb.MLeader || entity is AcDb.AttributeDefinition;
	}

	private static bool IsTrueCenterGeometry(AcDb.Entity entity)
	{
		return entity is AcDb.Polyline || entity is AcDb.Polyline2d || entity is AcDb.Line ||
			entity is AcDb.Solid || entity is AcDb.Face || entity is AcDb.Circle || entity is AcDb.Arc;
	}

	private static void TryExpandEntityBounds(AcDb.Entity entity, ref bool hasBounds, ref double minX, ref double minY, ref double maxX, ref double maxY)
	{
		try
		{
			AcDb.Extents3d extents = entity.GeometricExtents;
			AcGe.Point3d minPoint = extents.MinPoint;
			AcGe.Point3d maxPoint = extents.MaxPoint;
			if (!hasBounds)
			{
				minX = minPoint.X;
				minY = minPoint.Y;
				maxX = maxPoint.X;
				maxY = maxPoint.Y;
				hasBounds = true;
			}
			else
			{
				minX = Math.Min(minX, minPoint.X);
				minY = Math.Min(minY, minPoint.Y);
				maxX = Math.Max(maxX, maxPoint.X);
				maxY = Math.Max(maxY, maxPoint.Y);
			}
		}
		catch
		{
			// Some proxy or malformed CAD entities do not expose geometric extents.
		}
	}

	private static bool AreCentersCoincident(List<AcGe.Point3d> centers, double tolerance)
	{
		if (centers == null || centers.Count == 0) return false;
		AcGe.Point3d first = centers[0];
		foreach (AcGe.Point3d center in centers)
		{
			double dx = center.X - first.X;
			double dy = center.Y - first.Y;
			double dz = center.Z - first.Z;
			if (Math.Sqrt(dx * dx + dy * dy + dz * dz) > tolerance) return false;
		}
		return true;
	}

	private static double GetOneMillimeterInDrawingUnits(AcDb.BlockTableRecord definition)
	{
		try
		{
			double drawingUnitToMillimeter = AcDb.UnitsConverter.GetConversionFactor(definition.Database.Insunits, AcDb.UnitsValue.Millimeters);
			if (drawingUnitToMillimeter > 0.0 && !double.IsNaN(drawingUnitToMillimeter) && !double.IsInfinity(drawingUnitToMillimeter))
			{
				return 1.0 / drawingUnitToMillimeter;
			}
		}
		catch
		{
			// Undefined units fall back to the common millimeter CAD convention.
		}
		return 1.0;
	}

	private static bool BlockNameMatches(string actualName, string targetName)
	{
		if (string.Equals(actualName, targetName, StringComparison.OrdinalIgnoreCase)) return true;
		if (string.IsNullOrWhiteSpace(actualName) || string.IsNullOrWhiteSpace(targetName)) return false;
		int xrefSeparator = actualName.LastIndexOf('|');
		if (xrefSeparator >= 0 && xrefSeparator + 1 < actualName.Length)
		{
			actualName = actualName.Substring(xrefSeparator + 1);
		}
		int bindSeparator = actualName.LastIndexOf("$0$", StringComparison.OrdinalIgnoreCase);
		if (bindSeparator >= 0 && bindSeparator + 3 < actualName.Length)
		{
			actualName = actualName.Substring(bindSeparator + 3);
		}
		return string.Equals(actualName, targetName, StringComparison.OrdinalIgnoreCase);
	}

	public static double ParseCadUnitToFeetFactor(string unitName)
	{
		if (string.IsNullOrWhiteSpace(unitName)) return 0.0;
		if (unitName.IndexOf("Millimeter", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(mm)", StringComparison.OrdinalIgnoreCase) >= 0)
			return 1.0 / 304.8;
		if (unitName.IndexOf("Meter", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(m)", StringComparison.OrdinalIgnoreCase) >= 0)
			return 1.0 / 0.3048;
		if (unitName.IndexOf("Centimeter", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(cm)", StringComparison.OrdinalIgnoreCase) >= 0)
			return 1.0 / 30.48;
		if (unitName.IndexOf("Inch", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(in)", StringComparison.OrdinalIgnoreCase) >= 0)
			return 1.0 / 12.0;
		if (unitName.IndexOf("Feet", StringComparison.OrdinalIgnoreCase) >= 0 || unitName.IndexOf("(ft)", StringComparison.OrdinalIgnoreCase) >= 0)
			return 1.0;
		return 0.0;
	}

	private static double GetDrawingUnitToFeetFactor(AcDb.UnitsValue units)
	{
		try
		{
			if (units == AcDb.UnitsValue.Undefined) return 1.0 / 304.8;
			double factor = AcDb.UnitsConverter.GetConversionFactor(units, AcDb.UnitsValue.Feet);
			return factor > 0.0 && !double.IsNaN(factor) && !double.IsInfinity(factor) ? factor : 1.0 / 304.8;
		}
		catch
		{
			return 1.0 / 304.8;
		}
	}
	private static string CleanBlockName(string rawName, string filename, string cadLinkTypeName)
	{
		if (string.IsNullOrEmpty(rawName))
		{
			return "";
		}
		string name = rawName;
		if (name.StartsWith(filename, StringComparison.OrdinalIgnoreCase))
		{
			name = name.Substring(filename.Length);
		}
		else
		{
			string nameWithoutExt = Path.GetFileNameWithoutExtension(cadLinkTypeName) + ".";
			if (name.StartsWith(nameWithoutExt, StringComparison.OrdinalIgnoreCase))
			{
				name = name.Substring(nameWithoutExt.Length);
			}
		}
		return name;
	}

	private static void CollectBlockNames(GeometryElement geometryElement, string filename, List<string> listname, CADLinkType cadLinkType)
	{
		if ((GeometryObject)(object)geometryElement == (GeometryObject)null)
		{
			return;
		}
		foreach (GeometryObject geomOb in geometryElement)
		{
			GeometryInstance blockInstance = (GeometryInstance)(object)((geomOb is GeometryInstance) ? geomOb : null);
			if (blockInstance != null)
			{
				string rawName = blockInstance.Symbol.Name;
				string name = CleanBlockName(rawName, filename, ((Element)cadLinkType).Name);
				if (!string.IsNullOrEmpty(name) && !name.Equals("Model", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Layout", StringComparison.OrdinalIgnoreCase) && !name.Equals(((Element)cadLinkType).Name, StringComparison.OrdinalIgnoreCase))
				{
					listname.Add(name);
				}
				GeometryElement nestedGeo = blockInstance.GetSymbolGeometry();
				if ((GeometryObject)(object)nestedGeo != (GeometryObject)null)
				{
					CollectBlockNames(nestedGeo, filename, listname, cadLinkType);
				}
			}
		}
	}

	private static void DebugTraverse(GeometryElement geo, StringBuilder sb, string indent, int depth, CADLinkType cadLinkType)
	{
		if ((GeometryObject)(object)geo == (GeometryObject)null || depth > 5)
		{
			return;
		}
		foreach (GeometryObject obj in geo)
		{
			GeometryInstance gi = (GeometryInstance)(object)((obj is GeometryInstance) ? obj : null);
			if (gi != null)
			{
				Element symbol = gi.Symbol;
				string rawName = ((symbol != null) ? symbol.Name : null) ?? "(null)";
				sb.AppendLine(indent + "[GeoInstance] Raw=" + rawName);
				DebugTraverse(gi.GetSymbolGeometry(), sb, indent + "  ", depth + 1, cadLinkType);
			}
			else
			{
				sb.AppendLine(indent + "[" + ((object)obj).GetType().Name + "]");
			}
		}
	}

	public static void DumpDebugToFile(ImportInstance importInstance, CADLinkType cadLinkType)
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Expected O, but got Unknown
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("=== PlaceFamily Debug ===");
		sb.AppendLine("CADLinkType.Name = " + ((Element)cadLinkType).Name);
		sb.AppendLine($"ImportInstance.IsLinked = {importInstance.IsLinked}");
		sb.AppendLine();
		sb.AppendLine("--- CADLinkType.get_Geometry ---");
		GeometryElement geoType = ((Element)cadLinkType).get_Geometry(new Options());
		if ((GeometryObject)(object)geoType == (GeometryObject)null)
		{
			sb.AppendLine("  NULL");
		}
		else
		{
			int i = 0;
			foreach (GeometryObject obj in geoType)
			{
				sb.AppendLine($"  [Root {i++}] {((object)obj).GetType().Name}");
				GeometryInstance gi = (GeometryInstance)(object)((obj is GeometryInstance) ? obj : null);
				if (gi != null)
				{
					Element symbol = gi.Symbol;
					string rawName = ((symbol != null) ? symbol.Name : null) ?? "(null)";
					sb.AppendLine("    Raw=" + rawName);
					sb.AppendLine("    --- GetSymbolGeometry ---");
					DebugTraverse(gi.GetSymbolGeometry(), sb, "      ", 0, cadLinkType);
				}
			}
		}
		sb.AppendLine();
		sb.AppendLine("--- ImportInstance.get_Geometry ---");
		GeometryElement geoInst = ((Element)importInstance).get_Geometry(new Options());
		if ((GeometryObject)(object)geoInst == (GeometryObject)null)
		{
			sb.AppendLine("  NULL");
		}
		else
		{
			int i2 = 0;
			foreach (GeometryObject obj2 in geoInst)
			{
				sb.AppendLine($"  [Root {i2++}] {((object)obj2).GetType().Name}");
				GeometryInstance gi2 = (GeometryInstance)(object)((obj2 is GeometryInstance) ? obj2 : null);
				if (gi2 != null)
				{
					Element symbol2 = gi2.Symbol;
					string rawName2 = ((symbol2 != null) ? symbol2.Name : null) ?? "(null)";
					sb.AppendLine("    Raw=" + rawName2);
					sb.AppendLine("    --- GetSymbolGeometry ---");
					DebugTraverse(gi2.GetSymbolGeometry(), sb, "      ", 0, cadLinkType);
				}
			}
		}
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "PlaceFamily_debug.txt");
		File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
	}

	public static List<string> GetListBlockCad(ImportInstance importInstance, CADLinkType cadLinkType)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		List<string> listname = new List<string>();
		string filename = ((Element)cadLinkType).Name + ".";
		GeometryElement geoFromInstance = ((Element)importInstance).get_Geometry(new Options());
		ScanRootGeometry(geoFromInstance, filename, listname, cadLinkType);
		if (listname.Count == 0)
		{
			GeometryElement geoFromType = ((Element)cadLinkType).get_Geometry(new Options());
			ScanRootGeometry(geoFromType, filename, listname, cadLinkType);
		}
		return (from x in listname.Distinct()
			orderby x
			select x).ToList();
	}

	private static void ScanRootGeometry(GeometryElement geometryElement, string filename, List<string> listname, CADLinkType cadLinkType)
	{
		if ((GeometryObject)(object)geometryElement == (GeometryObject)null)
		{
			return;
		}
		foreach (GeometryObject geometryOb in geometryElement)
		{
			GeometryInstance topLevelInstance = (GeometryInstance)(object)((geometryOb is GeometryInstance) ? geometryOb : null);
			if (topLevelInstance != null)
			{
				GeometryElement symbolGeometry = topLevelInstance.GetSymbolGeometry();
				if ((GeometryObject)(object)symbolGeometry != (GeometryObject)null)
				{
					CollectBlockNames(symbolGeometry, filename, listname, cadLinkType);
				}
				GeometryElement instanceGeometry = topLevelInstance.GetInstanceGeometry();
				if ((GeometryObject)(object)instanceGeometry != (GeometryObject)null)
				{
					CollectBlockNames(instanceGeometry, filename, listname, cadLinkType);
				}
			}
		}
	}

	public static List<string> GetListFamily(Document doc, FamilyInstance instance)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		List<string> listFamily = new List<string>();
		BuiltInCategory builtInCategory = (BuiltInCategory)((Element)instance).Category.GetIdInt();
		List<FamilySymbol> listType = ((IEnumerable)new FilteredElementCollector(doc).OfCategory(builtInCategory).WhereElementIsElementType()).Cast<FamilySymbol>().ToList();
		foreach (FamilySymbol familySymbol in listType)
		{
			if (!listFamily.Contains(((ElementType)familySymbol).FamilyName))
			{
				listFamily.Add(((ElementType)familySymbol).FamilyName);
			}
		}
		return listFamily;
	}

	public static List<string> GetListTypeByFamilyName(Document doc, FamilyInstance instance, string familyName)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		BuiltInCategory builtIn = (BuiltInCategory)((Element)instance).Category.GetIdInt();
		return (from FamilySymbol x in (IEnumerable)new FilteredElementCollector(doc).OfCategory(builtIn).WhereElementIsElementType()
			where ((ElementType)x).FamilyName == familyName
			select ((Element)x).Name).ToList();
	}

	public static List<string> ListLevel(Document doc)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		return (from Level z in (IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2000240)).WhereElementIsNotElementType()
			orderby z.Elevation
			select ((Element)z).Name).ToList();
	}

	public static Level GetLevelByName(Document doc, string levelName)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return ((IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2000240)).WhereElementIsNotElementType()).Cast<Level>().FirstOrDefault((Level x) => ((Element)x).Name == levelName);
	}

	public static FamilySymbol GetFamilySymbolByName(Document doc, FamilyInstance instance, string familyName, string typeName)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		BuiltInCategory builtIn = (BuiltInCategory)((Element)instance).Category.GetIdInt();
		return ((IEnumerable)new FilteredElementCollector(doc).OfCategory(builtIn).WhereElementIsElementType()).Cast<FamilySymbol>().FirstOrDefault((FamilySymbol s) => ((ElementType)s).FamilyName == familyName && ((Element)s).Name == typeName);
	}

	public static XYZ GetRevitGeometryTrueCenter(GeometryElement symbolGeo)
	{
		RevitGeometryCenterScan scan = new RevitGeometryCenterScan();
		CollectRevitGeometryCenterCandidates(symbolGeo, Transform.Identity, scan);
		if (scan.CircleCenters.Count == 1 || AreCentersCoincident(scan.CircleCenters, 1.0 / 304.8))
		{
			return scan.CircleCenters[0];
		}
		if (scan.HasBounds && scan.MaxX > scan.MinX && scan.MaxY > scan.MinY)
		{
			return new XYZ((scan.MinX + scan.MaxX) / 2.0, (scan.MinY + scan.MaxY) / 2.0, 0.0);
		}
		return XYZ.Zero;
	}

	private sealed class RevitGeometryCenterScan
	{
		public readonly List<XYZ> CircleCenters = new List<XYZ>();
		public bool HasBounds;
		public double MinX;
		public double MinY;
		public double MaxX;
		public double MaxY;
	}

	private static void CollectRevitGeometryCenterCandidates(GeometryElement geo, Transform parentTransform, RevitGeometryCenterScan scan)
	{
		if ((GeometryObject)(object)geo == (GeometryObject)null) return;
		foreach (GeometryObject obj in geo)
		{
			GeometryInstance nestedInstance = obj as GeometryInstance;
			if (nestedInstance != null)
			{
				Transform nestedTransform = parentTransform.Multiply(nestedInstance.Transform);
				CollectRevitGeometryCenterCandidates(nestedInstance.GetSymbolGeometry(), nestedTransform, scan);
				continue;
			}

			Curve curve = obj as Curve;
			if (curve == null) continue;
			Arc arc = curve as Arc;
			if (arc != null)
			{
				XYZ center = parentTransform.OfPoint(arc.Center);
				scan.CircleCenters.Add(new XYZ(center.X, center.Y, 0.0));
			}
			AddCurveBounds(curve, parentTransform, scan);
		}
	}

	private static void AddCurveBounds(Curve curve, Transform transform, RevitGeometryCenterScan scan)
	{
		bool hasTessellation = false;
		try
		{
			foreach (XYZ point in curve.Tessellate())
			{
				AddRevitGeometryPoint(transform.OfPoint(point), scan);
				hasTessellation = true;
			}
		}
		catch
		{
			// Fall back to curve endpoints when a curve cannot be tessellated.
		}
		if (hasTessellation) return;
		try
		{
			AddRevitGeometryPoint(transform.OfPoint(curve.GetEndPoint(0)), scan);
			AddRevitGeometryPoint(transform.OfPoint(curve.GetEndPoint(1)), scan);
		}
		catch
		{
			// Ignore malformed imported curves and keep the remaining valid geometry.
		}
	}

	private static void AddRevitGeometryPoint(XYZ point, RevitGeometryCenterScan scan)
	{
		if (!scan.HasBounds)
		{
			scan.MinX = point.X;
			scan.MinY = point.Y;
			scan.MaxX = point.X;
			scan.MaxY = point.Y;
			scan.HasBounds = true;
			return;
		}
		scan.MinX = Math.Min(scan.MinX, point.X);
		scan.MinY = Math.Min(scan.MinY, point.Y);
		scan.MaxX = Math.Max(scan.MaxX, point.X);
		scan.MaxY = Math.Max(scan.MaxY, point.Y);
	}

	private static bool AreCentersCoincident(List<XYZ> centers, double tolerance)
	{
		if (centers == null || centers.Count == 0) return false;
		XYZ first = centers[0];
		foreach (XYZ center in centers)
		{
			double dx = center.X - first.X;
			double dy = center.Y - first.Y;
			double dz = center.Z - first.Z;
			if (Math.Sqrt(dx * dx + dy * dy + dz * dz) > tolerance) return false;
		}
		return true;
	}

	private static void CollectBlockPointsFromInstanceGeo(GeometryElement geo, string filename, string targetName, List<XYZ> points, Transform parentTransform, CADLinkType cadLinkType, bool useTrueCenter)
	{
		if ((GeometryObject)(object)geo == (GeometryObject)null)
		{
			return;
		}
		foreach (GeometryObject obj in geo)
		{
			GeometryInstance gi = (GeometryInstance)(object)((obj is GeometryInstance) ? obj : null);
			if (gi != null)
			{
				Element symbol = gi.Symbol;
				string rawName = ((symbol != null) ? symbol.Name : null) ?? "";
				string name = CleanBlockName(rawName, filename, ((Element)cadLinkType).Name);
				Transform worldTransform = ((parentTransform != null) ? parentTransform.Multiply(gi.Transform) : gi.Transform);
				GeometryElement nestedSym = gi.GetSymbolGeometry();
				if (name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
				{
					XYZ localCenter = useTrueCenter ? GetRevitGeometryTrueCenter(nestedSym) : XYZ.Zero;
					points.Add(worldTransform.OfPoint(localCenter));
				}
				if ((GeometryObject)(object)nestedSym != (GeometryObject)null)
				{
					CollectBlockPointsFromInstanceGeo(nestedSym, filename, targetName, points, worldTransform, cadLinkType, useTrueCenter);
				}
			}
		}
	}

	private static void CollectBlockPointsFromPairedGeo(GeometryElement symbolGeo, GeometryElement instanceGeo, string filename, string targetName, List<XYZ> points, CADLinkType cadLinkType)
	{
		if ((GeometryObject)(object)symbolGeo == (GeometryObject)null || (GeometryObject)(object)instanceGeo == (GeometryObject)null)
		{
			return;
		}
		List<GeometryObject> symbolObjects = symbolGeo.Cast<GeometryObject>().ToList();
		List<GeometryObject> instanceObjects = instanceGeo.Cast<GeometryObject>().ToList();
		int count = Math.Min(symbolObjects.Count, instanceObjects.Count);
		for (int i = 0; i < count; i++)
		{
			GeometryInstance symbolInstance = symbolObjects[i] as GeometryInstance;
			GeometryInstance projectInstance = instanceObjects[i] as GeometryInstance;
			if (symbolInstance == null || projectInstance == null)
			{
				continue;
			}
			Element symbol = symbolInstance.Symbol;
			string rawName = ((symbol != null) ? symbol.Name : null) ?? "";
			string name = CleanBlockName(rawName, filename, ((Element)cadLinkType).Name);
			if (name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
			{
				points.Add(projectInstance.Transform.Origin);
			}
			CollectBlockPointsFromPairedGeo(symbolInstance.GetSymbolGeometry(), projectInstance.GetInstanceGeometry(), filename, targetName, points, cadLinkType);
		}
	}

	public static List<XYZ> GetListBlockCadByName(ImportInstance importInstance, CADLinkType cadLinkType, string blockName, string selectedCadUnit = "Auto Detect (Tự động)", bool useTrueCenter = true)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		try
		{
			double manualUnitScale = ParseCadUnitToFeetFactor(selectedCadUnit);
			List<XYZ> dwgPoints = GetBlockPointsFromDwg(importInstance, cadLinkType, blockName, manualUnitScale, useTrueCenter);
			if (dwgPoints.Count > 0)
			{
				LastBlockScanDiagnostics += "; SelectedSource=DWG";
				return dwgPoints;
			}
		}
		catch (Exception ex)
		{
			LastBlockScanDiagnostics = $"Source=DWG; Target={blockName}; Error={ex.GetType().Name}: {ex.Message}";
		}
		List<XYZ> listPoints = new List<XYZ>();
		string filename = ((Element)cadLinkType).Name + ".";
		GeometryElement geoEle = ((Element)importInstance).get_Geometry(new Options());
		if ((GeometryObject)(object)geoEle != (GeometryObject)null)
		{
			foreach (GeometryObject geoOb in geoEle)
			{
				GeometryInstance topLevelInstance = (GeometryInstance)(object)((geoOb is GeometryInstance) ? geoOb : null);
				if (topLevelInstance != null)
				{
					// Symbol geometry is in the CAD link's local coordinates. Seed the
					// traversal with the top-level instance transform exactly once.
					CollectBlockPointsFromInstanceGeo(topLevelInstance.GetSymbolGeometry(), filename, blockName, listPoints, topLevelInstance.Transform, cadLinkType, useTrueCenter);
				}
			}
		}
		if (listPoints.Count == 0)
		{
			GeometryElement geoType = ((Element)cadLinkType).get_Geometry(new Options());
			if ((GeometryObject)(object)geoType != (GeometryObject)null)
			{
				foreach (GeometryObject geoOb2 in geoType)
				{
					GeometryInstance topLevelInstance2 = (GeometryInstance)(object)((geoOb2 is GeometryInstance) ? geoOb2 : null);
					if (topLevelInstance2 != null)
					{
						GeometryElement symGeo2 = topLevelInstance2.GetSymbolGeometry();
						if ((GeometryObject)(object)symGeo2 != (GeometryObject)null)
						{
							CollectBlockPointsFromInstanceGeo(symGeo2, filename, blockName, listPoints, topLevelInstance2.Transform, cadLinkType, useTrueCenter);
						}
					}
				}
			}
		}
		// The top-level transform converted CAD symbol points to project coordinates.
		// Do not apply ImportInstance.GetTotalTransform again.
		LastBlockScanDiagnostics += $"; FallbackMatches={listPoints.Count}; SelectedSource=RevitGeometry; Transform=TopLevelAppliedOnce";
		return listPoints;
	}
}
