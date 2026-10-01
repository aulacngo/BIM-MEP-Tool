using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class TurnMEP : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_019d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Expected O, but got Unknown
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			TurnMEPWindow ui = new TurnMEPWindow();
			if (ui.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			double offset = ui.OffsetValue / 304.8;
			string direction = ui.SelectedDirection;
			double angleValue = ui.SelectedAngle;
			int mepType = ui.MEPType;
			string prompt = mepType switch
			{
				1 => "Chọn Duct", 
				0 => "Chọn ống Pipe", 
				_ => "Chọn Cable Tray", 
			};
			ISelectionFilter obj;
			switch (mepType)
			{
			default:
			{
				ISelectionFilter val = (ISelectionFilter)(object)new SelectCableTrays();
				obj = val;
				break;
			}
			case 1:
			{
				ISelectionFilter val = (ISelectionFilter)(object)new SelectDucts();
				obj = val;
				break;
			}
			case 0:
			{
				ISelectionFilter val = (ISelectionFilter)(object)new SelectPipes();
				obj = val;
				break;
			}
			}
			ISelectionFilter filter = obj;
			Reference r = uidoc.Selection.PickObject((ObjectType)1, filter, prompt);
			Element element = doc.GetElement(r);
			XYZ pickPoint = r.GlobalPoint;
			Transaction trans = new Transaction(doc, "Turn MEP");
			try
			{
				trans.Start();
				XYZ turnPoint = GetTurnPoint(element, pickPoint);
				XYZ elsePoint = GetOppositeEndPoint(element, turnPoint);
				switch (mepType)
				{
				case 0:
				{
					Pipe pipe = (Pipe)(object)((element is Pipe) ? element : null);
					TurnPipeLogic(doc, pipe, turnPoint, elsePoint, offset, direction, angleValue);
					break;
				}
				case 1:
				{
					Duct duct = (Duct)(object)((element is Duct) ? element : null);
					TurnDuctLogic(doc, duct, turnPoint, elsePoint, offset, direction, angleValue);
					break;
				}
				default:
				{
					CableTray cableTray = (CableTray)(object)((element is CableTray) ? element : null);
					TurnCableTrayLogic(doc, cableTray, turnPoint, elsePoint, offset, direction, angleValue);
					break;
				}
				}
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

	private void TurnPipeLogic(Document doc, Pipe mainPipe, XYZ turnPoint, XYZ elsePoint, double offset, string direction, double angleValue)
	{
		double dia = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140225)).AsDouble();
		ElementId pipeTypeId = ((Element)mainPipe).GetTypeId();
		ElementId systemTypeId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		ElementId levelId = ((Element)mainPipe).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
		XYZ mainDir = (turnPoint - elsePoint).Normalize();
		XYZ endPoint = CalculateEndPoint(turnPoint, elsePoint, mainDir, offset, direction, angleValue);
		Pipe newPipe = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, turnPoint, endPoint);
		((Element)newPipe).get_Parameter((BuiltInParameter)(-1140225)).Set(dia);
		MEPLibrary.CreateElbowPipeFitting(doc, newPipe, mainPipe);
	}

	private void TurnDuctLogic(Document doc, Duct mainDuct, XYZ turnPoint, XYZ elsePoint, double offset, string direction, double angleValue)
	{
		Parameter obj = ((Element)mainDuct).get_Parameter((BuiltInParameter)(-1114101));
		double width = ((obj != null) ? obj.AsDouble() : 0.0);
		Parameter obj2 = ((Element)mainDuct).get_Parameter((BuiltInParameter)(-1114102));
		double height = ((obj2 != null) ? obj2.AsDouble() : 0.0);
		Parameter obj3 = ((Element)mainDuct).get_Parameter((BuiltInParameter)(-1114103));
		double diameter = ((obj3 != null) ? obj3.AsDouble() : 0.0);
		ElementId ductTypeId = ((Element)mainDuct).GetTypeId();
		ElementId systemTypeId = ((Element)mainDuct).get_Parameter((BuiltInParameter)(-1140333)).AsElementId();
		ElementId levelId = ((Element)mainDuct).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
		XYZ mainDir = (turnPoint - elsePoint).Normalize();
		XYZ endPoint = CalculateEndPoint(turnPoint, elsePoint, mainDir, offset, direction, angleValue);
		Duct newDuct = Duct.Create(doc, systemTypeId, ductTypeId, levelId, turnPoint, endPoint);
		if (diameter > 0.0)
		{
			((Element)newDuct).get_Parameter((BuiltInParameter)(-1114103)).Set(diameter);
		}
		else
		{
			((Element)newDuct).get_Parameter((BuiltInParameter)(-1114101)).Set(width);
			((Element)newDuct).get_Parameter((BuiltInParameter)(-1114102)).Set(height);
		}
		if ((direction == "UP" || direction == "DOWN") && angleValue == 90.0 && diameter == 0.0)
		{
			Line rotationAxis = Line.CreateBound(turnPoint, turnPoint + XYZ.BasisZ);
			ElementTransformUtils.RotateElement(doc, ((Element)newDuct).Id, rotationAxis, Math.PI / 2.0);
		}
		MEPLibrary.CreateElbowFitting(doc, newDuct, mainDuct);
	}

	private void TurnCableTrayLogic(Document doc, CableTray mainTray, XYZ turnPoint, XYZ elsePoint, double offset, string direction, double angleValue)
	{
		double width = ((Element)mainTray).get_Parameter((BuiltInParameter)(-1140122)).AsDouble();
		double height = ((Element)mainTray).get_Parameter((BuiltInParameter)(-1140121)).AsDouble();
		ElementId trayTypeId = ((Element)mainTray).GetTypeId();
		ElementId levelId = ((Element)mainTray).get_Parameter((BuiltInParameter)(-1114000)).AsElementId();
		XYZ mainDir = (turnPoint - elsePoint).Normalize();
		XYZ endPoint = CalculateEndPoint(turnPoint, elsePoint, mainDir, offset, direction, angleValue);
		CableTray newTray = CableTray.Create(doc, trayTypeId, turnPoint, endPoint, levelId);
		((Element)newTray).get_Parameter((BuiltInParameter)(-1140122)).Set(width);
		((Element)newTray).get_Parameter((BuiltInParameter)(-1140121)).Set(height);
		if ((direction == "UP" || direction == "DOWN") && angleValue == 90.0)
		{
			Line rotationAxis = Line.CreateBound(turnPoint, turnPoint + XYZ.BasisZ);
			ElementTransformUtils.RotateElement(doc, ((Element)newTray).Id, rotationAxis, Math.PI / 2.0);
		}
		MEPLibrary.CreateElbowCabletrayFitting(doc, newTray, mainTray);
	}

	private XYZ CalculateEndPoint(XYZ turnPoint, XYZ elsePoint, XYZ mainDir, double offset, string direction, double angleValue)
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Expected O, but got Unknown
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected O, but got Unknown
		if (direction == "UP" || direction == "DOWN")
		{
			double sign = ((direction == "UP") ? 1.0 : (-1.0));
			if (angleValue == 90.0)
			{
				return new XYZ(turnPoint.X, turnPoint.Y, turnPoint.Z + sign * offset);
			}
			XYZ elsePointZ = new XYZ(elsePoint.X, elsePoint.Y, turnPoint.Z);
			XYZ flatDir = (turnPoint - elsePointZ).Normalize();
			XYZ point90 = turnPoint + flatDir * offset;
			return new XYZ(point90.X, point90.Y, point90.Z + sign * offset);
		}
		double angleRad;
		if (angleValue == 90.0)
		{
			angleRad = ((direction == "LEFT") ? (Math.PI / 2.0) : (-Math.PI / 2.0));
			Transform rotate = Transform.CreateRotation(XYZ.BasisZ, angleRad);
			XYZ dir90 = rotate.OfVector(mainDir);
			return turnPoint + dir90 * offset;
		}
		angleRad = ((direction == "LEFT") ? (Math.PI / 4.0) : (-Math.PI / 4.0));
		Transform rotate2 = Transform.CreateRotation(XYZ.BasisZ, angleRad);
		XYZ dir91 = rotate2.OfVector(mainDir);
		return turnPoint + dir91 * (Math.Sqrt(2.0) * offset);
	}

	private XYZ GetTurnPoint(Element element, XYZ pickPoint)
	{
		MEPCurve mepCurve = (MEPCurve)(object)((element is MEPCurve) ? element : null);
		if (mepCurve == null)
		{
			return null;
		}
		List<Connector> openConnectors = (from Connector c in (IEnumerable)mepCurve.ConnectorManager.Connectors
			where !c.IsConnected
			select c).ToList();
		if (openConnectors.Count == 1)
		{
			return openConnectors[0].Origin;
		}
		if (openConnectors.Count >= 2)
		{
			return openConnectors.OrderBy((Connector c) => c.Origin.DistanceTo(pickPoint)).First().Origin;
		}
		Location location = ((Element)mepCurve).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		return (((Curve)line).GetEndPoint(0).DistanceTo(pickPoint) < ((Curve)line).GetEndPoint(1).DistanceTo(pickPoint)) ? ((Curve)line).GetEndPoint(0) : ((Curve)line).GetEndPoint(1);
	}

	private XYZ GetOppositeEndPoint(Element element, XYZ currentPoint)
	{
		Location location = ((element is MEPCurve) ? element : null).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		XYZ p0 = curve.GetEndPoint(0);
		XYZ p1 = curve.GetEndPoint(1);
		return (p0.DistanceTo(currentPoint) < 0.001) ? p1 : p0;
	}
}
