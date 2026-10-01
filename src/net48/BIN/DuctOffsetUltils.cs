using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace BIN;

public static class DuctOffsetUltils
{
	public static void DuctCut45(Document doc, Reference r1, Reference r2, double offset, bool isTop)
	{
		ExecuteDuctOffset(doc, r1, r2, offset, isTop, 45);
	}

	public static void DuctCut90(Document doc, Reference r1, Reference r2, double offset, bool isTop)
	{
		ExecuteDuctOffset(doc, r1, r2, offset, isTop, 90);
	}

	private static void ExecuteDuctOffset(Document doc, Reference r1, Reference r2, double offset, bool isTop, int angle)
	{
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Expected O, but got Unknown
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_02df: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e9: Expected O, but got Unknown
		//IL_0303: Unknown result type (might be due to invalid IL or missing references)
		//IL_030d: Expected O, but got Unknown
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a6: Expected O, but got Unknown
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Expected O, but got Unknown
		//IL_0410: Unknown result type (might be due to invalid IL or missing references)
		Element element = doc.GetElement(r1.ElementId);
		Duct duct = (Duct)(object)((element is Duct) ? element : null);
		if (duct == null)
		{
			return;
		}
		Parameter obj = ((Element)duct).get_Parameter((BuiltInParameter)(-1114101));
		double width = ((obj != null) ? obj.AsDouble() : 0.0);
		Parameter obj2 = ((Element)duct).get_Parameter((BuiltInParameter)(-1114102));
		double height = ((obj2 != null) ? obj2.AsDouble() : 0.0);
		Parameter obj3 = ((Element)duct).get_Parameter((BuiltInParameter)(-1114103));
		double diameter = ((obj3 != null) ? obj3.AsDouble() : 0.0);
		ElementId ductTypeId = ((Element)duct).GetTypeId();
		ElementId systemTypeId = ((Element)duct).get_Parameter((BuiltInParameter)(-1140333)).AsElementId();
		ElementId levelId = ((Element)duct).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
		Location location = ((Element)duct).Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locCurve.Curve;
		Line ductLine = (Line)(object)((curve is Line) ? curve : null);
		XYZ lineDir = ductLine.Direction;
		XYZ p1 = SnapToLine(((Curve)ductLine).GetEndPoint(0), lineDir, r1.GlobalPoint);
		XYZ p2 = SnapToLine(((Curve)ductLine).GetEndPoint(0), lineDir, r2.GlobalPoint);
		if ((p2 - p1).DotProduct(lineDir) < 0.0)
		{
			XYZ temp = p1;
			p1 = p2;
			p2 = temp;
		}
		double sign = (isTop ? 1.0 : (-1.0));
		Transaction t = new Transaction(doc, "Duct Offset");
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
				Duct ductAfterP1 = BreakDuctAt(doc, duct, p1, p2);
				Duct ductMid = BreakDuctAt(doc, ductAfterP1, p2, p1);
				doc.Delete(((Element)ductMid).Id);
				Duct ductSlant1 = Duct.Create(doc, systemTypeId, ductTypeId, levelId, cutPoint1, cutPoint2);
				SetDuctSize(ductSlant1, width, height, diameter);
				Duct ductSlant2 = Duct.Create(doc, systemTypeId, ductTypeId, levelId, cutPoint3, cutPoint4);
				SetDuctSize(ductSlant2, width, height, diameter);
				CreateElbowAtPointDuct(doc, cutPoint1, ductSlant1);
				if (midLength >= 0.01)
				{
					Duct ductMidNew = Duct.Create(doc, systemTypeId, ductTypeId, levelId, cutPoint2, cutPoint3);
					SetDuctSize(ductMidNew, width, height, diameter);
					MEPLibrary.CreateElbowFitting(doc, ductSlant1, ductMidNew);
					MEPLibrary.CreateElbowFitting(doc, ductMidNew, ductSlant2);
				}
				else
				{
					MEPLibrary.CreateElbowFitting(doc, ductSlant1, ductSlant2);
				}
				CreateElbowAtPointDuct(doc, cutPoint4, ductSlant2);
			}
			else
			{
				XYZ cutPoint5 = p1;
				XYZ cutPoint6 = p1 + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint7 = p2 + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint8 = p2;
				Duct ductAfterP2 = BreakDuctAt(doc, duct, p1, p2);
				Duct ductMid2 = BreakDuctAt(doc, ductAfterP2, p2, p1);
				doc.Delete(((Element)ductMid2).Id);
				Duct ductVert1 = Duct.Create(doc, systemTypeId, ductTypeId, levelId, cutPoint5, cutPoint6);
				if (diameter > 0.0)
				{
					SetDuctSize(ductVert1, width, height, diameter);
				}
				else
				{
					SetDuctSize(ductVert1, height, width, 0.0);
				}
				Duct ductMidNew2 = Duct.Create(doc, systemTypeId, ductTypeId, levelId, cutPoint6, cutPoint7);
				SetDuctSize(ductMidNew2, width, height, diameter);
				Duct ductVert2 = Duct.Create(doc, systemTypeId, ductTypeId, levelId, cutPoint7, cutPoint8);
				if (diameter > 0.0)
				{
					SetDuctSize(ductVert2, width, height, diameter);
				}
				else
				{
					SetDuctSize(ductVert2, height, width, 0.0);
				}
				CreateElbowAtPointDuct(doc, cutPoint5, ductVert1);
				MEPLibrary.CreateElbowFitting(doc, ductVert1, ductMidNew2);
				MEPLibrary.CreateElbowFitting(doc, ductMidNew2, ductVert2);
				CreateElbowAtPointDuct(doc, cutPoint8, ductVert2);
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
	}

