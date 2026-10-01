using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace BIN.Common.Utils;

public static class MepHelper
{
	public static void CreateElbowFitting(Document doc, Element elem1, Element elem2)
	{
		Connector c1 = GetClosestConnector(elem1, elem2);
		Connector c2 = GetClosestConnector(elem2, elem1);
		if (c1 != null && c2 != null)
		{
			try
			{
				doc.Create.NewElbowFitting(c1, c2);
			}
			catch
			{
			}
		}
	}

	public static Connector GetClosestConnector(Element source, Element target)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Invalid comparison between Unknown and I4
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		ConnectorManager cmSource = GetConnectorManager(source);
		ConnectorManager cmTarget = GetConnectorManager(target);
		if (cmSource == null || cmTarget == null)
		{
			return null;
		}
		Connector bestConnector = null;
		double minDistance = double.MaxValue;
		foreach (Connector connector in cmSource.Connectors)
		{
			Connector c1 = connector;
			if ((int)c1.ConnectorType == 4)
			{
				continue;
			}
			foreach (Connector connector2 in cmTarget.Connectors)
			{
				Connector c2 = connector2;
				double dist = c1.Origin.DistanceTo(c2.Origin);
				if (dist < minDistance)
				{
					minDistance = dist;
					bestConnector = c1;
				}
			}
		}
		if (minDistance < 0.01)
		{
			return bestConnector;
		}
		return bestConnector;
	}

	public static ConnectorManager GetConnectorManager(Element e)
	{
		Duct d = (Duct)(object)((e is Duct) ? e : null);
		if (d != null)
		{
			return ((MEPCurve)d).ConnectorManager;
		}
		Pipe p = (Pipe)(object)((e is Pipe) ? e : null);
		if (p != null)
		{
			return ((MEPCurve)p).ConnectorManager;
		}
		CableTray ct = (CableTray)(object)((e is CableTray) ? e : null);
		if (ct != null)
		{
			return ((MEPCurve)ct).ConnectorManager;
		}
		FamilyInstance fi = (FamilyInstance)(object)((e is FamilyInstance) ? e : null);
		if (fi != null)
		{
			MEPModel mEPModel = fi.MEPModel;
			return (mEPModel != null) ? mEPModel.ConnectorManager : null;
		}
		return null;
	}

	public static bool IsIntersecting(Element e1, Element e2)
	{
		Solid s1 = GetSolid(e1);
		Solid s2 = GetSolid(e2);
		if ((GeometryObject)(object)s1 == (GeometryObject)null || (GeometryObject)(object)s2 == (GeometryObject)null)
		{
			return false;
		}
		try
		{
			Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(s1, s2, (BooleanOperationsType)2);
			return (GeometryObject)(object)intersection != (GeometryObject)null && intersection.Volume > 0.0001;
		}
		catch
		{
			return false;
		}
	}

	private static Solid GetSolid(Element e)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		Options opt = new Options
		{
			ComputeReferences = true,
			DetailLevel = (ViewDetailLevel)3
		};
		GeometryElement geoElem = e.get_Geometry(opt);
		if ((GeometryObject)(object)geoElem == (GeometryObject)null)
		{
			return null;
		}
		foreach (GeometryObject obj in geoElem)
		{
			Solid s = (Solid)(object)((obj is Solid) ? obj : null);
			if (s != null && s.Volume > 0.0)
			{
				return s;
			}
			GeometryInstance inst = (GeometryInstance)(object)((obj is GeometryInstance) ? obj : null);
			if (inst == null)
			{
				continue;
			}
			foreach (GeometryObject instObj in inst.SymbolGeometry)
			{
				Solid s2 = (Solid)(object)((instObj is Solid) ? instObj : null);
				if (s2 != null && s2.Volume > 0.0)
				{
					return s2;
				}
			}
		}
		return null;
	}
}
