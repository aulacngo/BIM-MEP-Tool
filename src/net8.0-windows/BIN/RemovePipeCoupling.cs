using System;
using System.Collections;
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
public class RemovePipeCoupling : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		RemovePipeCouplingWindow ui = new RemovePipeCouplingWindow();
		ui.ShowDialog();
		if (!ui.IsContinue)
		{
			return Result.Cancelled;
		}
		try
		{
			ISelectionFilter flangeFilter = (ISelectionFilter)(object)new FlangeSelectionFilter();
			IList<Reference> selectedRefs = uidoc.Selection.PickObjects((ObjectType)1, flangeFilter, "Quet chon cac doi tuong Fitting can loai bo de ket noi truc tiep (nhan Finish)");
			if (selectedRefs == null || selectedRefs.Count == 0)
			{
				return Result.Cancelled;
			}
			Transaction trans = new Transaction(doc, "BIN_Connect Tee and Reducer");
			try
			{
				try { trans.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				trans.Start();
				foreach (Reference @ref in selectedRefs)
				{
					Element element = doc.GetElement(@ref);
					FamilyInstance fitting = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
					if (fitting != null)
					{
						ExecuteConnectionLogic(doc, fitting);
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

	private void ExecuteConnectionLogic(Document doc, FamilyInstance fitting)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Expected O, but got Unknown
		Connector connToPipe = null;
		Connector otherFittingConnector = null;
		Pipe pipe = null;
		if (fitting.MEPModel == null)
		{
			return;
		}
		foreach (Connector connector in fitting.MEPModel.ConnectorManager.Connectors)
		{
			Connector conn = connector;
			if (!conn.IsConnected)
			{
				continue;
			}
			foreach (Connector allRef in conn.AllRefs)
			{
				Connector neighbor = allRef;
				Element owner = neighbor.Owner;
				if (!(owner.Id == ((Element)fitting).Id))
				{
					if (owner is Pipe)
					{
						connToPipe = conn;
						pipe = (Pipe)(object)((owner is Pipe) ? owner : null);
					}
					else if (owner is FamilyInstance)
					{
						otherFittingConnector = neighbor;
					}
				}
			}
		}
		if (pipe == null || otherFittingConnector == null || connToPipe == null)
		{
			return;
		}
		XYZ targetPoint = otherFittingConnector.Origin;
		XYZ oldFittingPoint = connToPipe.Origin;
		Connector pipeConnector = GetConnectedConnector(connToPipe);
		if (pipeConnector != null)
		{
			connToPipe.DisconnectFrom(pipeConnector);
		}
		MovePipeEndPoint(pipe, oldFittingPoint, targetPoint);
		doc.Delete(((Element)fitting).Id);
		Connector newPipeConn = GetClosestConnector(pipe, targetPoint);
		if (newPipeConn == null || newPipeConn.IsConnected)
		{
			return;
		}
		try
		{
			newPipeConn.ConnectTo(otherFittingConnector);
		}
		catch
		{
		}
	}

	private void MovePipeEndPoint(Pipe pipe, XYZ oldPoint, XYZ newPoint)
	{
		Location location = ((Element)pipe).Location;
		LocationCurve lp = (LocationCurve)(object)((location is LocationCurve) ? location : null);
		if (lp == null)
		{
			return;
		}
		Curve curve = lp.Curve;
		Line line = (Line)(object)((curve is Line) ? curve : null);
		if ((GeometryObject)(object)line != (GeometryObject)null)
		{
			XYZ start = ((Curve)line).GetEndPoint(0);
			XYZ end = ((Curve)line).GetEndPoint(1);
			if (start.DistanceTo(oldPoint) < end.DistanceTo(oldPoint))
			{
				lp.Curve = (Curve)(object)Line.CreateBound(newPoint, end);
			}
			else
			{
				lp.Curve = (Curve)(object)Line.CreateBound(start, newPoint);
			}
		}
	}

	private Connector GetClosestConnector(Pipe pipe, XYZ point)
	{
		return (from Connector c in (IEnumerable)((MEPCurve)pipe).ConnectorManager.Connectors
			orderby c.Origin.DistanceTo(point)
			select c).FirstOrDefault();
	}

	private Connector GetConnectedConnector(Connector baseConn)
	{
		return ((IEnumerable)baseConn.AllRefs).Cast<Connector>().FirstOrDefault((Connector c) => c.Owner.Id != baseConn.Owner.Id && (int)c.ConnectorType != 4);
	}
}
