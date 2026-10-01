using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class TurnDrainagePipe : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_04a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_04a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Expected O, but got Unknown
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0265: Expected O, but got Unknown
		//IL_03b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bc: Expected O, but got Unknown
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Expected O, but got Unknown
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ef: Expected O, but got Unknown
		//IL_047a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0420: Unknown result type (might be due to invalid IL or missing references)
		//IL_0427: Expected O, but got Unknown
		//IL_0491: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			TurnDrainagePipeWindow ui = new TurnDrainagePipeWindow();
			if (ui.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			double offsetElbow = ui.OffsetElbowValue / 304.8;
			double offsetPipe = ui.OffsetPipeValue / 304.8;
			string direction = ui.SelectedDirection;
			Reference r = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectPipes(), "Chọn ống để xoay");
			Element element = doc.GetElement(r);
			Pipe mainPipe = (Pipe)(object)((element is Pipe) ? element : null);
			XYZ pickPoint = r.GlobalPoint;
			Transaction trans = new Transaction(doc, "Turn Pipe Unified");
			try
			{
				trans.Start();
				XYZ turnPoint = null;
				XYZ elsePoint = null;
				List<Connector> openConnectors = (from Connector c in (IEnumerable)((MEPCurve)mainPipe).ConnectorManager.Connectors
					where !c.IsConnected
					select c).ToList();
				if (openConnectors.Count == 1)
				{
					turnPoint = openConnectors[0].Origin;
				}
				else if (openConnectors.Count >= 2)
				{
					turnPoint = openConnectors.OrderBy((Connector c) => c.Origin.DistanceTo(pickPoint)).First().Origin;
				}
				else
				{
					Location location = ((Element)mainPipe).Location;
					Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
					Line line = (Line)(object)((curve is Line) ? curve : null);
					turnPoint = ((((Curve)line).GetEndPoint(0).DistanceTo(pickPoint) < ((Curve)line).GetEndPoint(1).DistanceTo(pickPoint)) ? ((Curve)line).GetEndPoint(0) : ((Curve)line).GetEndPoint(1));
				}
				elsePoint = GetOppositeEndPoint(mainPipe, turnPoint);
				double dia = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140225)).AsDouble();
				ElementId pipeTypeId = ((Element)mainPipe).GetTypeId();
				ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
				ElementId levelId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
				double slope = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140256)).AsDouble();
				XYZ mainDir = (turnPoint - elsePoint).Normalize();
				Pipe pipe1 = null;
				Pipe pipe2 = null;
				if (direction == "UP" || direction == "DOWN")
				{
					XYZ elsePointZ = new XYZ(elsePoint.X, elsePoint.Y, turnPoint.Z);
					XYZ flatDir = (turnPoint - elsePointZ).Normalize();
					XYZ point90 = turnPoint + flatDir * offsetElbow;
					double sign = ((direction == "UP") ? 1.0 : (-1.0));
					XYZ elbow2Point = new XYZ(point90.X, point90.Y, point90.Z + sign * offsetElbow);
					XYZ endPoint = new XYZ(elbow2Point.X, elbow2Point.Y, elbow2Point.Z + sign * offsetPipe);
					pipe1 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, turnPoint, elbow2Point);
					pipe2 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, elbow2Point, endPoint);
				}
				else
				{
					double angle = ((direction == "LEFT") ? (Math.PI / 4.0) : (-Math.PI / 4.0));
					XYZ point91 = turnPoint + mainDir * offsetElbow;
					XYZ pStart = turnPoint;
					Transform rotateAngle = Transform.CreateRotation(XYZ.BasisZ, angle);
					XYZ dir45 = rotateAngle.OfVector(mainDir);
					XYZ ep45 = pStart + dir45 * (Math.Sqrt(2.0) * offsetElbow);
					pipe1 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, turnPoint, ep45);
					XYZ point90z = new XYZ(point91.X, point91.Y, ep45.Z);
					XYZ dirPipe2 = (ep45 - point90z).Normalize();
					XYZ pipe2EndNoSlope = ep45 + dirPipe2 * offsetPipe;
					double length = ep45.DistanceTo(pipe2EndNoSlope);
					double zOffset = ((turnPoint.Z < elsePoint.Z) ? ((0.0 - slope) * length) : (slope * length));
					XYZ pipe2End = new XYZ(pipe2EndNoSlope.X, pipe2EndNoSlope.Y, pipe2EndNoSlope.Z + zOffset);
					pipe2 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, ep45, pipe2End);
				}
				((Element)pipe1).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				((Element)pipe2).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				MEPLibrary.CreateElbowPipeFitting(doc, pipe1, mainPipe);
				MEPLibrary.CreateElbowPipeFitting(doc, pipe1, pipe2);
				trans.Commit();
			}
			finally
			{
				((IDisposable)trans)?.Dispose();
			}
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private XYZ GetOppositeEndPoint(Pipe pipe, XYZ currentPoint)
	{
		Location location = ((Element)pipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		XYZ p0 = curve.GetEndPoint(0);
		XYZ p1 = curve.GetEndPoint(1);
		return (p0.DistanceTo(currentPoint) < 0.001) ? p1 : p0;
	}
}
