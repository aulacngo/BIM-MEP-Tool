using System;
using System.Collections;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ConnectVentRiser : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Invalid comparison between Unknown and I4
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		TaskDialog mainDialog = new TaskDialog("BIM Tool - Connect Riser");
		mainDialog.MainInstruction = "Hướng dẫn kết nối ống hơi";
		mainDialog.MainContent = "Vui lòng chọn ỐNG HƠI trước, sau đó chọn ỐNG CHÍNH.\n\nLưu ý: Nếu chọn sai thứ tự, tool sẽ không hoạt động chính xác.";
		mainDialog.CommonButtons = (TaskDialogCommonButtons)9;
		if ((int)mainDialog.Show() == 2)
		{
			return Result.Cancelled;
		}
		Pipe pipe1;
		XYZ globalPoint1;
		Pipe pipe2;
		try
		{
			Reference pickPipe1 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new FilterPipe(), "Bước 1: Chọn ống hơi (Vent Riser)...");
			Element element = doc.GetElement(pickPipe1);
			pipe1 = (Pipe)(object)((element is Pipe) ? element : null);
			globalPoint1 = pickPipe1.GlobalPoint;
			Reference pickPipe2 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new FilterPipe(), "Bước 2: Chọn ống chính (Main Pipe)...");
			Element element2 = doc.GetElement(pickPipe2);
			pipe2 = (Pipe)(object)((element2 is Pipe) ? element2 : null);
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
		try
		{
			ProcessConnection(doc, pipe1, pipe2, globalPoint1);
			return Result.Succeeded;
		}
		catch (Exception ex2)
		{
			MessageBox.Show(ex2.Message, "Lỗi khi tạo Fitting");
			return Result.Failed;
		}
	}

	private void ProcessConnection(Document doc, Pipe pipe1, Pipe pipe2, XYZ globalPoint1)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03dd: Expected O, but got Unknown
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Expected O, but got Unknown
		//IL_03e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Expected O, but got Unknown
		//IL_0427: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_043f: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b9: Unknown result type (might be due to invalid IL or missing references)
		Location location = ((Element)pipe1).Location;
		Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
		Line pLine1 = (Line)(object)((curve is Line) ? curve : null);
		Location location2 = ((Element)pipe2).Location;
		Curve curve2 = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve;
		Line pLine2 = (Line)(object)((curve2 is Line) ? curve2 : null);
		XYZ breakPoint1 = LineIntersectPlane(pLine1, pLine1.Direction, globalPoint1);
		XYZ projectionPoint1 = LineIntersectPlane(pLine2, pLine2.Direction, breakPoint1);
		double distanceOffset = breakPoint1.DistanceTo(projectionPoint1);
		XYZ breakPoint2 = new XYZ(projectionPoint1.X, projectionPoint1.Y, projectionPoint1.Z - distanceOffset);
		ElementId systemTypeId = ((Element)pipe1).get_Parameter((BuiltInParameter)(-1140334)).AsElementId();
		ElementId pipeTypeId = ((Element)pipe1).GetTypeId();
		ElementId levelId = ((Element)pipe1).LevelId;
		double diameter = ((MEPCurve)pipe1).Diameter;
		TransactionGroup tg = new TransactionGroup(doc, "Connect Riser Vent");
		try
		{
			tg.Start();
			FamilyInstance tee1 = null;
			FamilyInstance tee2 = null;
			bool directSuccess = false;
			Transaction t = new Transaction(doc, "Create Fittings Direct");
			try
			{
				t.Start();
				try
				{
					Pipe pipe45 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, breakPoint1, breakPoint2);
					((Element)pipe45).LookupParameter("Diameter").Set(diameter);
					ElementId newId1 = PlumbingUtils.BreakCurve(doc, ((Element)pipe1).Id, breakPoint1);
					ElementId newId2 = PlumbingUtils.BreakCurve(doc, ((Element)pipe2).Id, breakPoint2);
					Element element = doc.GetElement(newId1);
					Pipe newPipe1 = (Pipe)(object)((element is Pipe) ? element : null);
					Element element2 = doc.GetElement(newId2);
					Pipe newPipe2 = (Pipe)(object)((element2 is Pipe) ? element2 : null);
					tee1 = CreateTeeFittingAtPoint(pipe1, newPipe1, pipe45, breakPoint1);
					tee2 = CreateTeeFittingAtPoint(pipe2, newPipe2, pipe45, breakPoint2);
					if (tee1 != null && tee2 != null)
					{
						t.Commit();
						directSuccess = true;
					}
					else
					{
						t.RollBack();
					}
				}
				catch
				{
					t.RollBack();
				}
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			if (!directSuccess)
			{
				Transaction t2 = new Transaction(doc, "Create Fittings Fallback");
				try
				{
					t2.Start();
					XYZ projectionPoint2 = new XYZ(breakPoint1.X, breakPoint1.Y, breakPoint2.Z);
					Pipe pipeTemp1 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, breakPoint1, projectionPoint1);
					Pipe pipeTemp2 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, breakPoint2, projectionPoint2);
					((Element)pipeTemp1).LookupParameter("Diameter").Set(diameter);
					((Element)pipeTemp2).LookupParameter("Diameter").Set(diameter);
					ElementId newId3 = PlumbingUtils.BreakCurve(doc, ((Element)pipe1).Id, breakPoint1);
					ElementId newId4 = PlumbingUtils.BreakCurve(doc, ((Element)pipe2).Id, breakPoint2);
					Element element3 = doc.GetElement(newId3);
					Pipe newPipe3 = (Pipe)(object)((element3 is Pipe) ? element3 : null);
					Element element4 = doc.GetElement(newId4);
					Pipe newPipe4 = (Pipe)(object)((element4 is Pipe) ? element4 : null);
					tee1 = CreateTeeFittingAtPoint(pipe1, newPipe3, pipeTemp1, breakPoint1);
					tee2 = CreateTeeFittingAtPoint(pipe2, newPipe4, pipeTemp2, breakPoint2);
					if (tee1 == null || tee2 == null)
					{
						throw new Exception("Không thể tạo phụ kiện kết nối. Hãy kiểm tra xem vị trí click hoặc khoảng cách giữa các ống có phù hợp không.");
					}
					doc.Delete(((Element)pipeTemp1).Id);
					doc.Delete(((Element)pipeTemp2).Id);
					Parameter angleParam1 = FindAngleParameter((Element)(object)tee1);
					if (angleParam1 != null && !((APIObject)angleParam1).IsReadOnly)
					{
						angleParam1.Set(Math.PI * 3.0 / 4.0);
					}
					Parameter angleParam2 = FindAngleParameter((Element)(object)tee2);
					if (angleParam2 != null && !((APIObject)angleParam2).IsReadOnly)
					{
						angleParam2.Set(Math.PI / 4.0);
					}
					doc.Regenerate();
					Connector cn1 = GetUnusedConnector(tee1.MEPModel);
					Connector cn2 = GetUnusedConnector(tee2.MEPModel);
					if (cn1 == null || cn2 == null)
					{
						throw new Exception("Không tìm thấy đầu nối trống trên phụ kiện Tee để tạo đường ống xéo.");
					}
					Pipe pipe46 = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, cn1.Origin, cn2.Origin);
					((Element)pipe46).LookupParameter("Diameter").Set(diameter);
					ConnectAtPoint(cn1.Origin, pipe46, tee1);
					ConnectAtPoint(cn2.Origin, pipe46, tee2);
					t2.Commit();
				}
				finally
				{
					((IDisposable)t2)?.Dispose();
				}
			}
			Transaction t3 = new Transaction(doc, "Refresh Model");
			try
			{
				t3.Start();
				if (tee2 != null)
				{
					ElementTransformUtils.MoveElement(doc, ((Element)tee2).Id, XYZ.BasisZ);
					doc.Regenerate();
					ElementTransformUtils.MoveElement(doc, ((Element)tee2).Id, -XYZ.BasisZ);
				}
				t3.Commit();
			}
			finally
			{
				((IDisposable)t3)?.Dispose();
			}
			tg.Assimilate();
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
	}

	private XYZ LineIntersectPlane(Line line, XYZ planeNormal, XYZ planeOrigin)
	{
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		XYZ normalize = (ep - sp).Normalize();
		double distance = (planeNormal.DotProduct(planeOrigin) - planeNormal.DotProduct(sp)) / planeNormal.DotProduct(normalize);
		return sp + distance * normalize;
	}

	private Connector FindConnectorAtPoint(Pipe pipe, XYZ point)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		if (pipe == null || ((MEPCurve)pipe).ConnectorManager == null)
		{
			return null;
		}
		Connector target = null;
		double minDist = double.MaxValue;
		foreach (Connector connector in ((MEPCurve)pipe).ConnectorManager.Connectors)
		{
			Connector cn = connector;
			double d = cn.Origin.DistanceTo(point);
			if (d < minDist)
			{
				minDist = d;
				target = cn;
			}
		}
		if (target != null && minDist < 0.05)
		{
			return target;
		}
		return null;
	}

	private Connector GetUnusedConnector(MEPModel model)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		if (model == null || model.ConnectorManager == null)
		{
			return null;
		}
		IEnumerator enumerator = model.ConnectorManager.UnusedConnectors.GetEnumerator();
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
		return null;
	}

	private FamilyInstance CreateTeeFittingAtPoint(Pipe p1, Pipe p2, Pipe p3, XYZ pt)
	{
		if (p1 == null || p2 == null || p3 == null)
		{
			return null;
		}
		Connector c1 = FindConnectorAtPoint(p1, pt);
		Connector c2 = FindConnectorAtPoint(p2, pt);
		Connector c3 = FindConnectorAtPoint(p3, pt);
		if (c1 == null || c2 == null || c3 == null)
		{
			return null;
		}
		try
		{
			return ((Element)p1).Document.Create.NewTeeFitting(c1, c2, c3);
		}
		catch
		{
			try
			{
				return ((Element)p1).Document.Create.NewTeeFitting(c2, c1, c3);
			}
			catch
			{
				try
				{
					return ((Element)p1).Document.Create.NewTeeFitting(c1, c3, c2);
				}
				catch
				{
					try
					{
						return ((Element)p1).Document.Create.NewTeeFitting(c2, c3, c1);
					}
					catch
					{
						return null;
					}
				}
			}
		}
	}

	private Parameter FindAngleParameter(Element elem)
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		string[] commonNames = new string[7] { "Angle", "Góc", "角度", "Ángulo", "AngleRot", "Góc xoay", "Góc quay" };
		string[] array = commonNames;
		foreach (string name in array)
		{
			Parameter p = elem.LookupParameter(name);
			if (p != null)
			{
				return p;
			}
		}
		foreach (Parameter parameter in elem.Parameters)
		{
			Parameter p2 = parameter;
			string name2 = p2.Definition.Name;
			if (name2.IndexOf("Angle", StringComparison.OrdinalIgnoreCase) >= 0 || name2.IndexOf("Góc", StringComparison.OrdinalIgnoreCase) >= 0 || name2.IndexOf("角度", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return p2;
			}
		}
		return null;
	}

	private void ConnectAtPoint(XYZ point, Pipe pipe, FamilyInstance tee)
	{
		Connector cn = GetConnectorClosestTo(((MEPCurve)pipe).ConnectorManager.Connectors, point);
		Connector cb = GetConnectorClosestTo(tee.MEPModel.ConnectorManager.Connectors, point);
		if (cn != null && cb != null)
		{
			cn.ConnectTo(cb);
		}
	}

	private Connector GetConnectorClosestTo(ConnectorSet connectors, XYZ p)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		Connector target = null;
		double minDist = double.MaxValue;
		foreach (Connector connector in connectors)
		{
			Connector c = connector;
			double d = c.Origin.DistanceTo(p);
			if (d < minDist)
			{
				target = c;
				minDist = d;
			}
		}
		return target;
	}
}
