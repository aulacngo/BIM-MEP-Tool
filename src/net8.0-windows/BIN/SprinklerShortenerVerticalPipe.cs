using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class SprinklerShortenerVerticalPipe : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			PromptWindow prompt = new PromptWindow();
			if (prompt.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			double targetLengthMm = prompt.TargetLengthMm;
			IList<Reference> pickedRefs;
			try
			{
				pickedRefs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SprinklerSelectedFilter(), $"Quet chon cac dau phun de rut ngan ong dung ve {targetLengthMm}mm (nhan Finish)");
			}
			catch
			{
				return Result.Cancelled;
			}
			TransactionGroup tg = new TransactionGroup(doc, "Rut ngan ong dung Sprinkler");
			try
			{
				tg.Start();
				foreach (Reference r in pickedRefs)
				{
					Element element = doc.GetElement(r);
					FamilyInstance spr = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
					if (spr != null)
					{
						try
						{
							ProcessShortenPipe(doc, spr, targetLengthMm);
						}
						catch
						{
						}
					}
				}
				tg.Assimilate();
			}
			catch (Exception)
			{
				if (tg.HasStarted()) tg.RollBack();
				throw;
			}
			finally
			{
				((IDisposable)tg)?.Dispose();
			}
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private void ProcessShortenPipe(Document doc, FamilyInstance sprinkler, double lengthMm)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected O, but got Unknown
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Expected O, but got Unknown
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		Transaction t = new Transaction(doc, "Shorten Pipe");
		try
		{
			t.Start();
			Connector sprConn = GetMainConnector(sprinkler);
			if (sprConn == null || !sprConn.IsConnected)
			{
				return;
			}
			Connector reducerConn = GetConnectedConnector(sprConn);
			Element obj = ((reducerConn != null) ? reducerConn.Owner : null);
			FamilyInstance reducer = (FamilyInstance)(object)((obj is FamilyInstance) ? obj : null);
			if (reducer == null)
			{
				return;
			}
			Pipe verticalPipe = null;
			foreach (Connector connector in reducer.MEPModel.ConnectorManager.Connectors)
			{
				Connector c = connector;
				if (c.Id != reducerConn.Id)
				{
					Connector connectedConnector = GetConnectedConnector(c);
					Element obj2 = ((connectedConnector != null) ? connectedConnector.Owner : null);
					verticalPipe = (Pipe)(object)((obj2 is Pipe) ? obj2 : null);
					if (verticalPipe != null)
					{
						break;
					}
				}
			}
			if (verticalPipe == null)
			{
				return;
			}
			Parameter lenParam = ((Element)verticalPipe).get_Parameter((BuiltInParameter)(-1004005));
			double currentLengthFeet = lenParam.AsDouble();
			double targetLengthFeet = lengthMm / 304.8;
			double moveDistanceFeet = currentLengthFeet - targetLengthFeet;
			if (Math.Abs(moveDistanceFeet) > 0.003)
			{
				Location location = ((Element)verticalPipe).Location;
				Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
				Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
				XYZ pipeDir = pipeLine.Direction.Normalize();
				XYZ moveVec = pipeDir.Multiply(moveDistanceFeet);
				if (moveVec.Z < 0.0)
				{
					moveVec = moveVec.Negate();
				}
				ICollection<ElementId> idsToMove = new List<ElementId>
				{
					((Element)sprinkler).Id,
					((Element)reducer).Id
				};
				ElementTransformUtils.MoveElements(doc, idsToMove, moveVec);
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
	}

	private Connector GetMainConnector(FamilyInstance fi)
	{
		if (fi.MEPModel == null)
		{
			return null;
		}
		return ((IEnumerable)fi.MEPModel.ConnectorManager.Connectors).Cast<Connector>().FirstOrDefault();
	}

	private Connector GetConnectedConnector(Connector source)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Invalid comparison between Unknown and I4
		foreach (Connector allRef in source.AllRefs)
		{
			Connector target = allRef;
			if (target.Owner.Id != source.Owner.Id && (int)target.ConnectorType != 4)
			{
				return target;
			}
		}
		return null;
	}
}