	private static Duct BreakDuctAt(Document doc, Duct duct, XYZ breakPoint, XYZ referencePoint)
	{
		Location location = ((Element)duct).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line ductLine = (Line)(object)((curve is Line) ? curve : null);
		if (breakPoint.DistanceTo(((Curve)ductLine).GetEndPoint(0)) < 0.01 || breakPoint.DistanceTo(((Curve)ductLine).GetEndPoint(1)) < 0.01)
		{
			return duct;
		}
		ElementId newId = MechanicalUtils.BreakCurve(doc, ((Element)duct).Id, breakPoint);
		Element element = doc.GetElement(newId);
		Duct newDuct = (Duct)(object)((element is Duct) ? element : null);
		Location location2 = ((Element)duct).Location;
		Curve curve2 = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve;
		Line line1 = (Line)(object)((curve2 is Line) ? curve2 : null);
		Location location3 = ((Element)newDuct).Location;
		Curve curve3 = ((LocationCurve)((location3 is LocationCurve) ? location3 : null)).Curve;
		Line line2 = (Line)(object)((curve3 is Line) ? curve3 : null);
		XYZ mid1 = (((Curve)line1).GetEndPoint(0) + ((Curve)line1).GetEndPoint(1)) / 2.0;
		XYZ mid2 = (((Curve)line2).GetEndPoint(0) + ((Curve)line2).GetEndPoint(1)) / 2.0;
		return (mid1.DistanceTo(referencePoint) < mid2.DistanceTo(referencePoint)) ? duct : newDuct;
	}

	private static void SetDuctSize(Duct duct, double width, double height, double diameter)
	{
		if (diameter > 0.0)
		{
			((Element)duct).get_Parameter((BuiltInParameter)(-1114103)).Set(diameter);
			return;
		}
		((Element)duct).get_Parameter((BuiltInParameter)(-1114101)).Set(width);
		((Element)duct).get_Parameter((BuiltInParameter)(-1114102)).Set(height);
	}

	private static void CreateElbowAtPointDuct(Document doc, XYZ point, Duct newDuct)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		ConnectorSet connectors = ((MEPCurve)newDuct).ConnectorManager.Connectors;
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
		FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Duct)).WhereElementIsNotElementType();
		foreach (Duct item2 in collector)
		{
			Duct otherDuct = item2;
			if (((Element)otherDuct).Id == ((Element)newDuct).Id)
			{
				continue;
			}
			foreach (Connector connector in ((MEPCurve)otherDuct).ConnectorManager.Connectors)
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
