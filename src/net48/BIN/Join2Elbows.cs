using System;
using System.Collections;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class Join2Elbows : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Invalid comparison between Unknown and I4
		//IL_0419: Unknown result type (might be due to invalid IL or missing references)
		//IL_042a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_042e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Expected O, but got Unknown
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Expected O, but got Unknown
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_025c: Expected O, but got Unknown
		//IL_02b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0412: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		View view = doc.ActiveView;
		TaskDialog mainDialog = new TaskDialog("BIM Tool - Join Elbows");
		mainDialog.MainInstruction = "Hướng dẫn sử dụng";
		mainDialog.MainContent = "Vui lòng chọn ĐOẠN ỐNG nằm giữa 2 Elbow (Co) để thực hiện gộp.";
		mainDialog.CommonButtons = (TaskDialogCommonButtons)9;
		mainDialog.DefaultButton = (TaskDialogResult)1;
		if ((int)mainDialog.Show() == 2)
		{
			return Result.Cancelled;
		}
		try
		{
			Reference refPipe0 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new PipeFilter(), "Vui lòng chọn đoạn ống nằm giữa 2 Elbow...");
			Element element = doc.GetElement(refPipe0);
			Pipe pipe0 = (Pipe)(object)((element is Pipe) ? element : null);
			FamilyInstance elbow1 = null;
			FamilyInstance elbow2 = null;
			Pipe pipe1 = null;
			Pipe pipe2 = null;
			foreach (Connector connector in ((MEPCurve)pipe0).ConnectorManager.Connectors)
			{
				Connector c2 = connector;
				foreach (Connector allRef in c2.AllRefs)
				{
					Connector neighbor = allRef;
					Element owner = neighbor.Owner;
					FamilyInstance fi = (FamilyInstance)(object)((owner is FamilyInstance) ? owner : null);
					if (fi != null)
					{
						if (elbow1 == null)
						{
							elbow1 = fi;
						}
						else
						{
							elbow2 = fi;
						}
					}
				}
			}
			if (elbow1 == null || elbow2 == null)
			{
				TaskDialog.Show("Lỗi", "Đoạn ống được chọn không kết nối đủ với 2 Elbow.");
				return Result.Failed;
			}
			pipe1 = FindConnectedPipe(elbow1, ((Element)pipe0).Id);
			pipe2 = FindConnectedPipe(elbow2, ((Element)pipe0).Id);
			if (pipe1 == null || pipe2 == null)
			{
				TaskDialog.Show("Lỗi", "Không tìm thấy ống kết nối phía sau Elbow.");
				return Result.Failed;
			}
			Transaction trans = new Transaction(doc, "Gộp Elbow Cố Định Ống");
			try
			{
				trans.Start();
				Location location = ((Element)pipe1).Location;
				Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
				Line line1 = (Line)(object)((curve is Line) ? curve : null);
				Location location2 = ((Element)pipe2).Location;
				Curve curve2 = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve;
				Line line2 = (Line)(object)((curve2 is Line) ? curve2 : null);
				DisconnectAll(elbow2);
				doc.Delete(((Element)pipe0).Id);
				Connector connE1_ToE2 = null;
				foreach (Connector connector2 in elbow1.MEPModel.ConnectorManager.Connectors)
				{
					Connector c3 = connector2;
					if (((Curve)line1).Project(c3.Origin).Distance > 0.01)
					{
						connE1_ToE2 = c3;
						break;
					}
				}
				if (connE1_ToE2 == null)
				{
					return Result.Failed;
				}
				Connector connE2_ToE1 = GetClosestConnector((Element)(object)elbow2, connE1_ToE2.Origin);
				XYZ moveVecToE1 = connE1_ToE2.Origin - connE2_ToE1.Origin;
				ElementTransformUtils.MoveElement(doc, ((Element)elbow2).Id, moveVecToE1);
				connE1_ToE2.ConnectTo(connE2_ToE1);
				double extensionLength = 16.404199475065617;
				XYZ p0 = ((Curve)line2).GetEndPoint(0);
				XYZ p1 = ((Curve)line2).GetEndPoint(1);
				XYZ direction = line2.Direction;
				XYZ newStart = p0 - direction * extensionLength;
				XYZ newEnd = p1 + direction * extensionLength;
				Line extendedLine = Line.CreateBound(newStart, newEnd);
				Connector connE2_ToP2 = ((IEnumerable)elbow2.MEPModel.ConnectorManager.Connectors).Cast<Connector>().First((Connector c) => !c.IsConnected);
				IntersectionResult proj = ((Curve)extendedLine).Project(connE2_ToP2.Origin);
				XYZ moveVecToAxis = proj.XYZPoint - connE2_ToP2.Origin;
				ElementTransformUtils.MoveElement(doc, ((Element)elbow2).Id, moveVecToAxis);
				XYZ newE2_Point = GetOpenConnector(elbow2).Origin;
				UpdatePipeLength(pipe2, line2, newE2_Point);
				GetOpenConnector(elbow2).ConnectTo(GetClosestConnector((Element)(object)pipe2, newE2_Point));
				trans.Commit();
			}
			finally
			{
				((IDisposable)trans)?.Dispose();
			}
			return Result.Succeeded;
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
	}

	private Pipe FindConnectedPipe(FamilyInstance elbow, ElementId excludeId)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		foreach (Connector connector in elbow.MEPModel.ConnectorManager.Connectors)
		{
			Connector c = connector;
			foreach (Connector allRef in c.AllRefs)
			{
				Connector neighbor = allRef;
				Element owner = neighbor.Owner;
				Pipe p = (Pipe)(object)((owner is Pipe) ? owner : null);
				if (p != null && ((Element)p).Id != excludeId)
				{
					return p;
				}
			}
		}
		return null;
	}

	private void DisconnectAll(FamilyInstance fi)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		foreach (Connector connector in fi.MEPModel.ConnectorManager.Connectors)
		{
			Connector c = connector;
			if (!c.IsConnected)
			{
				continue;
			}
			foreach (Connector allRef in c.AllRefs)
			{
				Connector neighbor = allRef;
				c.DisconnectFrom(neighbor);
			}
		}
	}

	private void UpdatePipeLength(Pipe pipe, Line originalLine, XYZ connectPoint)
	{
		XYZ pStart = ((Curve)originalLine).GetEndPoint(0);
		XYZ pEnd = ((Curve)originalLine).GetEndPoint(1);
		if (pStart.DistanceTo(connectPoint) < pEnd.DistanceTo(connectPoint))
		{
			Location location = ((Element)pipe).Location;
			((LocationCurve)((location is LocationCurve) ? location : null)).Curve = (Curve)(object)Line.CreateBound(connectPoint, pEnd);
		}
		else
		{
			Location location2 = ((Element)pipe).Location;
			((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve = (Curve)(object)Line.CreateBound(pStart, connectPoint);
		}
	}

	private Connector GetOpenConnector(FamilyInstance fi)
	{
		return ((IEnumerable)fi.MEPModel.ConnectorManager.Connectors).Cast<Connector>().FirstOrDefault((Connector c) => !c.IsConnected);
	}

	private Connector GetClosestConnector(Element el, XYZ point)
	{
		FamilyInstance fi = (FamilyInstance)(object)((el is FamilyInstance) ? el : null);
		ConnectorManager cm = ((fi != null) ? fi.MEPModel.ConnectorManager : ((MEPCurve)((el is Pipe) ? el : null)).ConnectorManager);
		return (from Connector c in (IEnumerable)cm.Connectors
			orderby c.Origin.DistanceTo(point)
			select c).FirstOrDefault();
	}
}
