using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class FlexDuctAvoidMepCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_038c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0371: Unknown result type (might be due to invalid IL or missing references)
		//IL_0326: Unknown result type (might be due to invalid IL or missing references)
		//IL_032d: Expected O, but got Unknown
		//IL_0330: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		double currentDistanceMm = 100.0;
		while (true)
		{
			AvoidMepWindow ui = new AvoidMepWindow();
			ui.txtDistance.Text = currentDistanceMm.ToString();
			if (ui.ShowDialog() != true)
			{
				break;
			}
			currentDistanceMm = ui.Distance;
			int nPoints = ui.PointsCount;
			bool isAvoidUp = ui.IsAvoidUp;
			while (true)
			{
				try
				{
					Reference obstacleRef = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new AvoidMep_ObstacleFilter(), "Chọn Vật cản để né (Nhấn Esc để quay lại bảng cài đặt)");
					Element obstacle = doc.GetElement(obstacleRef);
					Location obstacleLoc = obstacle.Location;
					if (obstacleLoc == null)
					{
						continue;
					}
					double obstacleHalfHeight = GetElementHalfHeight(obstacle);
					Reference flexRef = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new AvoidMep_FlexDuctFilter(), "Chọn Flex Duct cần điều chỉnh (Nhấn Esc để quay lại bảng cài đặt)");
					Element element = doc.GetElement(flexRef);
					FlexDuct flexDuct = (FlexDuct)(object)((element is FlexDuct) ? element : null);
					List<XYZ> newPoints;
					double avoidDist;
					XYZ pStart;
					XYZ pEnd;
					double dist;
					XYZ cpFlex;
					XYZ cpObstacle;
					if (flexDuct != null)
					{
						double flexOuterDiam = ((Element)flexDuct).get_Parameter((BuiltInParameter)(-1114103)).AsDouble();
						double flexRadius = flexOuterDiam / 2.0;
						IList<XYZ> points = flexDuct.Points;
						newPoints = new List<XYZ>();
						double thresholdFeet = currentDistanceMm / 304.8;
						avoidDist = obstacleHalfHeight + flexRadius + thresholdFeet;
						pStart = points.First();
						pEnd = points.Last();
						Line flexLine = Line.CreateBound(pStart, pEnd);
						dist = 0.0;
						LocationCurve oc = (LocationCurve)(object)((obstacleLoc is LocationCurve) ? obstacleLoc : null);
						if (oc != null)
						{
							dist = ClosestPointsBetweenCurves((Curve)(object)flexLine, oc.Curve, out cpFlex, out cpObstacle);
							goto IL_0223;
						}
						LocationPoint op = (LocationPoint)(object)((obstacleLoc is LocationPoint) ? obstacleLoc : null);
						if (op != null)
						{
							cpObstacle = op.Point;
							XYZ v = pEnd - pStart;
							XYZ w = cpObstacle - pStart;
							double t = w.DotProduct(v) / v.DotProduct(v);
							t = Math.Max(0.0, Math.Min(1.0, t));
							cpFlex = pStart + t * v;
							dist = cpFlex.DistanceTo(cpObstacle);
							goto IL_0223;
						}
					}
					goto end_IL_0077;
					IL_0223:
					if (dist < avoidDist)
					{
						XYZ moveDir = XYZ.BasisZ;
						if (!isAvoidUp)
						{
							moveDir = -XYZ.BasisZ;
						}
						XYZ flexDir = (pEnd - pStart).Normalize();
						double bridgeHalfLength = avoidDist * 1.2;
						newPoints.Add(pStart);
						if (nPoints == 1)
						{
							XYZ pPeak = cpObstacle + moveDir * avoidDist;
							newPoints.Add(pPeak);
						}
						else if (nPoints > 1)
						{
							for (int i = 0; i < nPoints; i++)
							{
								double factor = -1.0 + 2.0 * (double)i / (double)(nPoints - 1);
								XYZ pMid = cpFlex + flexDir * (factor * bridgeHalfLength) + moveDir * avoidDist;
								newPoints.Add(pMid);
							}
						}
						newPoints.Add(pEnd);
						Transaction tr = new Transaction(doc, "Flex Duct Avoid MEP");
						try
						{
							tr.Start();
							flexDuct.Points = newPoints;
							tr.Commit();
						}
						finally
						{
							((IDisposable)tr)?.Dispose();
						}
					}
					else
					{
						TaskDialog.Show("Flex Duct Avoid", $"Không tìm thấy va chạm hoặc khoảng cách đã đủ {currentDistanceMm}mm.");
					}
					end_IL_0077:;
				}
				catch (Autodesk.Revit.Exceptions.OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					message = ex.Message;
					return Result.Failed;
				}
			}
		}
		return Result.Succeeded;
	}

	private double GetElementHalfHeight(Element elem)
	{
		Parameter pDiam = elem.get_Parameter((BuiltInParameter)(-1140238));
		if (pDiam == null || !pDiam.HasValue)
		{
			pDiam = elem.get_Parameter((BuiltInParameter)(-1114103));
		}
		if (pDiam != null && pDiam.HasValue)
		{
			return pDiam.AsDouble() / 2.0;
		}
		Parameter pHeight = elem.get_Parameter((BuiltInParameter)(-1114102));
		if (pHeight == null || !pHeight.HasValue)
		{
			pHeight = elem.get_Parameter((BuiltInParameter)(-1140121));
		}
		if (pHeight != null && pHeight.HasValue)
		{
			return pHeight.AsDouble() / 2.0;
		}
		BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
		if (bbox != null)
		{
			return (bbox.Max.Z - bbox.Min.Z) / 2.0;
		}
		return 0.0;
	}

	private double ClosestPointsBetweenCurves(Curve c1, Curve c2, out XYZ p1, out XYZ p2)
	{
		if (c1 is Line && c2 is Line)
		{
			Line l1 = (Line)(object)((c1 is Line) ? c1 : null);
			Line l2 = (Line)(object)((c2 is Line) ? c2 : null);
			XYZ a = ((Curve)l1).GetEndPoint(0);
			XYZ b = ((Curve)l1).GetEndPoint(1);
			XYZ c3 = ((Curve)l2).GetEndPoint(0);
			XYZ d = ((Curve)l2).GetEndPoint(1);
			XYZ u = b - a;
			XYZ v = d - c3;
			XYZ w = a - c3;
			double aa = u.DotProduct(u);
			double bb = u.DotProduct(v);
			double cc = v.DotProduct(v);
			double dd = u.DotProduct(w);
			double ee = v.DotProduct(w);
			double denom = aa * cc - bb * bb;
			double sc;
			double tc;
			if (denom < 1E-08)
			{
				sc = 0.0;
				tc = ((bb > cc) ? (dd / bb) : (ee / cc));
			}
			else
			{
				sc = (bb * ee - cc * dd) / denom;
				tc = (aa * ee - bb * dd) / denom;
			}
			sc = Math.Max(0.0, Math.Min(1.0, sc));
			tc = Math.Max(0.0, Math.Min(1.0, tc));
			p1 = a + sc * u;
			p2 = c3 + tc * v;
			return p1.DistanceTo(p2);
		}
		p1 = c1.Evaluate(0.5, true);
		IntersectionResult ir = c2.Project(p1);
		p2 = ir.XYZPoint;
		return p1.DistanceTo(p2);
	}
}
