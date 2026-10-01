using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class DrawCleanOut : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0402: Unknown result type (might be due to invalid IL or missing references)
		//IL_0413: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0417: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Expected O, but got Unknown
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Expected O, but got Unknown
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0206: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Expected O, but got Unknown
		//IL_0358: Unknown result type (might be due to invalid IL or missing references)
		//IL_035f: Expected O, but got Unknown
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		DrawCleanOutWindow ui = new DrawCleanOutWindow();
		if (ui.ShowDialog() != true)
		{
			return Result.Cancelled;
		}
		try
		{
			IList<Reference> refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectPipes(), "Chọn các ống để tạo Clean Out");
			if (refs == null || refs.Count == 0)
			{
				return Result.Cancelled;
			}
			FamilySymbol cleanOutSymbol = ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))).Cast<FamilySymbol>().FirstOrDefault((FamilySymbol x) => ((ElementType)x).FamilyName.Contains("CLEAN OUT") && ((Element)x).Name.Contains("CLEAN OUT"));
			Transaction trans = new Transaction(doc, "Create Multiple Clean Outs");
			try
			{
				trans.Start();
				if (cleanOutSymbol != null && !cleanOutSymbol.IsActive)
				{
					cleanOutSymbol.Activate();
				}
				Level currentLevel = default(Level);
				foreach (Reference r in refs)
				{
					Element element = doc.GetElement(r);
					Pipe pipe = (Pipe)(object)((element is Pipe) ? element : null);
					if (pipe == null)
					{
						continue;
					}
					double dia = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140225)).AsDouble();
					double offsetE = ui.OffsetElbow;
					ElementId pipeTypeId = ((Element)pipe).GetTypeId();
					ElementId systemTypeId = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
					ElementId levelId = ((Element)pipe).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
					Connector cnnUnused = GetUnusedConnector((Element)(object)pipe);
					Connector cnnUsed = GetUsedConnector((Element)(object)pipe);
					if (cnnUnused == null || cnnUsed == null)
					{
						continue;
					}
					XYZ elbowPoint = cnnUnused.Origin;
					XYZ lowPoint = cnnUsed.Origin;
					XYZ lowPointZ = new XYZ(lowPoint.X, lowPoint.Y, elbowPoint.Z);
					XYZ mainDir = (elbowPoint - lowPointZ).Normalize();
					XYZ point90 = MovePointFollowDirection(elbowPoint, mainDir, offsetE);
					if (ui.SelectedType == 0)
					{
						XYZ endPoint = new XYZ(point90.X, point90.Y, point90.Z + offsetE);
						Pipe pipe2 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, elbowPoint, endPoint);
						((Element)pipe2).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
						MEPLibrary.CreateElbowPipeFitting(doc, pipe2, pipe);
						PlumbingUtils.PlaceCapOnOpenEnds(doc, ((Element)pipe2).Id, pipeTypeId);
						continue;
					}
					XYZ elbowPoint2 = new XYZ(point90.X, point90.Y, point90.Z + offsetE);
					Pipe pipe3 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, elbowPoint, elbowPoint2);
					((Element)pipe3).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
					List<Level> allLevels = (from Level l in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(Level))
						orderby l.Elevation
						select l).ToList();
					ref Level reference = ref currentLevel;
					Element element2 = doc.GetElement(levelId);
					reference = (Level)(object)((element2 is Level) ? element2 : null);
					int currentIndex = allLevels.FindIndex((Level l) => ((Element)l).Id == ((Element)currentLevel).Id);
					Level nextLevel = null;
					Plane nextLevelPlane = null;
					if (currentIndex != -1 && currentIndex < allLevels.Count - 1)
					{
						nextLevel = allLevels[currentIndex + 1];
						XYZ origin = new XYZ(0.0, 0.0, nextLevel.Elevation);
						nextLevelPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, origin);
					}
					XYZ endPoint2 = ProjectPointToPlane(elbowPoint2, nextLevelPlane);
					Pipe pipe4 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, elbowPoint2, endPoint2);
					((Element)pipe4).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
					MEPLibrary.CreateElbowPipeFitting(doc, pipe3, pipe);
					MEPLibrary.CreateElbowPipeFitting(doc, pipe3, pipe4);
					PlumbingUtils.PlaceCapOnOpenEnds(doc, ((Element)pipe4).Id, pipeTypeId);
				}
				trans.Commit();
			}
			finally
			{
				((IDisposable)trans)?.Dispose();
			}
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private Connector GetUsedConnector(Element element)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Invalid comparison between Unknown and I4
		ConnectorSet connectors = GetConnectors(element);
		if (connectors == null)
		{
			return null;
		}
		foreach (Connector item in connectors)
		{
			Connector c = item;
			if ((int)c.ConnectorType != 4 && c.IsConnected)
			{
				return c;
			}
		}
		return null;
	}

	private Connector GetUnusedConnector(Element element)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Invalid comparison between Unknown and I4
		ConnectorSet connectors = GetConnectors(element);
		if (connectors == null)
		{
			return null;
		}
		foreach (Connector item in connectors)
		{
			Connector c = item;
			if ((int)c.ConnectorType != 4 && !c.IsConnected)
			{
				return c;
			}
		}
		return null;
	}

	private ConnectorSet GetConnectors(Element element)
	{
		MEPCurve curve = (MEPCurve)(object)((element is MEPCurve) ? element : null);
		if (curve != null)
		{
			return curve.ConnectorManager.Connectors;
		}
		FamilyInstance fi = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
		if (fi != null && fi.MEPModel != null)
		{
			return fi.MEPModel.ConnectorManager.Connectors;
		}
		return null;
	}

	public static XYZ MovePointFollowDirection(XYZ point, XYZ direction, double distance)
	{
		if (direction.IsZeroLength())
		{
			return point;
		}
		return point + direction.Normalize() * distance;
	}

	private static XYZ ProjectPointToPlane(XYZ point, Plane plane)
	{
		if (plane == null)
		{
			throw new System.ArgumentNullException("plane");
		}
		UV uv = default(UV);
		double dist = default(double);
		((Surface)plane).Project(point, out uv, out dist);
		return plane.Origin + uv.U * plane.XVec + uv.V * plane.YVec;
	}
}
