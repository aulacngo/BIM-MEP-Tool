using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;

namespace BIN;

public static class CableTrayOffsetUltils
{
	public static void CableTrayCut45(Document doc, Reference r1, Reference r2, double offset, bool isTop)
	{
		ExecuteCableTrayOffset(doc, r1, r2, offset, isTop, 45);
	}

	public static void CableTrayCut90(Document doc, Reference r1, Reference r2, double offset, bool isTop)
	{
		ExecuteCableTrayOffset(doc, r1, r2, offset, isTop, 90);
	}

	private static void ExecuteCableTrayOffset(Document doc, Reference r1, Reference r2, double offset, bool isTop, int angle)
	{
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		//IL_0271: Expected O, but got Unknown
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0295: Expected O, but got Unknown
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Expected O, but got Unknown
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0187: Expected O, but got Unknown
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		Element element = doc.GetElement(r1.ElementId);
		CableTray tray = (CableTray)(object)((element is CableTray) ? element : null);
		if (tray == null)
		{
			return;
		}
		double width = ((Element)tray).get_Parameter((BuiltInParameter)(-1140122)).AsDouble();
		double height = ((Element)tray).get_Parameter((BuiltInParameter)(-1140121)).AsDouble();
		ElementId trayTypeId = ((Element)tray).GetTypeId();
		ElementId levelId = ((Element)tray).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
		Location location = ((Element)tray).Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locCurve.Curve;
		Line trayLine = (Line)(object)((curve is Line) ? curve : null);
		XYZ lineDir = trayLine.Direction;
		XYZ p1 = SnapToLine(((Curve)trayLine).GetEndPoint(0), lineDir, r1.GlobalPoint);
		XYZ p2 = SnapToLine(((Curve)trayLine).GetEndPoint(0), lineDir, r2.GlobalPoint);
		if ((p2 - p1).DotProduct(lineDir) < 0.0)
		{
			XYZ temp = p1;
			p1 = p2;
			p2 = temp;
		}
		double sign = (isTop ? 1.0 : (-1.0));
		Transaction t = new Transaction(doc, "Cable Tray Offset");
		try
		{
			t.Start();
			if (angle == 45)
			{
				XYZ cutPoint1 = p1;
				XYZ cutPoint2 = cutPoint1 + lineDir * offset + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint3 = p2 - lineDir * offset + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint4 = p2;
				double midLength = cutPoint2.DistanceTo(cutPoint3);
				SplitAndDeleteMiddle(doc, tray, trayLine, p1, p2);
				CableTray traySlant1 = CableTray.Create(doc, trayTypeId, cutPoint1, cutPoint2, levelId);
				SetCableTraySize(traySlant1, width, height);
				CableTray traySlant2 = CableTray.Create(doc, trayTypeId, cutPoint3, cutPoint4, levelId);
				SetCableTraySize(traySlant2, width, height);
				CreateElbowAtPointCableTray(doc, cutPoint1, traySlant1);
				if (midLength >= 0.01)
				{
					CableTray trayMid = CableTray.Create(doc, trayTypeId, cutPoint2, cutPoint3, levelId);
					SetCableTraySize(trayMid, width, height);
					MEPLibrary.CreateElbowCabletrayFitting(doc, traySlant1, trayMid);
					MEPLibrary.CreateElbowCabletrayFitting(doc, trayMid, traySlant2);
				}
				else
				{
					MEPLibrary.CreateElbowCabletrayFitting(doc, traySlant1, traySlant2);
				}
				CreateElbowAtPointCableTray(doc, cutPoint4, traySlant2);
			}
			else
			{
				XYZ cutPoint5 = p1;
				XYZ cutPoint6 = p1 + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint7 = p2 + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint8 = p2;
				SplitAndDeleteMiddle(doc, tray, trayLine, p1, p2);
				CableTray trayVert1 = CableTray.Create(doc, trayTypeId, cutPoint5, cutPoint6, levelId);
				SetCableTraySize(trayVert1, width, height);
				Line rotAxis1 = Line.CreateBound(cutPoint5, cutPoint6);
				ElementTransformUtils.RotateElement(doc, ((Element)trayVert1).Id, rotAxis1, Math.PI / 2.0);
				CableTray trayMid2 = CableTray.Create(doc, trayTypeId, cutPoint6, cutPoint7, levelId);
				SetCableTraySize(trayMid2, width, height);
				CableTray trayVert2 = CableTray.Create(doc, trayTypeId, cutPoint7, cutPoint8, levelId);
				SetCableTraySize(trayVert2, width, height);
				Line rotAxis2 = Line.CreateBound(cutPoint7, cutPoint8);
				ElementTransformUtils.RotateElement(doc, ((Element)trayVert2).Id, rotAxis2, Math.PI / 2.0);
				CreateElbowAtPointCableTray(doc, cutPoint5, trayVert1);
				MEPLibrary.CreateElbowCabletrayFitting(doc, trayVert1, trayMid2);
				MEPLibrary.CreateElbowCabletrayFitting(doc, trayMid2, trayVert2);
				CreateElbowAtPointCableTray(doc, cutPoint8, trayVert2);
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
	}

	private static void SplitAndDeleteMiddle(Document doc, CableTray tray, Line trayLine, XYZ p1, XYZ p2)
	{
		XYZ sp = ((Curve)trayLine).GetEndPoint(0);
		XYZ ep = ((Curve)trayLine).GetEndPoint(1);
		XYZ dir = trayLine.Direction;
		double tSP = 0.0;
		double tP1 = (p1 - sp).DotProduct(dir);
		double tP2 = (p2 - sp).DotProduct(dir);
		double tEP = (ep - sp).DotProduct(dir);
		bool spBeforeP1 = tSP < tP1 - 0.01;
		bool p2BeforeEP = tP2 < tEP - 0.01;
		if (spBeforeP1 && p2BeforeEP)
		{
			Location location = ((Element)tray).Location;
			((LocationCurve)((location is LocationCurve) ? location : null)).Curve = (Curve)(object)Line.CreateBound(sp, p1);
			ElementId trayTypeId = ((Element)tray).GetTypeId();
			ElementId levelId = ((Element)tray).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
			double width = ((Element)tray).get_Parameter((BuiltInParameter)(-1140122)).AsDouble();
			double height = ((Element)tray).get_Parameter((BuiltInParameter)(-1140121)).AsDouble();
			CableTray newTray = CableTray.Create(doc, trayTypeId, p2, ep, levelId);
			SetCableTraySize(newTray, width, height);
		}
		else if (spBeforeP1)
		{
			Location location2 = ((Element)tray).Location;
			((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve = (Curve)(object)Line.CreateBound(sp, p1);
		}
		else if (p2BeforeEP)
		{
			Location location3 = ((Element)tray).Location;
			((LocationCurve)((location3 is LocationCurve) ? location3 : null)).Curve = (Curve)(object)Line.CreateBound(p2, ep);
		}
		else
		{
			doc.Delete(((Element)tray).Id);
		}
	}

	private static void SetCableTraySize(CableTray tray, double width, double height)
	{
		((Element)tray).get_Parameter((BuiltInParameter)(-1140122)).Set(width);
		((Element)tray).get_Parameter((BuiltInParameter)(-1140121)).Set(height);
	}

	private static void CreateElbowAtPointCableTray(Document doc, XYZ point, CableTray newTray)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		ConnectorSet connectors = ((MEPCurve)newTray).ConnectorManager.Connectors;
		Connector closestNew = null;
		double minDist = double.MaxValue;
		foreach (Connector item in connectors)
		{
			Connector c = item;
			double d = c.Origin.DistanceTo(point);
			if (d < minDist)
			{
				minDist = d;
				closestNew = c;
			}
		}
		if (closestNew == null)
		{
			return;
		}
		FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(CableTray)).WhereElementIsNotElementType();
		foreach (CableTray item2 in collector)
		{
			CableTray otherTray = item2;
			if (((Element)otherTray).Id == ((Element)newTray).Id)
			{
				continue;
			}
			foreach (Connector connector in ((MEPCurve)otherTray).ConnectorManager.Connectors)
			{
				Connector c2 = connector;
				if (c2.Origin.DistanceTo(point) < 0.01 && !c2.IsConnected)
				{
					try
					{
						doc.Create.NewElbowFitting(closestNew, c2);
						return;
					}
					catch
					{
					}
				}
			}
		}
	}

	private static XYZ SnapToLine(XYZ lineOrigin, XYZ lineDir, XYZ point)
	{
		double t = (point - lineOrigin).DotProduct(lineDir);
		return lineOrigin + t * lineDir;
	}
}
