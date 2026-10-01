using Document = Autodesk.Revit.DB.Document;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;

namespace BIN;

public class SprinklerFlipperLogic
{
	private const double FIXED_LENGTH_MM = 60.0;

	public void Process(Document doc, FamilyInstance oldSprinkler, FamilySymbol newSymbol)
	{
		//IL_033c: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Expected O, but got Unknown
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		Connector sprConn = GetConnector((Element)(object)oldSprinkler);
		if (sprConn == null || !sprConn.IsConnected)
		{
			return;
		}
		Element nextElem = GetConnectedElement(sprConn);
		if (nextElem == null)
		{
			return;
		}
		Element reducer = null;
		Pipe branchPipe = null;
		if (nextElem.Category != null && nextElem.Category.GetIdInt() == -2008049)
		{
			reducer = nextElem;
			Connector redOutConn = GetOppositeConnector(reducer, GetConnectedConnector(sprConn));
			if (redOutConn == null || !redOutConn.IsConnected)
			{
				return;
			}
			Element connectedElement = GetConnectedElement(redOutConn);
			branchPipe = (Pipe)(object)((connectedElement is Pipe) ? connectedElement : null);
		}
		else
		{
			Pipe p = (Pipe)(object)((nextElem is Pipe) ? nextElem : null);
			if (p != null)
			{
				branchPipe = p;
			}
		}
		if (branchPipe == null)
		{
			return;
		}
		Connector pipeStartConn = GetConnectorConnectedTo((Element)(object)branchPipe, (reducer != null) ? reducer.Id : ((Element)oldSprinkler).Id);
		Connector pipeEndConn = GetOppositeConnector((Element)(object)branchPipe, pipeStartConn);
		if (pipeEndConn == null || !pipeEndConn.IsConnected)
		{
			return;
		}
		Element connectedElement2 = GetConnectedElement(pipeEndConn);
		FamilyInstance mainFitting = (FamilyInstance)(object)((connectedElement2 is FamilyInstance) ? connectedElement2 : null);
		if (mainFitting == null)
		{
			return;
		}
		Line rotationAxis = GetMainRunAxis(mainFitting, GetConnectedConnector(pipeEndConn));
		if ((GeometryObject)(object)rotationAxis == (GeometryObject)null)
		{
			return;
		}
		SubTransaction st = new SubTransaction(doc);
		try
		{
			st.Start();
			try
			{
				List<ElementId> idsToRotate = new List<ElementId>();
				idsToRotate.Add(((Element)branchPipe).Id);
				if (reducer != null)
				{
					idsToRotate.Add(reducer.Id);
				}
				idsToRotate.Add(((Element)mainFitting).Id);
				Connector fittingConn = GetConnectedConnector(pipeEndConn);
				if (fittingConn != null && fittingConn.IsConnected)
				{
					fittingConn.DisconnectFrom(pipeEndConn);
				}
				ElementTransformUtils.RotateElements(doc, (ICollection<ElementId>)idsToRotate, rotationAxis, Math.PI);
				Element hostForNewSprinkler = (Element)((reducer != null) ? ((object)reducer) : ((object)branchPipe));
				doc.Delete(((Element)oldSprinkler).Id);
				doc.Regenerate();
				FamilyInstance newSprinkler = null;
				Connector openConnector = GetOpenConnector(hostForNewSprinkler);
				if (newSymbol != null && openConnector != null)
				{
					if (!newSymbol.IsActive)
					{
						newSymbol.Activate();
					}
					XYZ location = openConnector.Origin;
					Element element = doc.GetElement(((Element)((MEPCurve)branchPipe).ReferenceLevel).Id);
					Level level = (Level)(object)((element is Level) ? element : null);
					newSprinkler = doc.Create.NewFamilyInstance(location, newSymbol, level, (StructuralType)0);
					AlignSprinklerOrientation(doc, newSprinkler, openConnector);
					Connector newSprConn = GetConnector((Element)(object)newSprinkler);
					if (newSprConn != null)
					{
						try
						{
							newSprConn.ConnectTo(openConnector);
						}
						catch
						{
						}
					}
				}
				if (fittingConn != null && pipeEndConn != null)
				{
					Connector newFittingConn = GetClosestConnector((Element)(object)mainFitting, pipeEndConn.Origin);
					if (newFittingConn != null && newFittingConn.Origin.DistanceTo(pipeEndConn.Origin) < 0.01)
					{
						try
						{
							newFittingConn.ConnectTo(pipeEndConn);
						}
						catch
						{
						}
					}
				}
				doc.Regenerate();
				if (newSprinkler != null)
				{
					double targetLengthFeet = 0.19685039370078738;
					RestorePipeLength(doc, branchPipe, targetLengthFeet, newSprinkler, reducer, pipeEndConn);
				}
				st.Commit();
			}
			catch (Exception)
			{
				st.RollBack();
			}
		}
		finally
		{
			((IDisposable)st)?.Dispose();
		}
	}

