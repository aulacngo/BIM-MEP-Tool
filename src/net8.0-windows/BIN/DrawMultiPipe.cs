using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class DrawMultiPipe : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_06da: Unknown result type (might be due to invalid IL or missing references)
		//IL_06de: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Expected O, but got Unknown
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Invalid comparison between Unknown and I4
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected O, but got Unknown
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Invalid comparison between Unknown and I4
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Expected O, but got Unknown
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_032e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		//IL_036c: Expected O, but got Unknown
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		//IL_038b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0392: Expected O, but got Unknown
		//IL_0482: Unknown result type (might be due to invalid IL or missing references)
		//IL_0489: Expected O, but got Unknown
		//IL_069f: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_05fd: Expected O, but got Unknown
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		View view = doc.ActiveView;
		TaskDialog.Show("Hướng dẫn", "Vui lòng quét chọn ống, sau đó click vào vùng tạo ra ống mới");
		IList<Element> listPipes = uidoc.Selection.PickElementsByRectangle((ISelectionFilter)(object)new PipeFilter(), "Select Pipes");
		IList<Element> selectedPipes = new List<Element>();
		if (listPipes.Count > 0)
		{
			selectedPipes = listPipes.ToList();
			if (selectedPipes.Count > 0)
			{
				while (true)
				{
					try
					{
						XYZ pickPoint = uidoc.Selection.PickPoint();
						if (IsPointInside(selectedPipes, pickPoint))
						{
							Transaction t = new Transaction(doc, "Extend Multi Pipes");
							try
							{
								t.Start();
								List<XYZ> listXYZ = GetListPoint(selectedPipes, pickPoint);
								for (int i = 0; i < selectedPipes.Count; i++)
								{
									Element obj = selectedPipes[i];
									Pipe pipe = (Pipe)(object)((obj is Pipe) ? obj : null);
									XYZ pointToExtend = listXYZ[i];
									ExtendPipes(pipe, pointToExtend);
								}
								t.Commit();
							}
							finally
							{
								((IDisposable)t)?.Dispose();
							}
							continue;
						}
						Pipe nearestPipe;
						XYZ nearestPoint = GetNearestPoint(selectedPipes, pickPoint, out nearestPipe);
						TaskDialog td = new TaskDialog("Select Turn Angle");
						td.MainInstruction = "Select the turning angle";
						td.AddCommandLink((TaskDialogCommandLinkId)1001, "45 Degrees");
						td.AddCommandLink((TaskDialogCommandLinkId)1002, "90 Degrees");
						TaskDialogResult result = td.Show();
						double angleDegree = 90.0;
						if ((int)result == 1001)
						{
							angleDegree = 45.0;
							goto IL_01c4;
						}
						if ((int)result == 1002)
						{
							angleDegree = 90.0;
							goto IL_01c4;
						}
						goto end_IL_007d;
						IL_01c4:
						Location location = ((Element)nearestPipe).Location;
						LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
						Curve curve = locCurve.Curve;
						Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
						XYZ pipeDir = pipeLine.Direction;
						XYZ flatPipeDir = new XYZ(pipeDir.X, pipeDir.Y, 0.0).Normalize();
						XYZ clickDir = new XYZ(pickPoint.X - nearestPoint.X, pickPoint.Y - nearestPoint.Y, 0.0);
						if (clickDir.IsZeroLength())
						{
							break;
						}
						clickDir = clickDir.Normalize();
						double angleRad = angleDegree * Math.PI / 180.0;
						Transform rotRight = Transform.CreateRotation(XYZ.BasisZ, angleRad);
						Transform rotLeft = Transform.CreateRotation(XYZ.BasisZ, 0.0 - angleRad);
						XYZ dirRight = rotRight.OfVector(flatPipeDir);
						XYZ dirLeft = rotLeft.OfVector(flatPipeDir);
						XYZ D2 = ((dirRight.AngleTo(clickDir) < dirLeft.AngleTo(clickDir)) ? dirRight : dirLeft);
						XYZ D3 = flatPipeDir;
						XYZ T1 = XYZ.BasisZ.CrossProduct(D3).Normalize();
						XYZ T2 = XYZ.BasisZ.CrossProduct(D2).Normalize();
						double cross = D3.X * D2.Y - D3.Y * D2.X;
						double planDist = new XYZ(pickPoint.X - nearestPoint.X, pickPoint.Y - nearestPoint.Y, 0.0).GetLength();
						if (planDist < 1.0)
						{
							planDist = 5.0;
						}
						IList<Element> newList = new List<Element>();
						Transaction t2 = new Transaction(doc, "Draw Multi Turn Pipes");
						try
						{
							t2.Start();
							foreach (Pipe item in selectedPipes)
							{
								Pipe pipe_i = item;
								Location location2 = ((Element)pipe_i).Location;
								LocationCurve lc = (LocationCurve)(object)((location2 is LocationCurve) ? location2 : null);
								XYZ p_sp = lc.Curve.GetEndPoint(0);
								XYZ p_ep = lc.Curve.GetEndPoint(1);
								double d = (p_sp - nearestPoint).DotProduct(T1);
								XYZ A = nearestPoint + d * T1;
								XYZ B = nearestPoint + d * T2;
								XYZ diff = B - A;
								double t3 = 0.0;
								if (Math.Abs(cross) > 1E-06)
								{
									t3 = (diff.X * D2.Y - diff.Y * D2.X) / cross;
								}
								XYZ intersection2D = A + t3 * D3;
								XYZ origDir = (p_ep - p_sp).Normalize();
								XYZ origDirFl = new XYZ(origDir.X, origDir.Y, 0.0);
								double origPlanLen = origDirFl.GetLength();
								double t_z = 0.0;
								if (origPlanLen > 1E-06)
								{
									origDirFl = origDirFl.Normalize();
									t_z = ((!(Math.Abs(origDirFl.X) > Math.Abs(origDirFl.Y))) ? ((intersection2D.Y - p_sp.Y) / origDir.Y) : ((intersection2D.X - p_sp.X) / origDir.X));
								}
								XYZ turnPoint3D = p_sp + origDir * t_z;
								ExtendPipes(pipe_i, turnPoint3D);
								ElementId systemTypeId = ((Element)pipe_i).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
								ElementId levelId = ((Element)pipe_i).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
								double diameter = ((Element)pipe_i).get_Parameter((BuiltInParameter)(-1140225)).AsDouble();
								ElementId typeId = ((Element)pipe_i).GetTypeId();
								double Z_slope = 0.0;
								if (origPlanLen > 1E-06)
								{
									Z_slope = origDir.Z / origPlanLen;
								}
								double dirSign = ((origDirFl.DotProduct(D3) > 0.0) ? 1.0 : (-1.0));
								double newZ_ep = turnPoint3D.Z + Z_slope * dirSign * planDist;
								XYZ endPt2D = intersection2D + D2 * planDist;
								XYZ ep3D = new XYZ(endPt2D.X, endPt2D.Y, newZ_ep);
								Pipe newPipe = Pipe.Create(doc, systemTypeId, typeId, levelId, turnPoint3D, ep3D);
								((Element)newPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(diameter);
								Parameter pSlope = ((Element)newPipe).get_Parameter((BuiltInParameter)(-1140256));
								Parameter opSlope = ((Element)pipe_i).get_Parameter((BuiltInParameter)(-1140256));
								if (pSlope != null && opSlope != null && !((APIObject)pSlope).IsReadOnly)
								{
									pSlope.Set(opSlope.AsDouble());
								}
								CreateElbowFiting(doc, newPipe, pipe_i);
								newList.Add((Element)(object)newPipe);
							}
							t2.Commit();
						}
						finally
						{
							((IDisposable)t2)?.Dispose();
						}
						selectedPipes.Clear();
						selectedPipes = new List<Element>(newList);
						continue;
						end_IL_007d:;
					}
					catch
					{
					}
					break;
				}
			}
		}
		return Result.Succeeded;
	}

	private XYZ LineIntersectPlane(Line line, Plane plane)
	{
		XYZ lineOrigin = ((Curve)line).GetEndPoint(0);
		XYZ lineDir = line.Direction;
		double denominator = lineDir.DotProduct(plane.Normal);
		if (Math.Abs(denominator) < 1E-09)
		{
			return null;
		}
		double t = (plane.Origin - lineOrigin).DotProduct(plane.Normal) / denominator;
		return lineOrigin + t * lineDir;
	}

	private Line CreateExtendLine(Line line, double distance)
	{
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		XYZ normalize = (ep - sp).Normalize();
		XYZ nsp = sp - normalize * distance;
		XYZ nep = ep + normalize * distance;
		return Line.CreateBound(nsp, nep);
	}

	private List<XYZ> GetListPoint(IList<Element> listPipes, XYZ pickPoint)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		Element obj = listPipes[0];
		Pipe pipe0 = (Pipe)(object)((obj is Pipe) ? obj : null);
		Location location = ((Element)pipe0).Location;
		LocationCurve lc = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = lc.Curve;
		Line line0 = (Line)(object)((curve is Line) ? curve : null);
		XYZ flatDir = new XYZ(line0.Direction.X, line0.Direction.Y, 0.0).Normalize();
		Plane plane = Plane.CreateByNormalAndOrigin(flatDir, pickPoint);
		List<XYZ> listXYZ = new List<XYZ>();
		foreach (Element element in listPipes)
		{
			Pipe pipe1 = (Pipe)(object)((element is Pipe) ? element : null);
			Location location2 = ((Element)pipe1).Location;
			LocationCurve locationCurve = (LocationCurve)(object)((location2 is LocationCurve) ? location2 : null);
			Curve curve2 = locationCurve.Curve;
			Line line1 = (Line)(object)((curve2 is Line) ? curve2 : null);
			Line newLine = CreateExtendLine(line1, 500.0);
			XYZ intersectPoint = LineIntersectPlane(newLine, plane);
			listXYZ.Add(intersectPoint);
		}
		return listXYZ;
	}

	private double GetMaxDistance(IList<Element> listPipes, XYZ pickPoint)
	{
		List<XYZ> listXYZ = GetListPoint(listPipes, pickPoint);
		List<double> listDistance = new List<double>();
		for (int i = 0; i < listXYZ.Count; i++)
		{
			XYZ p1 = listXYZ[i];
			foreach (XYZ p2 in listXYZ)
			{
				double distance = p2.DistanceTo(p1);
				listDistance.Add(distance);
			}
		}
		return listDistance.Max();
	}

	private bool IsPointInside(IList<Element> listPipes, XYZ pickPoint)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Expected O, but got Unknown
		double max = GetMaxDistance(listPipes, pickPoint);
		List<XYZ> listXYZ = GetListPoint(listPipes, pickPoint);
		XYZ point1 = new XYZ();
		XYZ point2 = new XYZ();
		for (int i = 0; i < listXYZ.Count; i++)
		{
			XYZ p1 = listXYZ[i];
			foreach (XYZ p2 in listXYZ)
			{
				double distance = p2.DistanceTo(p1);
				double value = Math.Round(max - distance, 0);
				if (value == 0.0)
				{
					point1 = p1;
					point2 = p2;
					break;
				}
			}
		}
		XYZ pZ = new XYZ(pickPoint.X, pickPoint.Y, point1.Z);
		double maxDistance = pZ.DistanceTo(point1) + pZ.DistanceTo(point2);
		if (Math.Round(maxDistance - max, 0) == 0.0)
		{
			return true;
		}
		return false;
	}

	private void ExtendPipes(Pipe pipe, XYZ pointToExtend)
	{
		Location location = ((Element)pipe).Location;
		LocationCurve lc = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = lc.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		XYZ dir1 = line.Direction;
		XYZ dir2 = pointToExtend - sp;
		double radian = dir1.AngleTo(dir2);
		double degree = Math.Round(radian * 180.0 / Math.PI, 3);
		double dis1 = sp.DistanceTo(pointToExtend);
		double dis2 = ep.DistanceTo(pointToExtend);
		if (degree == 180.0)
		{
			line = ((!(dis1 > dis2)) ? Line.CreateBound(pointToExtend, ep) : Line.CreateBound(pointToExtend, sp));
		}
		if (degree == 0.0)
		{
			line = ((!(dis1 > dis2)) ? Line.CreateBound(ep, pointToExtend) : Line.CreateBound(sp, pointToExtend));
		}
		Location location2 = ((Element)pipe).Location;
		((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve = (Curve)(object)line;
	}

	private XYZ GetNearestPoint(IList<Element> listPipes, XYZ pickPoint, out Pipe nearestPipe)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Expected O, but got Unknown
		nearestPipe = null;
		List<double> listDistance = new List<double>();
		foreach (Element ele in listPipes)
		{
			Pipe pipe = (Pipe)(object)((ele is Pipe) ? ele : null);
			Connector cn = GetNearestConnector(pipe, pickPoint);
			XYZ pZ = new XYZ(pickPoint.X, pickPoint.Y, cn.Origin.Z);
			double distance = pZ.DistanceTo(cn.Origin);
			listDistance.Add(distance);
		}
		double min = listDistance.Min();
		foreach (Element ele2 in listPipes)
		{
			Pipe pipe2 = (Pipe)(object)((ele2 is Pipe) ? ele2 : null);
			Connector cn2 = GetNearestConnector(pipe2, pickPoint);
			XYZ pZ2 = new XYZ(pickPoint.X, pickPoint.Y, cn2.Origin.Z);
			double distance2 = pZ2.DistanceTo(cn2.Origin);
			double hs = Math.Round(distance2 - min, 2);
			if (hs == 0.0)
			{
				nearestPipe = pipe2;
				return cn2.Origin;
			}
		}
		return null;
	}

	private Connector GetNearestConnector(Pipe pipe, XYZ pickPoint)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		ConnectorManager cm = ((MEPCurve)pipe).ConnectorManager;
		ConnectorSet cs = cm.Connectors;
		List<double> listDistance = new List<double>();
		foreach (Connector item in cs)
		{
			Connector cn = item;
			XYZ origin = cn.Origin;
			double distance = origin.DistanceTo(pickPoint);
			listDistance.Add(distance);
		}
		double min = listDistance.Min();
		foreach (Connector item2 in cs)
		{
			Connector cn2 = item2;
			XYZ origin2 = cn2.Origin;
			double distance2 = origin2.DistanceTo(pickPoint);
			double hs = Math.Round(distance2 - min, 3);
			if (hs == 0.0)
			{
				return cn2;
			}
		}
		return null;
	}

	private double DistancePipeToPoint(Pipe pipe, XYZ point)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0076: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Expected O, but got Unknown
		Location location = ((Element)pipe).Location;
		LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locationCurve.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		Line extendLine = CreateExtendLine(line, 500.0);
		XYZ sp = ((Curve)extendLine).GetEndPoint(0);
		XYZ ep = ((Curve)extendLine).GetEndPoint(1);
		Line flatLine = Line.CreateBound(new XYZ(sp.X, sp.Y, 0.0), new XYZ(ep.X, ep.Y, 0.0));
		return ((Curve)flatLine).Distance(new XYZ(point.X, point.Y, 0.0));
	}

	private List<double> GetListDistance(IList<Element> listPipes, XYZ pickPoint)
	{
		Pipe nearestPipe;
		XYZ nearestPoint = GetNearestPoint(listPipes, pickPoint, out nearestPipe);
		List<double> listDistance = (from ele in listPipes
			select DistancePipeToPoint((Pipe)(object)((ele is Pipe) ? ele : null), nearestPoint) into dis
			orderby dis
			select dis).ToList();
		listDistance.RemoveAt(0);
		return listDistance;
	}

	private bool IsLineIntersection(Pipe pipe, Line line)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0076: Expected O, but got Unknown
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Expected O, but got Unknown
		//IL_00db: Expected O, but got Unknown
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		Location location = ((Element)pipe).Location;
		LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locationCurve.Curve;
		Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
		Line pipeLineExtend = CreateExtendLine(pipeLine, 500.0);
		XYZ p_sp = ((Curve)pipeLineExtend).GetEndPoint(0);
		XYZ p_ep = ((Curve)pipeLineExtend).GetEndPoint(1);
		Line flatPipeLine = Line.CreateBound(new XYZ(p_sp.X, p_sp.Y, 0.0), new XYZ(p_ep.X, p_ep.Y, 0.0));
		Line extendLine = CreateExtendLine(line, 500.0);
		XYZ l_sp = ((Curve)extendLine).GetEndPoint(0);
		XYZ l_ep = ((Curve)extendLine).GetEndPoint(1);
		Line flatExtendLine = Line.CreateBound(new XYZ(l_sp.X, l_sp.Y, 0.0), new XYZ(l_ep.X, l_ep.Y, 0.0));
		IntersectionResultArray array = default(IntersectionResultArray);
		((Curve)flatExtendLine).Intersect((Curve)(object)flatPipeLine, out array);
		if (array != null && array.Size == 1)
		{
			return true;
		}
		return false;
	}

	private List<Pipe> SortedListPipe(IList<Element> listPipes, XYZ pickPoint)
	{
		List<double> listDistance = GetListDistance(listPipes, pickPoint);
		Pipe nearestPipe;
		XYZ nearestPoint = GetNearestPoint(listPipes, pickPoint, out nearestPipe);
		List<Pipe> listnewPipe = new List<Pipe>();
		foreach (double value in listDistance)
		{
			foreach (Element ele in listPipes)
			{
				double distance = DistancePipeToPoint((Pipe)(object)((ele is Pipe) ? ele : null), nearestPoint);
				double hs = Math.Round(distance - value, 3);
				if (hs == 0.0)
				{
					listnewPipe.Add((Pipe)(object)((ele is Pipe) ? ele : null));
					break;
				}
			}
		}
		listnewPipe.Insert(0, nearestPipe);
		return listnewPipe;
	}

	private XYZ PipeIntersectionLine(Pipe pipe, Line line)
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_0076: Expected O, but got Unknown
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Expected O, but got Unknown
		//IL_00db: Expected O, but got Unknown
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		Location location = ((Element)pipe).Location;
		LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locationCurve.Curve;
		Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
		Line pipeLineExtend = CreateExtendLine(pipeLine, 500.0);
		XYZ p_sp = ((Curve)pipeLineExtend).GetEndPoint(0);
		XYZ p_ep = ((Curve)pipeLineExtend).GetEndPoint(1);
		Line flatPipeLine = Line.CreateBound(new XYZ(p_sp.X, p_sp.Y, 0.0), new XYZ(p_ep.X, p_ep.Y, 0.0));
		Line extendLine = CreateExtendLine(line, 500.0);
		XYZ l_sp = ((Curve)extendLine).GetEndPoint(0);
		XYZ l_ep = ((Curve)extendLine).GetEndPoint(1);
		Line flatExtendLine = Line.CreateBound(new XYZ(l_sp.X, l_sp.Y, 0.0), new XYZ(l_ep.X, l_ep.Y, 0.0));
		IntersectionResultArray array = default(IntersectionResultArray);
		((Curve)flatExtendLine).Intersect((Curve)(object)flatPipeLine, out array);
		if (array != null && array.Size == 1)
		{
			XYZ flatPt = array.get_Item(0).XYZPoint;
			XYZ dir = pipeLineExtend.Direction;
			double t = 0.0;
			t = ((!(Math.Abs(dir.X) > Math.Abs(dir.Y))) ? ((flatPt.Y - p_sp.Y) / dir.Y) : ((flatPt.X - p_sp.X) / dir.X));
			return p_sp + dir * t;
		}
		return null;
	}

	private void CreateElbowFiting(Document doc, Pipe pipe1, Pipe pipe2)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		ConnectorManager cM1 = ((MEPCurve)pipe1).ConnectorManager;
		ConnectorManager cM2 = ((MEPCurve)pipe2).ConnectorManager;
		ConnectorSet cS1 = cM1.Connectors;
		ConnectorSet cS2 = cM2.Connectors;
		List<Connector> list = new List<Connector>();
		foreach (Connector item in cS1)
		{
			Connector c1 = item;
			foreach (Connector item2 in cS2)
			{
				Connector c2 = item2;
				XYZ o1 = c1.Origin;
				XYZ o2 = c2.Origin;
				double kc = Math.Round(o1.DistanceTo(o2), 3);
				if (kc == 0.0)
				{
					list.Add(c1);
					list.Add(c2);
					break;
				}
			}
		}
		try
		{
			doc.Create.NewElbowFitting(list[0], list[1]);
		}
		catch
		{
		}
	}
}
