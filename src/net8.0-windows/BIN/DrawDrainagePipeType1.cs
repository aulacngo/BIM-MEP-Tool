using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class DrawDrainagePipeType1 : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_052d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Expected O, but got Unknown
		//IL_0531: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Expected O, but got Unknown
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0376: Expected O, but got Unknown
		//IL_0405: Unknown result type (might be due to invalid IL or missing references)
		//IL_040c: Expected O, but got Unknown
		//IL_0466: Unknown result type (might be due to invalid IL or missing references)
		//IL_046d: Expected O, but got Unknown
		//IL_0496: Unknown result type (might be due to invalid IL or missing references)
		//IL_049d: Expected O, but got Unknown
		//IL_0505: Unknown result type (might be due to invalid IL or missing references)
		//IL_051c: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		View view = doc.ActiveView;
		try
		{
			double offsetElbow = DrainagePipeOffsets.GetOffsetElbowOrDefault(150.0);
			double offsetY = DrainagePipeOffsets.GetOffsetYOrDefault(150.0);
			Reference refVertical = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectPipes(), "Chọn ống đứng");
			Reference refMain = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectPipes(), "Chọn ống chính (có dốc)");
			Element element = doc.GetElement(refVertical);
			Pipe vPipe = (Pipe)(object)((element is Pipe) ? element : null);
			Element element2 = doc.GetElement(refMain);
			Pipe mPipe = (Pipe)(object)((element2 is Pipe) ? element2 : null);
			ElementId pipeTypeId = ((Element)vPipe).GetTypeId();
			ElementId systemTypeId = ((Element)vPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
			Parameter slopeParam = ((Element)mPipe).get_Parameter((BuiltInParameter)(-1140256));
			double slope = slopeParam.AsDouble();
			ElementId levelId = ((Element)mPipe).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
			Transaction trans = new Transaction(doc, "BIN_Connect_45_Degrees");
			try
			{
				trans.Start();
				Location location = ((Element)mPipe).Location;
				Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
				Line mLine = (Line)(object)((curve is Line) ? curve : null);
				XYZ p0 = ((Curve)mLine).GetEndPoint(0);
				XYZ p1 = ((Curve)mLine).GetEndPoint(1);
				XYZ sp = null;
				XYZ ep = null;
				if (p0.Z > p1.Z)
				{
					sp = p0;
					ep = p1;
				}
				else
				{
					sp = p1;
					ep = p0;
				}
				Line slopemLine = Line.CreateBound(sp, ep);
				XYZ mainDir = (ep - sp).Normalize();
				Location location2 = ((Element)vPipe).Location;
				Curve curve2 = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve;
				Line vLine = (Line)(object)((curve2 is Line) ? curve2 : null);
				XYZ point0 = ((Curve)vLine).GetEndPoint(0);
				XYZ point1 = ((Curve)vLine).GetEndPoint(1);
				XYZ vStartPoint = null;
				XYZ vEndPoint = null;
				if (point0.Z > point1.Z)
				{
					vStartPoint = point0;
					vEndPoint = point1;
				}
				else
				{
					vStartPoint = point1;
					vEndPoint = point0;
				}
				XYZ newep = new XYZ(ep.X, ep.Y, sp.Z);
				XYZ newdir = (newep - sp).Normalize();
				Plane planeE = Plane.CreateByNormalAndOrigin(newdir, vEndPoint);
				XYZ intersectPoint = LineIntersectPlan(mLine, planeE);
				Plane planeI = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, intersectPoint);
				XYZ newEndPoint = ProjectPointToPlane(vEndPoint, planeI);
				List<Pipe> listNewMainPipes = SplitPipeAtSinglePoint(doc, mPipe, intersectPoint);
				XYZ dir = (newEndPoint - intersectPoint).Normalize();
				XYZ unRealPoint = intersectPoint + offsetY * dir;
				Pipe unRealPipe = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, intersectPoint, unRealPoint);
				double dia = ((Element)vPipe).get_Parameter((BuiltInParameter)(-1140225)).AsDouble();
				((Element)unRealPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				FamilyInstance tee = CreateTeeFittingAtPoint(listNewMainPipes[1], listNewMainPipes[0], unRealPipe, intersectPoint);
				doc.Delete(((Element)unRealPipe).Id);
				doc.Regenerate();
				((Element)tee).LookupParameter("Angle").Set(Math.PI / 4.0);
				doc.Regenerate();
				XYZ newLocationY = MovePointOnLine(intersectPoint, slopemLine, offsetY);
				Location location3 = ((Element)tee).Location;
				((LocationPoint)((location3 is LocationPoint) ? location3 : null)).Point = newLocationY;
				doc.Regenerate();
				Connector unUsedCon = GetUnuseConnector(tee.MEPModel);
				XYZ unUsedConPoint = unUsedCon.Origin;
				Location location4 = ((Element)tee).Location;
				XYZ teeOrigin = ((LocationPoint)((location4 is LocationPoint) ? location4 : null)).Point;
				Line line45toEx = Line.CreateBound(unUsedConPoint, teeOrigin);
				Line line45Extended = CreateExtendedLine(line45toEx, 1000.0);
				XYZ inter = LineIntersectWithPlane(line45Extended, planeE);
				XYZ elbow1Point = new XYZ(inter.X, inter.Y, inter.Z + slope * inter.DistanceTo(unUsedConPoint));
				Pipe pipe1 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, unUsedConPoint, elbow1Point);
				((Element)pipe1).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				Connector cn1 = FindConnectorAtPoint(pipe1, unUsedConPoint);
				cn1.ConnectTo(unUsedCon);
				ChangePipeSlope(pipe1, slope);
				doc.Regenerate();
				Location location5 = ((Element)pipe1).Location;
				Curve curve3 = ((LocationCurve)((location5 is LocationCurve) ? location5 : null)).Curve;
				Line newline1 = (Line)(object)((curve3 is Line) ? curve3 : null);
				XYZ newElbow1Point = ((Curve)newline1).GetEndPoint(1);
				XYZ vEndPointOffset = new XYZ(vEndPoint.X, vEndPoint.Y, vEndPoint.Z - 16.404199475065617);
				Line extendedLine = Line.CreateBound(vStartPoint, vEndPointOffset);
				IntersectionResult pro = ((Curve)extendedLine).Project(newElbow1Point);
				XYZ proPoint = pro.XYZPoint;
				Line horiLine = Line.CreateBound(proPoint, newElbow1Point);
				XYZ elbow1PointOffset = MovePointOnLine(proPoint, horiLine, 0.0 - offsetElbow);
				XYZ elbow2Point = new XYZ(elbow1PointOffset.X, elbow1PointOffset.Y, newElbow1Point.Z + slope * elbow1PointOffset.DistanceTo(newElbow1Point));
				Pipe pipe2 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, newElbow1Point, elbow2Point);
				XYZ elbow3Point = new XYZ(proPoint.X, proPoint.Y, proPoint.Z + offsetElbow);
				ExtendPipe(vPipe, elbow3Point);
				Pipe pipe3 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, elbow3Point, elbow2Point);
				((Element)pipe2).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				((Element)pipe3).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				MEPLibrary.CreateElbowPipeFitting(doc, pipe1, pipe2);
				MEPLibrary.CreateElbowPipeFitting(doc, pipe2, pipe3);
				MEPLibrary.CreateElbowPipeFitting(doc, pipe3, vPipe);
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

	public void ChangePipeSlope(Pipe pipe, double newSlope)
	{
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Expected O, but got Unknown
		Location location = ((Element)pipe).Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		if (locCurve == null)
		{
			return;
		}
		Curve curve = locCurve.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		if (line != null)
		{
			XYZ startPt = ((Curve)line).GetEndPoint(0);
			XYZ endPt = ((Curve)line).GetEndPoint(1);
			double horizontalLength = Math.Sqrt(Math.Pow(endPt.X - startPt.X, 2.0) + Math.Pow(endPt.Y - startPt.Y, 2.0));
			double newDeltaZ = horizontalLength * newSlope;
			XYZ newEndPt = new XYZ(endPt.X, endPt.Y, startPt.Z + newDeltaZ);
			if (startPt.DistanceTo(newEndPt) > ((Element)pipe).Document.Application.ShortCurveTolerance)
			{
				Line newCurve = Line.CreateBound(startPt, newEndPt);
				locCurve.Curve = (Curve)(object)newCurve;
			}
		}
	}

	private static Connector GetUnuseConnector(MEPModel teeFitting)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		ConnectorSet cns = teeFitting.ConnectorManager.UnusedConnectors;
		if (cns.Size > 0)
		{
			{
				IEnumerator enumerator = cns.GetEnumerator();
				try
				{
					if (enumerator.MoveNext())
					{
						return (Connector)enumerator.Current;
					}
				}
				finally
				{
					IDisposable disposable = enumerator as IDisposable;
					if (disposable != null)
					{
						disposable.Dispose();
					}
				}
			}
		}
		return null;
	}

	private FamilyInstance CreateTeeFittingAtPoint(Pipe pipe1, Pipe pipe2, Pipe pipe3, XYZ point)
	{
		Connector cn1 = FindConnectorAtPoint(pipe1, point);
		Connector cn2 = FindConnectorAtPoint(pipe2, point);
		Connector cn3 = FindConnectorAtPoint(pipe3, point);
		if (cn1 == null || cn2 == null || cn3 == null)
		{
			return null;
		}
		return ((Element)pipe1).Document.Create.NewTeeFitting(cn1, cn2, cn3);
	}

	private Connector FindConnectorAtPoint(Pipe pipe, XYZ point)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		ConnectorSet cns = ((MEPCurve)pipe).ConnectorManager.Connectors;
		foreach (Connector item in cns)
		{
			Connector cn = item;
			double dis = Math.Round(cn.Origin.DistanceTo(point) * 304.8, 3);
			if (dis == 0.0)
			{
				return cn;
			}
		}
		return null;
	}

	private void ExtendPipe(Pipe pipe, XYZ pointToExtend)
	{
		Location location = ((Element)pipe).Location;
		LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locationCurve.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		double distance1 = pointToExtend.DistanceTo(sp);
		double distance2 = pointToExtend.DistanceTo(ep);
		XYZ dir1 = line.Direction;
		XYZ dir2 = pointToExtend - sp;
		double radian = dir1.AngleTo(dir2);
		double degree = Math.Round(radian * 180.0 / Math.PI, 3);
		if (degree == 180.0)
		{
			line = ((!(distance1 > distance2)) ? Line.CreateBound(pointToExtend, ep) : Line.CreateBound(pointToExtend, sp));
		}
		if (degree == 0.0)
		{
			line = ((!(distance1 > distance2)) ? Line.CreateBound(ep, pointToExtend) : Line.CreateBound(sp, pointToExtend));
		}
		Location location2 = ((Element)pipe).Location;
		((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve = (Curve)(object)line;
	}

	private static XYZ ProjectPointToPlane(XYZ point, Plane plane)
	{
		if (plane == null)
		{
			throw new ArgumentNullException("plane");
		}
		UV uv = default(UV);
		double dist = default(double);
		((Surface)plane).Project(point, out uv, out dist);
		return plane.Origin + uv.U * plane.XVec + uv.V * plane.YVec;
	}

	private static XYZ LineIntersectPlan(Line line, Plane plane)
	{
		XYZ normal = plane.Normal;
		XYZ origin = plane.Origin;
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		XYZ normalize = (ep - sp).Normalize();
		double distance = (normal.DotProduct(origin) - normal.DotProduct(sp)) / normal.DotProduct(normalize);
		return sp + distance * normalize;
	}

	private XYZ MovePointOnLine(XYZ point, Line line, double distance)
	{
		XYZ p0 = ((Curve)line).GetEndPoint(0);
		XYZ p1 = ((Curve)line).GetEndPoint(1);
		XYZ sp = null;
		XYZ ep = null;
		if (p0.Z > p1.Z)
		{
			sp = p0;
			ep = p1;
		}
		else
		{
			sp = p1;
			ep = p0;
		}
		XYZ direction = (ep - sp).Normalize();
		return point + direction * distance;
	}

	private List<Pipe> SplitPipeAtSinglePoint(Document doc, Pipe pipe, XYZ splitPoint)
	{
		List<Pipe> listNewPipes = new List<Pipe>();
		try
		{
			Location location = ((Element)pipe).Location;
			LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
			XYZ projectedPoint = locCurve.Curve.Project(splitPoint).XYZPoint;
			ElementId newPipeId = PlumbingUtils.BreakCurve(doc, ((Element)pipe).Id, projectedPoint);
			if (newPipeId != ElementId.InvalidElementId)
			{
				listNewPipes.Add(pipe);
				Element element = doc.GetElement(newPipeId);
				listNewPipes.Add((Pipe)(object)((element is Pipe) ? element : null));
			}
		}
		catch
		{
		}
		return listNewPipes;
	}

	private static Line CreateExtendedLine(Line line, double dist)
	{
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		XYZ vector = (ep - sp).Normalize();
		XYZ spN = sp - vector * dist;
		XYZ epN = ep + vector * dist;
		return Line.CreateBound(spN, epN);
	}

	private static XYZ LineIntersectWithPlane(Line line, Plane plane)
	{
		XYZ planePoint = plane.Origin;
		XYZ planeNormal = plane.Normal;
		XYZ linePoint = ((Curve)line).GetEndPoint(0);
		XYZ lineDirection = (((Curve)line).GetEndPoint(1) - linePoint).Normalize();
		double lineParameter = (lineParameter = (planeNormal.DotProduct(planePoint) - planeNormal.DotProduct(linePoint)) / planeNormal.DotProduct(lineDirection));
		return linePoint + lineParameter * lineDirection;
	}
}