	private void RestorePipeLength(Document doc, Pipe pipe, double targetLen, FamilyInstance sprinkler, Element reducer, Connector fixedEndConn)
	{
		double currentLen = ((Element)pipe).get_Parameter((BuiltInParameter)(-1004005)).AsDouble();
		double diff = targetLen - currentLen;
		if (Math.Abs(diff) > 0.0032808398950131233)
		{
			Location location = ((Element)pipe).Location;
			Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
			Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
			XYZ pipeDir = pipeLine.Direction;
			XYZ anchorPoint = fixedEndConn.Origin;
			XYZ endPoint0 = ((Curve)pipeLine).GetEndPoint(0);
			XYZ endPoint1 = ((Curve)pipeLine).GetEndPoint(1);
			if (anchorPoint.DistanceTo(endPoint0) > anchorPoint.DistanceTo(endPoint1))
			{
				pipeDir = -pipeDir;
			}
			XYZ moveVector = pipeDir * diff;
			List<ElementId> idsToMove = new List<ElementId>();
			idsToMove.Add(((Element)sprinkler).Id);
			if (reducer != null)
			{
				idsToMove.Add(reducer.Id);
			}
			ElementTransformUtils.MoveElements(doc, (ICollection<ElementId>)idsToMove, moveVector);
		}
	}

	private void AlignSprinklerOrientation(Document doc, FamilyInstance sprinkler, Connector pipeConn)
	{
		XYZ pipeDirection = pipeConn.CoordinateSystem.BasisZ;
		Connector sprConn = GetConnector((Element)(object)sprinkler);
		if (sprConn == null)
		{
			return;
		}
		XYZ sprDirection = sprConn.CoordinateSystem.BasisZ;
		XYZ targetVector = -pipeDirection;
		if (sprDirection.IsAlmostEqualTo(targetVector))
		{
			return;
		}
		XYZ axis = sprDirection.CrossProduct(targetVector);
		if (axis.IsZeroLength())
		{
			if (sprDirection.IsAlmostEqualTo(-targetVector))
			{
				return;
			}
			axis = XYZ.BasisX;
		}
		double angle = sprDirection.AngleTo(targetVector);
		Line rotationLine = Line.CreateBound(sprConn.Origin, sprConn.Origin + axis);
		try
		{
			ElementTransformUtils.RotateElement(doc, ((Element)sprinkler).Id, rotationLine, angle);
		}
		catch
		{
		}
	}

