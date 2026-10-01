using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class CutMEPByLevel : IExternalCommand
{
	private const double MIN_ABOVE_LENGTH_MM = 1000.0;

	private const double MIN_ABOVE_LENGTH_FT = 3.2808398950131235;

	private const double TOLERANCE = 0.001;

	private static readonly BuiltInCategory[] CuttableCategories;

	private static readonly BuiltInCategory[] AssignOnlyCategories;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0371: Unknown result type (might be due to invalid IL or missing references)
		//IL_038d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0394: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Invalid comparison between Unknown and I4
		//IL_0398: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Expected O, but got Unknown
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Expected O, but got Unknown
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Expected O, but got Unknown
		//IL_01ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_030b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			TaskDialog td = new TaskDialog("BIM - Cut MEP By Level");
			td.MainInstruction = "Trước khi chạy tool, bạn phải điều chỉnh View hiện hành thật chuẩn để tránh sinh ra lỗi.";
			td.CommonButtons = (TaskDialogCommonButtons)9;
			td.DefaultButton = (TaskDialogResult)1;
			TaskDialogResult tdResult = td.Show();
			if ((int)tdResult == 2)
			{
				return Result.Cancelled;
			}
			List<Level> allLevels = (from Level l in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(Level))
				orderby l.Elevation
				select l).ToList();
			if (allLevels.Count < 2)
			{
			TaskDialog.Show("BIM TOOL", "Cần ít nhất 2 Level trong project.");
				return Result.Cancelled;
			}
			int cutCount = 0;
			int assignCount = 0;
			int propagateCount = 0;
			Transaction t = new Transaction(doc, "BIN_CutMEPByLevel");
			try
			{
				t.Start();
				FailureHandlingOptions failOpts = t.GetFailureHandlingOptions();
				failOpts.SetClearAfterRollback(true);
				failOpts.SetFailuresPreprocessor((IFailuresPreprocessor)(object)new IgnoreWarningsPreprocessor());
				t.SetFailureHandlingOptions(failOpts);
				ElementMulticategoryFilter cuttableFilter = new ElementMulticategoryFilter((ICollection<BuiltInCategory>)CuttableCategories);
				List<Element> cuttableElements = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id).WherePasses((ElementFilter)(object)cuttableFilter).WhereElementIsNotElementType().ToElements()
					.ToList();
				List<Element> verticalElements = cuttableElements.Where((Element e) => IsVertical(e)).ToList();
				foreach (Element elem in verticalElements)
				{
					int cuts = ProcessCuttableElement(doc, elem, allLevels);
					cutCount += cuts;
				}
				doc.Regenerate();
				List<BuiltInCategory> allMepCategories = CuttableCategories.Concat(AssignOnlyCategories).ToList();
				ElementMulticategoryFilter allMepFilter = new ElementMulticategoryFilter((ICollection<BuiltInCategory>)allMepCategories);
				List<Element> allMepElements = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id).WherePasses((ElementFilter)(object)allMepFilter).WhereElementIsNotElementType().ToElements()
					.ToList();
				foreach (Element elem2 in allMepElements)
				{
					if (AssignReferenceLevel(doc, elem2, allLevels))
					{
						assignCount++;
					}
				}
				doc.Regenerate();
				propagateCount = 0;
				List<Element> allMepAfter = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id).WherePasses((ElementFilter)(object)allMepFilter).WhereElementIsNotElementType().ToElements()
					.ToList();
				List<Element> verticalPipesForPropagate = allMepAfter.Where((Element e) => IsVertical(e) && HasUncutCrossing(e, allLevels)).ToList();
				foreach (Element vertPipe in verticalPipesForPropagate)
				{
					Level targetLevel = DetermineTargetLevel(doc, vertPipe, allLevels);
					if (targetLevel != null)
					{
						propagateCount += PropagateReferenceLevel(doc, vertPipe, targetLevel);
					}
				}
				assignCount += propagateCount;
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			TaskDialog.Show("BIM - Cut MEP By Level", "Hoàn thành!\n" + $"• Số lần cắt ống đứng: {cutCount}\n" + $"• Số đối tượng đã gán Reference Level: {assignCount}\n" + $"• Trong đó lan truyền từ ống đứng: {propagateCount}");
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			TaskDialog.Show("BIM TOOL - Error", ex.ToString());
			return Result.Failed;
		}
	}

	private bool IsVertical(Element elem)
	{
		Location location = elem.Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		if (locCurve == null)
		{
			return false;
		}
		Curve curve = locCurve.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		if ((GeometryObject)(object)line == (GeometryObject)null)
		{
			return false;
		}
		XYZ direction = line.Direction;
		double dotZ = Math.Abs(direction.DotProduct(XYZ.BasisZ));
		return dotZ > 0.996;
	}

	private void GetVerticalExtents(Element elem, out double zMin, out double zMax)
	{
		Location location = elem.Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locCurve.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		double z0 = ((Curve)line).GetEndPoint(0).Z;
		double z1 = ((Curve)line).GetEndPoint(1).Z;
		zMin = Math.Min(z0, z1);
		zMax = Math.Max(z0, z1);
	}

	private bool HasFittingConnectedAtTop(Element elem)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Expected O, but got Unknown
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Invalid comparison between Unknown and I4
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Invalid comparison between Unknown and I4
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Invalid comparison between Unknown and I4
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Invalid comparison between Unknown and I4
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Invalid comparison between Unknown and I4
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Invalid comparison between Unknown and I4
		MEPCurve mepCurve = (MEPCurve)(object)((elem is MEPCurve) ? elem : null);
		if (((mepCurve != null) ? mepCurve.ConnectorManager : null) == null)
		{
			return false;
		}
		GetVerticalExtents(elem, out var _, out var zMax);
		foreach (Connector connector2 in mepCurve.ConnectorManager.Connectors)
		{
			Connector connector = connector2;
			if (Math.Abs(connector.Origin.Z - zMax) > 0.01)
			{
				continue;
			}
			if (!connector.IsConnected)
			{
				return false;
			}
			foreach (Connector allRef in connector.AllRefs)
			{
				Connector refConn = allRef;
				Element owner = refConn.Owner;
				if (owner != null && !(owner.Id == elem.Id) && owner.Category != null)
				{
					BuiltInCategory cat = (BuiltInCategory)owner.Category.GetIdInt();
					if ((int)cat == -2008049 || (int)cat == -2008010 || (int)cat == -2008126 || (int)cat == -2008128 || (int)cat == -2008055 || (int)cat == -2008016)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private int ProcessCuttableElement(Document doc, Element elem, List<Level> allLevels)
	{
		int cutsMade = 0;
		GetVerticalExtents(elem, out var origZMin, out var origZMax);
		List<Level> crossingLevels = (from l in allLevels
			where l.Elevation > origZMin + 0.001 && l.Elevation < origZMax - 0.001
			orderby l.Elevation
			select l).ToList();
		if (crossingLevels.Count == 0)
		{
			return 0;
		}
		bool hasFittingAtTop = HasFittingConnectedAtTop(elem);
		List<Level> levelsToCut = new List<Level>();
		foreach (Level level in crossingLevels)
		{
			double aboveLength = origZMax - level.Elevation;
			if (aboveLength >= 3.2808398950131235)
			{
				levelsToCut.Add(level);
			}
			else if (!hasFittingAtTop)
			{
				levelsToCut.Add(level);
			}
		}
		if (levelsToCut.Count == 0)
		{
			return 0;
		}
		levelsToCut = levelsToCut.OrderByDescending((Level l) => l.Elevation).ToList();
		ElementId currentId = elem.Id;
		foreach (Level level2 in levelsToCut)
		{
			Element currentElem = doc.GetElement(currentId);
			if (currentElem == null)
			{
				break;
			}
			Location location = currentElem.Location;
			LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
			if (locCurve == null)
			{
				break;
			}
			Curve curve = locCurve.Curve;
			Line line = (Line)(object)((curve is Line) ? curve : null);
			if ((GeometryObject)(object)line == (GeometryObject)null)
			{
				break;
			}
			double z0 = ((Curve)line).GetEndPoint(0).Z;
			double z1 = ((Curve)line).GetEndPoint(1).Z;
			double currentZMin = Math.Min(z0, z1);
			double currentZMax = Math.Max(z0, z1);
			if (level2.Elevation <= currentZMin + 0.001 || level2.Elevation >= currentZMax - 0.001)
			{
				continue;
			}
			XYZ startPt = ((Curve)line).GetEndPoint(0);
			XYZ endPt = ((Curve)line).GetEndPoint(1);
			XYZ direction = (endPt - startPt).Normalize();
			if (Math.Abs(direction.Z) < 0.001)
			{
				continue;
			}
			double tParam = (level2.Elevation - startPt.Z) / direction.Z;
			XYZ splitPoint = startPt + tParam * direction;
			ElementId newId = BreakMEPCurve(doc, currentElem, splitPoint);
			if (newId != (ElementId)null && newId != ElementId.InvalidElementId)
			{
				cutsMade++;
				doc.Regenerate();
				Element origElem = doc.GetElement(currentId);
				Element newElem = doc.GetElement(newId);
				if (origElem != null && newElem != null)
				{
					double origMidZ = GetMidZ(origElem);
					double newMidZ = GetMidZ(newElem);
					currentId = ((origMidZ < newMidZ) ? currentId : newId);
				}
			}
		}
		return cutsMade;
	}

	private double GetMidZ(Element elem)
	{
		Location location = elem.Location;
		LocationCurve lc = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		if (lc == null)
		{
			return 0.0;
		}
		Curve curve = lc.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		if ((GeometryObject)(object)line == (GeometryObject)null)
		{
			return 0.0;
		}
		return (((Curve)line).GetEndPoint(0).Z + ((Curve)line).GetEndPoint(1).Z) / 2.0;
	}

	private ElementId BreakMEPCurve(Document doc, Element elem, XYZ splitPoint)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Invalid comparison between Unknown and I4
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between Unknown and I4
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Invalid comparison between Unknown and I4
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Invalid comparison between Unknown and I4
		try
		{
			BuiltInCategory cat = (BuiltInCategory)elem.Category.GetIdInt();
			BuiltInCategory val = cat;
			BuiltInCategory val2 = val;
			if ((int)val2 <= -2008130)
			{
				if ((int)val2 == -2008132)
				{
					return BreakConduit(doc, (Conduit)(object)((elem is Conduit) ? elem : null), splitPoint);
				}
				if ((int)val2 == -2008130)
				{
					return BreakCableTray(doc, (CableTray)(object)((elem is CableTray) ? elem : null), splitPoint);
				}
			}
			else
			{
				if ((int)val2 == -2008044)
				{
					return PlumbingUtils.BreakCurve(doc, elem.Id, splitPoint);
				}
				if ((int)val2 == -2008000)
				{
					return MechanicalUtils.BreakCurve(doc, elem.Id, splitPoint);
				}
			}
			return ElementId.InvalidElementId;
		}
		catch
		{
			return ElementId.InvalidElementId;
		}
	}

	private ElementId BreakConduit(Document doc, Conduit conduit, XYZ splitPoint)
	{
		if (conduit == null)
		{
			return ElementId.InvalidElementId;
		}
		Location location = ((Element)conduit).Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locCurve.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		ElementId conduitTypeId = ((Element)conduit).GetTypeId();
		Parameter obj = ((Element)conduit).get_Parameter((BuiltInParameter)(-1114000));
		ElementId levelId = ((obj != null) ? obj.AsElementId() : null) ?? ElementId.InvalidElementId;
		Parameter obj2 = ((Element)conduit).get_Parameter((BuiltInParameter)(-1140123));
		double diameter = ((obj2 != null) ? obj2.AsDouble() : 0.0);
		locCurve.Curve = (Curve)(object)Line.CreateBound(sp, splitPoint);
		Conduit newConduit = Conduit.Create(doc, conduitTypeId, splitPoint, ep, levelId);
		if (diameter > 0.0)
		{
			Parameter diaParam = ((Element)newConduit).get_Parameter((BuiltInParameter)(-1140123));
			if (diaParam != null && !((APIObject)diaParam).IsReadOnly)
			{
				diaParam.Set(diameter);
			}
		}
		return ((Element)newConduit).Id;
	}

	private ElementId BreakCableTray(Document doc, CableTray tray, XYZ splitPoint)
	{
		if (tray == null)
		{
			return ElementId.InvalidElementId;
		}
		Location location = ((Element)tray).Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locCurve.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		ElementId trayTypeId = ((Element)tray).GetTypeId();
		Parameter obj = ((Element)tray).get_Parameter((BuiltInParameter)(-1114000));
		ElementId levelId = ((obj != null) ? obj.AsElementId() : null) ?? ElementId.InvalidElementId;
		Parameter obj2 = ((Element)tray).get_Parameter((BuiltInParameter)(-1140122));
		double width = ((obj2 != null) ? obj2.AsDouble() : 0.0);
		Parameter obj3 = ((Element)tray).get_Parameter((BuiltInParameter)(-1140121));
		double height = ((obj3 != null) ? obj3.AsDouble() : 0.0);
		locCurve.Curve = (Curve)(object)Line.CreateBound(sp, splitPoint);
		CableTray newTray = CableTray.Create(doc, trayTypeId, splitPoint, ep, levelId);
		if (width > 0.0)
		{
			((Element)newTray).get_Parameter((BuiltInParameter)(-1140122)).Set(width);
		}
		if (height > 0.0)
		{
			((Element)newTray).get_Parameter((BuiltInParameter)(-1140121)).Set(height);
		}
		return ((Element)newTray).Id;
	}

	private bool AssignReferenceLevel(Document doc, Element elem, List<Level> allLevels)
	{
		try
		{
			Level targetLevel = DetermineTargetLevel(doc, elem, allLevels);
			if (targetLevel == null)
			{
				return false;
			}
			Parameter pLevel = GetLevelParam(elem);
			if (pLevel == null || ((APIObject)pLevel).IsReadOnly)
			{
				return false;
			}
			ElementId currentLevelId = pLevel.AsElementId();
			if (currentLevelId == ((Element)targetLevel).Id)
			{
				return false;
			}
			Element element = doc.GetElement(currentLevelId);
			Level oldLevel = (Level)(object)((element is Level) ? element : null);
			if (oldLevel == null)
			{
				pLevel.Set(((Element)targetLevel).Id);
				return true;
			}
			double deltaZ = oldLevel.Elevation - targetLevel.Elevation;
			Parameter pOffset = GetOffsetParam(elem);
			if (pOffset != null && !((APIObject)pOffset).IsReadOnly)
			{
				double newOffset = pOffset.AsDouble() + deltaZ;
				pLevel.Set(((Element)targetLevel).Id);
				pOffset.Set(newOffset);
				Parameter pEndOffset = elem.get_Parameter((BuiltInParameter)(-1114003));
				if (pEndOffset != null && !((APIObject)pEndOffset).IsReadOnly)
				{
					pEndOffset.Set(pEndOffset.AsDouble() + deltaZ);
				}
				return true;
			}
			pLevel.Set(((Element)targetLevel).Id);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private Level DetermineTargetLevel(Document doc, Element elem, List<Level> allLevels)
	{
		double zBottom = GetElementBottomZ(elem);
		if (double.IsNaN(zBottom))
		{
			return null;
		}
		if (IsVertical(elem))
		{
			GetVerticalExtents(elem, out var zMin, out var zMax);
			Level lowestUncutCrossing = null;
			for (int i = 0; i < allLevels.Count; i++)
			{
				double levelElev = allLevels[i].Elevation;
				if (!(levelElev <= zMin + 0.001) && !(levelElev >= zMax - 0.001))
				{
					double aboveLength = zMax - levelElev;
					if (aboveLength < 3.2808398950131235 && HasFittingConnectedAtTop(elem))
					{
						lowestUncutCrossing = allLevels[i];
						break;
					}
				}
			}
			if (lowestUncutCrossing != null)
			{
				int idx = allLevels.IndexOf(lowestUncutCrossing);
				if (idx > 0)
				{
					return allLevels[idx - 1];
				}
				return allLevels[0];
			}
		}
		Level targetLevel = null;
		for (int i2 = allLevels.Count - 1; i2 >= 0; i2--)
		{
			if (allLevels[i2].Elevation <= zBottom + 0.001)
			{
				targetLevel = allLevels[i2];
				break;
			}
		}
		return targetLevel ?? allLevels.First();
	}

	private double GetElementBottomZ(Element elem)
	{
		Location location = elem.Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		if (locCurve != null)
		{
			Curve curve = locCurve.Curve;
			Line line = (Line)(object)((curve is Line) ? curve : null);
			if ((GeometryObject)(object)line != (GeometryObject)null)
			{
				return Math.Min(((Curve)line).GetEndPoint(0).Z, ((Curve)line).GetEndPoint(1).Z);
			}
		}
		Location location2 = elem.Location;
		LocationPoint locPoint = (LocationPoint)(object)((location2 is LocationPoint) ? location2 : null);
		if (locPoint != null)
		{
			return locPoint.Point.Z;
		}
		BoundingBoxXYZ bb = elem.get_BoundingBox(null);
		if (bb != null)
		{
			return bb.Min.Z;
		}
		return double.NaN;
	}

	private Parameter GetLevelParam(Element elem)
	{
		BuiltInParameter[] levelParams = new BuiltInParameter[] { BuiltInParameter.RBS_START_LEVEL_PARAM, BuiltInParameter.LEVEL_PARAM, BuiltInParameter.SCHEDULE_LEVEL_PARAM };
		foreach (BuiltInParameter bip in levelParams)
		{
			Parameter p = elem.get_Parameter(bip);
			if (p != null) return p;
		}
		return null;
	}

	private Parameter GetOffsetParam(Element elem)
	{
		BuiltInParameter[] offsetParams = new BuiltInParameter[] { BuiltInParameter.RBS_OFFSET_PARAM, BuiltInParameter.RBS_START_OFFSET_PARAM, BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM, BuiltInParameter.AUTO_JOIN_CONDITION };
		foreach (BuiltInParameter bip in offsetParams)
		{
			Parameter p = elem.get_Parameter(bip);
			if (p != null) return p;
		}
		return null;
	}

	private bool HasUncutCrossing(Element elem, List<Level> allLevels)
	{
		if (!IsVertical(elem))
		{
			return false;
		}
		GetVerticalExtents(elem, out var zMin, out var zMax);
		foreach (Level level in allLevels)
		{
			double levelElev = level.Elevation;
			if (!(levelElev <= zMin + 0.001) && !(levelElev >= zMax - 0.001))
			{
				double aboveLength = zMax - levelElev;
				if (aboveLength < 3.2808398950131235 && HasFittingConnectedAtTop(elem))
				{
					return true;
				}
			}
		}
		return false;
	}

	private List<Element> GetConnectedElements(Element elem)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected O, but got Unknown
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected O, but got Unknown
		List<Element> result = new List<Element>();
		ConnectorSet connectors = null;
		MEPCurve mepCurve = (MEPCurve)(object)((elem is MEPCurve) ? elem : null);
		if (mepCurve != null)
		{
			ConnectorManager connectorManager = mepCurve.ConnectorManager;
			connectors = ((connectorManager != null) ? connectorManager.Connectors : null);
		}
		else
		{
			FamilyInstance fi = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
			object obj;
			if (fi == null)
			{
				obj = null;
			}
			else
			{
				MEPModel mEPModel = fi.MEPModel;
				obj = ((mEPModel != null) ? mEPModel.ConnectorManager : null);
			}
			if (obj != null)
			{
				connectors = fi.MEPModel.ConnectorManager.Connectors;
			}
		}
		if (connectors == null)
		{
			return result;
		}
		foreach (Connector item in connectors)
		{
			Connector connector = item;
			if (!connector.IsConnected)
			{
				continue;
			}
			ConnectorSet refs = connector.AllRefs;
			foreach (Connector item2 in refs)
			{
				Connector refConn = item2;
				Element owner = refConn.Owner;
				if (owner != null && owner.Id != elem.Id)
				{
					result.Add(owner);
				}
			}
		}
		return result;
	}

	private int PropagateReferenceLevel(Document doc, Element startVerticalPipe, Level targetLevel)
	{
		int count = 0;
		HashSet<ElementId> visited = new HashSet<ElementId>();
		Queue<Element> queue = new Queue<Element>();
		visited.Add(startVerticalPipe.Id);
		foreach (Element connected in GetConnectedElements(startVerticalPipe))
		{
			if (!visited.Contains(connected.Id))
			{
				queue.Enqueue(connected);
				visited.Add(connected.Id);
			}
		}
		while (queue.Count > 0)
		{
			Element current = queue.Dequeue();
			if (IsVertical(current))
			{
				continue;
			}
			if (SetReferenceLevel(doc, current, targetLevel))
			{
				count++;
			}
			foreach (Element connected2 in GetConnectedElements(current))
			{
				if (!visited.Contains(connected2.Id))
				{
					queue.Enqueue(connected2);
					visited.Add(connected2.Id);
				}
			}
		}
		return count;
	}

	private bool SetReferenceLevel(Document doc, Element elem, Level targetLevel)
	{
		try
		{
			Parameter pLevel = GetLevelParam(elem);
			if (pLevel == null || ((APIObject)pLevel).IsReadOnly)
			{
				return false;
			}
			ElementId currentLevelId = pLevel.AsElementId();
			if (currentLevelId == ((Element)targetLevel).Id)
			{
				return false;
			}
			Element element = doc.GetElement(currentLevelId);
			Level oldLevel = (Level)(object)((element is Level) ? element : null);
			if (oldLevel == null)
			{
				pLevel.Set(((Element)targetLevel).Id);
				return true;
			}
			double deltaZ = oldLevel.Elevation - targetLevel.Elevation;
			Parameter pOffset = GetOffsetParam(elem);
			if (pOffset != null && !((APIObject)pOffset).IsReadOnly)
			{
				double newOffset = pOffset.AsDouble() + deltaZ;
				pLevel.Set(((Element)targetLevel).Id);
				pOffset.Set(newOffset);
				Parameter pEndOffset = elem.get_Parameter((BuiltInParameter)(-1114003));
				if (pEndOffset != null && !((APIObject)pEndOffset).IsReadOnly)
				{
					pEndOffset.Set(pEndOffset.AsDouble() + deltaZ);
				}
				return true;
			}
			pLevel.Set(((Element)targetLevel).Id);
			return true;
		}
		catch
		{
			return false;
		}
	}

	static CutMEPByLevel()
	{
		CuttableCategories = new BuiltInCategory[] { BuiltInCategory.OST_PipeCurves, BuiltInCategory.OST_DuctCurves, BuiltInCategory.OST_CableTray, BuiltInCategory.OST_Conduit };
		AssignOnlyCategories = new BuiltInCategory[] { BuiltInCategory.OST_PipeFitting, BuiltInCategory.OST_DuctFitting, BuiltInCategory.OST_CableTrayFitting, BuiltInCategory.OST_ConduitFitting, BuiltInCategory.OST_PipeAccessory, BuiltInCategory.OST_DuctAccessory, BuiltInCategory.OST_DuctTerminal, BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_Sprinklers, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_LightingDevices, BuiltInCategory.OST_ElectricalEquipment, BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_DataDevices, BuiltInCategory.OST_CommunicationDevices, BuiltInCategory.OST_FireAlarmDevices, BuiltInCategory.OST_SecurityDevices, BuiltInCategory.OST_NurseCallDevices };
	}
}
