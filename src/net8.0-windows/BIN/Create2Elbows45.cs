using System;
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
public class Create2Elbows45 : IExternalCommand
{
	public class SelectPipe : ISelectionFilter
	{
		public bool AllowElement(Element element)
		{
			return element is Pipe;
		}

		public bool AllowReference(Reference refer, XYZ point)
		{
			return false;
		}
	}

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		double currentLength = 150.0;
		while (true)
		{
			InputLengthWindow ui = new InputLengthWindow();
			ui.txtInput.Text = currentLength.ToString();
			if (ui.ShowDialog() != true)
			{
				break;
			}
			currentLength = ui.ResultLength;
			while (true)
			{
				try
				{
					Reference r1 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectPipe(), "Chọn ống thứ nhất (Nhấn Esc để quay lại bảng cài đặt)");
					Reference r2 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectPipe(), "Chọn ống thứ hai (Nhấn Esc để quay lại bảng cài đặt)");
					Element element = doc.GetElement(r1);
					Pipe pipe1 = (Pipe)(object)((element is Pipe) ? element : null);
					Element element2 = doc.GetElement(r2);
					Pipe pipe2 = (Pipe)(object)((element2 is Pipe) ? element2 : null);
					Transaction t = new Transaction(doc, "Create 45 Degree Connection");
					try
					{
						t.Start();
						Create2ElbowsLogic(pipe1, pipe2, currentLength);
						t.Commit();
					}
					finally
					{
						((IDisposable)t)?.Dispose();
					}
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

	private void Create2ElbowsLogic(Pipe pipe1, Pipe pipe2, double lengthMm)
	{
		Document doc = ((Element)pipe1).Document;
		DeleteCommonFitting(pipe1, pipe2);
		((XYZ, XYZ), (XYZ, XYZ)) points = ClassifyPoints((MEPCurve)(object)pipe1, (MEPCurve)(object)pipe2);
		XYZ A = points.Item1.Item1;
		XYZ C = points.Item1.Item2;
		XYZ B = points.Item2.Item1;
		XYZ D = points.Item2.Item2;
		ElementId systemTypeId = ((Element)pipe1).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		ElementId pipeTypeId = ((Element)pipe1).GetTypeId();
		ElementId levelId = ((Element)pipe1).LevelId;
		double diameter = ((MEPCurve)pipe1).Diameter;
		Location location = ((Element)pipe1).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line pLine1 = (Line)(object)((curve is Line) ? curve : null);
		Location location2 = ((Element)pipe2).Location;
		Curve curve2 = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve;
		Line pLine2 = (Line)(object)((curve2 is Line) ? curve2 : null);
		XYZ d1 = pLine1.Direction;
		XYZ d2 = pLine2.Direction;
		GetClosestPointsPair(pLine1, pLine2, out var onLine1, out var onLine2);
		if (d1.DotProduct(B - onLine1) < 0.0)
		{
			d1 = d1.Negate();
		}
		if (d2.DotProduct(D - onLine2) < 0.0)
		{
			d2 = d2.Negate();
		}
		double desiredLengthFeet = lengthMm / 304.8;
		double angleRad = Math.PI / 8.0;
		double distanceOffset = desiredLengthFeet / 2.0 / Math.Tan(angleRad);
		XYZ connectPoint1 = onLine1 + d1 * distanceOffset;
		XYZ connectPoint2 = onLine2 + d2 * distanceOffset;
		Pipe pipe45 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, connectPoint1, connectPoint2);
		((Element)pipe45).LookupParameter("Diameter").Set(diameter);
		try
		{
			Parameter slopeParam1 = ((Element)pipe1).get_Parameter((BuiltInParameter)(-1140256));
			Parameter slopeParam45 = ((Element)pipe45).get_Parameter((BuiltInParameter)(-1140256));
			if (slopeParam1 != null && slopeParam45 != null && slopeParam1.AsDouble() > 0.0)
			{
				slopeParam45.Set(slopeParam1.AsDouble());
			}
		}
		catch
		{
		}
		Pipe newPipe1 = EditPipe(pipe1, connectPoint1, B);
		Pipe newPipe2 = EditPipe(pipe2, connectPoint2, D);
		CreateElbowFitting((MEPCurve)(object)newPipe1, (MEPCurve)(object)pipe45);
		CreateElbowFitting((MEPCurve)(object)newPipe2, (MEPCurve)(object)pipe45);
	}

	private void GetClosestPointsPair(Line line1, Line line2, out XYZ ptOnLine1, out XYZ ptOnLine2)
	{
		XYZ p1 = ((Curve)line1).GetEndPoint(0);
		XYZ u = line1.Direction;
		XYZ p2 = ((Curve)line2).GetEndPoint(0);
		XYZ v = line2.Direction;
		XYZ w0 = p1 - p2;
		double a = u.DotProduct(u);
		double b = u.DotProduct(v);
		double c = v.DotProduct(v);
		double d = u.DotProduct(w0);
		double e = v.DotProduct(w0);
		double denom = a * c - b * b;
		double s;
		double t;
		if (Math.Abs(denom) < 1E-10)
		{
			s = 0.0;
			t = d / b;
		}
		else
		{
			s = (b * e - c * d) / denom;
			t = (a * e - b * d) / denom;
		}
		ptOnLine1 = p1 + s * u;
		ptOnLine2 = p2 + t * v;
	}

