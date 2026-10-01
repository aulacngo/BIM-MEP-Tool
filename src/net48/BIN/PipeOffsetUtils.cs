using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace BIN;

public static class PipeOffsetUtils
{
	public static void PipeCut45(Document doc, Reference r1, Reference r2, double offset, bool isTop)
	{
		ExecutePipeOffset(doc, r1, r2, offset, isTop, 45);
	}

	public static void PipeCut90(Document doc, Reference r1, Reference r2, double offset, bool isTop)
	{
		ExecutePipeOffset(doc, r1, r2, offset, isTop, 90);
	}

	private static void ExecutePipeOffset(Document doc, Reference r1, Reference r2, double offset, bool isTop, int angle)
	{
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		//IL_039f: Expected O, but got Unknown
		//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c3: Expected O, but got Unknown
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Expected O, but got Unknown
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Expected O, but got Unknown
		//IL_0566: Unknown result type (might be due to invalid IL or missing references)
		Element element = doc.GetElement(r1.ElementId);
		Pipe pipe = (Pipe)(object)((element is Pipe) ? element : null);
		if (pipe == null)
		{
			return;
		}
		double dia = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140225)).AsDouble();
		ElementId pipeTypeId = ((Element)pipe).GetTypeId();
		ElementId systemTypeId = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		ElementId levelId = ((Element)pipe).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
		Location location = ((Element)pipe).Location;
		LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		Curve curve = locCurve.Curve;
		Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
		XYZ lineDir = pipeLine.Direction;
		XYZ p1 = SnapToLine(((Curve)pipeLine).GetEndPoint(0), lineDir, r1.GlobalPoint);
		XYZ p2 = SnapToLine(((Curve)pipeLine).GetEndPoint(0), lineDir, r2.GlobalPoint);
		if ((p2 - p1).DotProduct(lineDir) < 0.0)
		{
			XYZ temp = p1;
			p1 = p2;
			p2 = temp;
		}
		double sign = (isTop ? 1.0 : (-1.0));
		Transaction t = new Transaction(doc, "Pipe Offset");
		try
		{
			t.Start();
			if (angle == 45)
			{
				XYZ cutPoint1 = p1;
				XYZ cutPoint4 = p2;
				XYZ cutPoint5 = cutPoint1 + lineDir * offset + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint6 = cutPoint4 - lineDir * offset + new XYZ(0.0, 0.0, sign * offset);
				double midLength = cutPoint5.DistanceTo(cutPoint6);
				if (midLength < 0.01)
				{
					cutPoint6 = cutPoint5;
				}
				Pipe pipeAfterP1 = pipe;
				if (p1.DistanceTo(((Curve)pipeLine).GetEndPoint(0)) > 0.01 && p1.DistanceTo(((Curve)pipeLine).GetEndPoint(1)) > 0.01)
				{
					ElementId newId = PlumbingUtils.BreakCurve(doc, ((Element)pipe).Id, p1);
					Element element2 = doc.GetElement(newId);
					Pipe newPipe = (Pipe)(object)((element2 is Pipe) ? element2 : null);
					pipeAfterP1 = GetPipeContainingPoint(doc, pipe, newPipe, p2);
				}
				Location location2 = ((Element)pipeAfterP1).Location;
				Curve curve2 = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve;
				Line lineAfterP1 = (Line)(object)((curve2 is Line) ? curve2 : null);
				Pipe pipeBeforeP2 = pipeAfterP1;
				if (p2.DistanceTo(((Curve)lineAfterP1).GetEndPoint(0)) > 0.01 && p2.DistanceTo(((Curve)lineAfterP1).GetEndPoint(1)) > 0.01)
				{
					ElementId newId2 = PlumbingUtils.BreakCurve(doc, ((Element)pipeAfterP1).Id, p2);
					Element element3 = doc.GetElement(newId2);
					Pipe newPipe2 = (Pipe)(object)((element3 is Pipe) ? element3 : null);
					pipeBeforeP2 = GetPipeContainingPoint(doc, pipeAfterP1, newPipe2, p1);
				}
				doc.Delete(((Element)pipeBeforeP2).Id);
				Pipe pipeSlant1 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, cutPoint1, cutPoint5);
				((Element)pipeSlant1).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				Pipe pipeSlant2 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, cutPoint6, cutPoint4);
				((Element)pipeSlant2).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				CreateElbowAtPoint(doc, cutPoint1, pipeSlant1);
				if (midLength >= 0.01)
				{
					Pipe pipeMid = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, cutPoint5, cutPoint6);
					((Element)pipeMid).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
					MEPLibrary.CreateElbowPipeFitting(doc, pipeSlant1, pipeMid);
					MEPLibrary.CreateElbowPipeFitting(doc, pipeMid, pipeSlant2);
				}
				else
				{
					MEPLibrary.CreateElbowPipeFitting(doc, pipeSlant1, pipeSlant2);
				}
				CreateElbowAtPoint(doc, cutPoint4, pipeSlant2);
			}
			else
			{
				XYZ cutPoint7 = p1;
				XYZ cutPoint8 = p1 + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint9 = p2 + new XYZ(0.0, 0.0, sign * offset);
				XYZ cutPoint10 = p2;
				Pipe pipeAfterP2 = pipe;
				if (p1.DistanceTo(((Curve)pipeLine).GetEndPoint(0)) > 0.01 && p1.DistanceTo(((Curve)pipeLine).GetEndPoint(1)) > 0.01)
				{
					ElementId newId3 = PlumbingUtils.BreakCurve(doc, ((Element)pipe).Id, p1);
					Element element4 = doc.GetElement(newId3);
					Pipe newPipe3 = (Pipe)(object)((element4 is Pipe) ? element4 : null);
					pipeAfterP2 = GetPipeContainingPoint(doc, pipe, newPipe3, p2);
				}
				Location location3 = ((Element)pipeAfterP2).Location;
				Curve curve3 = ((LocationCurve)((location3 is LocationCurve) ? location3 : null)).Curve;
				Line lineAfterP2 = (Line)(object)((curve3 is Line) ? curve3 : null);
				Pipe pipeBeforeP3 = pipeAfterP2;
				if (p2.DistanceTo(((Curve)lineAfterP2).GetEndPoint(0)) > 0.01 && p2.DistanceTo(((Curve)lineAfterP2).GetEndPoint(1)) > 0.01)
				{
					ElementId newId4 = PlumbingUtils.BreakCurve(doc, ((Element)pipeAfterP2).Id, p2);
					Element element5 = doc.GetElement(newId4);
					Pipe newPipe4 = (Pipe)(object)((element5 is Pipe) ? element5 : null);
					pipeBeforeP3 = GetPipeContainingPoint(doc, pipeAfterP2, newPipe4, p1);
				}
				doc.Delete(((Element)pipeBeforeP3).Id);
				Pipe pipeVert1 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, cutPoint7, cutPoint8);
				((Element)pipeVert1).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				Pipe pipeMid2 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, cutPoint8, cutPoint9);
				((Element)pipeMid2).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				Pipe pipeVert2 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, cutPoint9, cutPoint10);
				((Element)pipeVert2).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
				CreateElbowAtPoint(doc, cutPoint7, pipeVert1);
				MEPLibrary.CreateElbowPipeFitting(doc, pipeVert1, pipeMid2);
				MEPLibrary.CreateElbowPipeFitting(doc, pipeMid2, pipeVert2);
				CreateElbowAtPoint(doc, cutPoint10, pipeVert2);
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
	}

	private static Pipe GetPipeContainingPoint(Document doc, Pipe pipe1, Pipe pipe2, XYZ point)
	{
		Location location = ((Element)pipe1).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line line1 = (Line)(object)((curve is Line) ? curve : null);
		Location location2 = ((Element)pipe2).Location;
		Curve curve2 = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve;
		Line line2 = (Line)(object)((curve2 is Line) ? curve2 : null);
		XYZ mid1 = (((Curve)line1).GetEndPoint(0) + ((Curve)line1).GetEndPoint(1)) / 2.0;
		XYZ mid2 = (((Curve)line2).GetEndPoint(0) + ((Curve)line2).GetEndPoint(1)) / 2.0;
		XYZ proj1 = SnapToLine(((Curve)line1).GetEndPoint(0), line1.Direction, point);
		XYZ proj2 = SnapToLine(((Curve)line2).GetEndPoint(0), line2.Direction, point);
		double d1 = proj1.DistanceTo(mid1);
		double d2 = proj2.DistanceTo(mid2);
		return (d1 < d2) ? pipe1 : pipe2;
	}

	private static void CreateElbowAtPoint(Document doc, XYZ point, Pipe newPipe)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Expected O, but got Unknown
		ConnectorSet connectors = ((MEPCurve)newPipe).ConnectorManager.Connectors;
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
		FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).WhereElementIsNotElementType();
		foreach (Pipe item2 in collector)
		{
			Pipe otherPipe = item2;
			if (((Element)otherPipe).Id == ((Element)newPipe).Id)
			{
				continue;
			}
			foreach (Connector connector in ((MEPCurve)otherPipe).ConnectorManager.Connectors)
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
