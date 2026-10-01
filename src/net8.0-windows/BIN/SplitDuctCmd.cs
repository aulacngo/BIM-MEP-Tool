using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class SplitDuctCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		IList<Reference> listOb = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectDucts(), "Pick Ducts");
		SplitDuctWindow window = new SplitDuctWindow();
		window.ShowDialog();
		if (window.DialogResult == true)
		{
			double distance = window.Distance / 304.8;
			Transaction t = new Transaction(doc, "Split Duct Fixed");
			try
			{
				t.Start();
				foreach (Reference reference in listOb)
				{
					Element element = doc.GetElement(reference);
					Duct originDuct = (Duct)(object)((element is Duct) ? element : null);
					if (originDuct != null)
					{
						SplitDuctFixed(doc, originDuct, distance);
					}
				}
				t.Commit();
				TaskDialog.Show("Thông báo", $"Đã hoàn thành ngắt ống gió và đặt Union cho {listOb.Count} ống gió");
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
		}
		return Result.Succeeded;
	}

	private void SplitDuctFixed(Document doc, Duct originDuct, double distance)
	{
		List<ElementId> allSegments = new List<ElementId>();
		Queue<ElementId> queue = new Queue<ElementId>();
		queue.Enqueue(((Element)originDuct).Id);
		int safetyCounter = 0;
		int maxIterations = 1000;
		while (queue.Count > 0 && safetyCounter < maxIterations)
		{
			safetyCounter++;
			ElementId currentDuctId = queue.Dequeue();
			Element element = doc.GetElement(currentDuctId);
			Duct currentDuct = (Duct)(object)((element is Duct) ? element : null);
			if (currentDuct == null)
			{
				continue;
			}
			Location location = ((Element)currentDuct).Location;
			LocationCurve lc = (LocationCurve)(object)((location is LocationCurve) ? location : null);
			if (lc == null)
			{
				allSegments.Add(currentDuctId);
				continue;
			}
			Curve curve = lc.Curve;
			Line line = (Line)(object)((curve is Line) ? curve : null);
			if ((GeometryObject)(object)line == (GeometryObject)null)
			{
				allSegments.Add(currentDuctId);
				continue;
			}
			double currentLength = ((Curve)line).Length;
			if (currentLength <= distance + 0.001)
			{
				allSegments.Add(currentDuctId);
				continue;
			}
			XYZ startPt = ((Curve)line).GetEndPoint(0);
			XYZ endPt = ((Curve)line).GetEndPoint(1);
			XYZ direction = (endPt - startPt).Normalize();
			XYZ splitPoint = startPt + direction * distance;
			try
			{
				ElementId newDuctId = MechanicalUtils.BreakCurve(doc, currentDuctId, splitPoint);
				doc.Regenerate();
				if (newDuctId != (ElementId)null && newDuctId != ElementId.InvalidElementId)
				{
					queue.Enqueue(currentDuctId);
					queue.Enqueue(newDuctId);
				}
				else
				{
					allSegments.Add(currentDuctId);
				}
			}
			catch
			{
				allSegments.Add(currentDuctId);
			}
		}
		for (int i = 0; i < allSegments.Count; i++)
		{
			for (int j = i + 1; j < allSegments.Count; j++)
			{
				Element element2 = doc.GetElement(allSegments[i]);
				Duct dt1 = (Duct)(object)((element2 is Duct) ? element2 : null);
				Element element3 = doc.GetElement(allSegments[j]);
				Duct dt2 = (Duct)(object)((element3 is Duct) ? element3 : null);
				if (dt1 != null && dt2 != null)
				{
					CreateUnionFitting(doc, dt1, dt2);
				}
			}
		}
	}

	private void CreateUnionFitting(Document doc, Duct duct1, Duct duct2)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
		Connector c1 = null;
		Connector c2 = null;
		foreach (Connector connector in ((MEPCurve)duct1).ConnectorManager.Connectors)
		{
			Connector con1 = connector;
			foreach (Connector connector2 in ((MEPCurve)duct2).ConnectorManager.Connectors)
			{
				Connector con2 = connector2;
				if (con1.Origin.DistanceTo(con2.Origin) < 0.01)
				{
					c1 = con1;
					c2 = con2;
					break;
				}
			}
			if (c1 != null)
			{
				break;
			}
		}
		if (c1 != null && c2 != null)
		{
			try
			{
				doc.Create.NewUnionFitting(c1, c2);
			}
			catch
			{
			}
		}
	}
}