	private Line GetMainRunAxis(FamilyInstance fitting, Connector branchConnOnFitting)
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		ConnectorSet connectors = GetConnectors((Element)(object)fitting);
		if (connectors == null)
		{
			return null;
		}
		List<Connector> conns = new List<Connector>();
		foreach (Connector item in connectors)
		{
			Connector c2 = item;
			conns.Add(c2);
		}
		if (conns.Count >= 3)
		{
			for (int i = 0; i < conns.Count; i++)
			{
				for (int j = i + 1; j < conns.Count; j++)
				{
					double angle = conns[i].CoordinateSystem.BasisZ.AngleTo(conns[j].CoordinateSystem.BasisZ);
					if (Math.Abs(angle - Math.PI) < 0.1)
					{
						return Line.CreateBound(conns[i].Origin, conns[j].Origin);
					}
				}
			}
			List<Connector> mainConns = conns.Where((Connector c) => c.Id != branchConnOnFitting.Id).ToList();
			if (mainConns.Count >= 2)
			{
				return Line.CreateBound(mainConns[0].Origin, mainConns[1].Origin);
			}
		}
		if (conns.Count == 2)
		{
			Connector mainConnOnFitting = conns.FirstOrDefault((Connector c) => c.Id != branchConnOnFitting.Id);
			if (mainConnOnFitting != null && mainConnOnFitting.IsConnected)
			{
				Element mainPipe = GetConnectedElement(mainConnOnFitting);
				Pipe p = (Pipe)(object)((mainPipe is Pipe) ? mainPipe : null);
				if (p != null)
				{
					Location location = ((Element)p).Location;
					Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
					return (Line)(object)((curve is Line) ? curve : null);
				}
				XYZ origin = mainConnOnFitting.Origin;
				XYZ direction = mainConnOnFitting.CoordinateSystem.BasisZ;
				return Line.CreateBound(origin, origin + direction * 10.0);
			}
			if (mainConnOnFitting != null)
			{
				XYZ origin2 = mainConnOnFitting.Origin;
				XYZ direction2 = mainConnOnFitting.CoordinateSystem.BasisZ;
				return Line.CreateBound(origin2, origin2 + direction2 * 10.0);
			}
		}
		return null;
	}

	private Connector GetClosestConnector(Element elem, XYZ point)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		Connector closest = null;
		double minDst = double.MaxValue;
		ConnectorSet connectors = GetConnectors(elem);
		if (connectors == null)
		{
			return null;
		}
		foreach (Connector item in connectors)
		{
			Connector c = item;
			double d = c.Origin.DistanceTo(point);
			if (d < minDst)
			{
				minDst = d;
				closest = c;
			}
		}
		return closest;
	}

	private Connector GetOpenConnector(Element e)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		ConnectorSet conns = GetConnectors(e);
		if (conns == null)
		{
			return null;
		}
		foreach (Connector item in conns)
		{
			Connector c = item;
			if (!c.IsConnected)
			{
				return c;
			}
		}
		return null;
	}

	private Connector GetConnector(Element e)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		ConnectorSet conns = GetConnectors(e);
		if (conns == null)
		{
			return null;
		}
		IEnumerator enumerator = conns.GetEnumerator();
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

	private ConnectorSet GetConnectors(Element e)
	{
		FamilyInstance fi = (FamilyInstance)(object)((e is FamilyInstance) ? e : null);
		if (fi != null && fi.MEPModel != null)
		{
			return fi.MEPModel.ConnectorManager.Connectors;
		}
		Pipe p = (Pipe)(object)((e is Pipe) ? e : null);
		if (p != null)
		{
			return ((MEPCurve)p).ConnectorManager.Connectors;
		}
		return null;
	}

	private Element GetConnectedElement(Connector conn)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Invalid comparison between Unknown and I4
		if (!conn.IsConnected)
		{
			return null;
		}
		foreach (Connector allRef in conn.AllRefs)
		{
			Connector c = allRef;
			if (c.Owner.Id != conn.Owner.Id && (int)c.ConnectorType != 4)
			{
				return c.Owner;
			}
		}
		return null;
	}

	private Connector GetConnectedConnector(Connector conn)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Invalid comparison between Unknown and I4
		if (!conn.IsConnected)
		{
			return null;
		}
		foreach (Connector allRef in conn.AllRefs)
		{
			Connector c = allRef;
			if (c.Owner.Id != conn.Owner.Id && (int)c.ConnectorType != 4)
			{
				return c;
			}
		}
		return null;
	}

	private Connector GetOppositeConnector(Element elem, Connector conn)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		ConnectorSet conns = GetConnectors(elem);
		if (conns == null)
		{
			return null;
		}
		foreach (Connector item in conns)
		{
			Connector c = item;
			if (c.Id != conn.Id)
			{
				return c;
			}
		}
		return null;
	}

	private Connector GetConnectorConnectedTo(Element elem, ElementId targetId)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		ConnectorSet conns = GetConnectors(elem);
		if (conns == null)
		{
			return null;
		}
		foreach (Connector item in conns)
		{
			Connector c = item;
			if (!c.IsConnected)
			{
				continue;
			}
			foreach (Connector allRef in c.AllRefs)
			{
				Connector subC = allRef;
				if (subC.Owner.Id == targetId)
				{
					return c;
				}
			}
		}
		return null;
	}
}