	private Pipe EditPipe(Pipe pipe, XYZ connectPoint, XYZ farEnd)
	{
		Document doc = ((Element)pipe).Document;
		Location location = ((Element)pipe).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		XYZ snapped = SnapToLine(sp, line.Direction, connectPoint);
		if (snapped.DistanceTo(sp) > ((Curve)line).Length + 0.001 || snapped.DistanceTo(ep) > ((Curve)line).Length + 0.001)
		{
			XYZ keepEnd = ((sp.DistanceTo(farEnd) < ep.DistanceTo(farEnd)) ? sp : ep);
			Line newLine = Line.CreateBound(keepEnd, snapped);
			Location location2 = ((Element)pipe).Location;
			((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve = (Curve)(object)newLine;
			return pipe;
		}
		ElementId newId = PlumbingUtils.BreakCurve(doc, ((Element)pipe).Id, snapped);
		Element element = doc.GetElement(newId);
		Pipe newPipe = (Pipe)(object)((element is Pipe) ? element : null);
		if (newPipe == null)
		{
			return pipe;
		}
		Location location3 = ((Element)pipe).Location;
		Curve curve2 = ((LocationCurve)((location3 is LocationCurve) ? location3 : null)).Curve;
		Line line2 = (Line)(object)((curve2 is Line) ? curve2 : null);
		Location location4 = ((Element)newPipe).Location;
		Curve curve3 = ((LocationCurve)((location4 is LocationCurve) ? location4 : null)).Curve;
		Line line3 = (Line)(object)((curve3 is Line) ? curve3 : null);
		XYZ mid1 = (((Curve)line2).GetEndPoint(0) + ((Curve)line2).GetEndPoint(1)) / 2.0;
		XYZ mid2 = (((Curve)line3).GetEndPoint(0) + ((Curve)line3).GetEndPoint(1)) / 2.0;
		bool origHasFarEnd = mid1.DistanceTo(farEnd) < mid2.DistanceTo(farEnd);
		Pipe keepPipe = (origHasFarEnd ? pipe : newPipe);
		Pipe stubPipe = (origHasFarEnd ? newPipe : pipe);
		doc.Delete(((Element)stubPipe).Id);
		return keepPipe;
	}

	private XYZ SnapToLine(XYZ lineOrigin, XYZ lineDir, XYZ point)
	{
		double t = (point - lineOrigin).DotProduct(lineDir);
		return lineOrigin + t * lineDir;
	}

	private static void CreateElbowFitting(MEPCurve ele1, MEPCurve ele2)
	{
		Connector c1 = GetClosestConnector(ele1, ele2);
		Connector c2 = GetClosestConnector(ele2, ele1);
		((Element)ele1).Document.Create.NewElbowFitting(c1, c2);
	}

	private static Connector GetClosestConnector(MEPCurve main, MEPCurve target)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		Connector closest = null;
		double minDistance = double.MaxValue;
		foreach (Connector connector in main.ConnectorManager.Connectors)
		{
			Connector cMain = connector;
			foreach (Connector connector2 in target.ConnectorManager.Connectors)
			{
				Connector cTarget = connector2;
				double dist = cMain.Origin.DistanceTo(cTarget.Origin);
				if (dist < minDistance)
				{
					minDistance = dist;
					closest = cMain;
				}
			}
		}
		return closest;
	}

	private ((XYZ, XYZ), (XYZ, XYZ)) ClassifyPoints(MEPCurve ele1, MEPCurve ele2)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Expected O, but got Unknown
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Expected O, but got Unknown
		List<XYZ> p1s = new List<XYZ>();
		foreach (Connector connector in ele1.ConnectorManager.Connectors)
		{
			Connector cn = connector;
			p1s.Add(cn.Origin);
		}
		List<XYZ> p2s = new List<XYZ>();
		foreach (Connector connector2 in ele2.ConnectorManager.Connectors)
		{
			Connector cn2 = connector2;
			p2s.Add(cn2.Origin);
		}
		var combinations = from p1 in p1s
			from p2 in p2s
			select new
			{
				p1 = p1,
				p2 = p2,
				dist = p1.DistanceTo(p2)
			};
		var min = combinations.OrderBy(x => x.dist).First();
		var max = combinations.OrderByDescending(x => x.dist).First();
		return ((min.p1, min.p2), (max.p1, max.p2));
	}

	private void DeleteCommonFitting(Pipe pipe1, Pipe pipe2)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Expected O, but got Unknown
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Expected O, but got Unknown
		Document doc = ((Element)pipe1).Document;
		foreach (Connector connector in ((MEPCurve)pipe1).ConnectorManager.Connectors)
		{
			Connector c1 = connector;
			if (!c1.IsConnected)
			{
				continue;
			}
			foreach (Connector allRef in c1.AllRefs)
			{
				Connector refConn = allRef;
				Element owner = refConn.Owner;
				FamilyInstance fitting = (FamilyInstance)(object)((owner is FamilyInstance) ? owner : null);
				if (fitting == null)
				{
					continue;
				}
				foreach (Connector connector2 in fitting.MEPModel.ConnectorManager.Connectors)
				{
					Connector fc = connector2;
					foreach (Connector allRef2 in fc.AllRefs)
					{
						Connector fRef = allRef2;
						if (fRef.Owner.Id == ((Element)pipe2).Id)
						{
							try
							{
								doc.Delete(((Element)fitting).Id);
								return;
							}
							catch
							{
								return;
							}
						}
					}
				}
			}
		}
	}
}
