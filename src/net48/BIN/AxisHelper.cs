using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace BIN;

public static class AxisHelper
{
	public static bool IsValidAxisElement(Element elem)
	{
		if (elem == null) return false;
		if (elem is MEPCurve) return true;
		if (elem is FamilyInstance fi && fi.MEPModel?.ConnectorManager != null)
		{
			Category cat = elem.Category;
			if (cat != null)
			{
#if NET8_0_OR_GREATER
				long catInt = cat.Id.Value;
#else
				long catInt = (long)cat.Id.IntegerValue;
#endif
				if (catInt == (long)BuiltInCategory.OST_PipeFitting ||
					catInt == (long)BuiltInCategory.OST_DuctFitting ||
					catInt == (long)BuiltInCategory.OST_PipeAccessory ||
					catInt == (long)BuiltInCategory.OST_DuctAccessory ||
					catInt == (long)BuiltInCategory.OST_MechanicalEquipment)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static Line GetAxisLine(Element elem, XYZ hintPoint = null)
	{
		if (elem == null) return null;

		if (elem is MEPCurve curve)
		{
			LocationCurve lc = curve.Location as LocationCurve;
			if (lc?.Curve is Line l)
			{
				return Line.CreateUnbound(l.GetEndPoint(0), (l.GetEndPoint(1) - l.GetEndPoint(0)).Normalize());
			}
		}
		else if (elem is FamilyInstance fi && fi.MEPModel?.ConnectorManager != null)
		{
			List<Connector> conns = fi.MEPModel.ConnectorManager.Connectors.Cast<Connector>().ToList();
			if (conns.Count >= 2)
			{
				// Tim 2 connector thang hang doi xung nhat (Main flow axis cua Tee, Van, Reducer, Coupling)
				Connector best1 = null;
				Connector best2 = null;

				for (int i = 0; i < conns.Count; i++)
				{
					for (int j = i + 1; j < conns.Count; j++)
					{
						XYZ dir1 = conns[i].CoordinateSystem.BasisZ;
						XYZ dir2 = conns[j].CoordinateSystem.BasisZ;
						double dot = dir1.DotProduct(dir2);
						if (dot < -0.85) // Doi nghich nhau ~180 deg
						{
							best1 = conns[i];
							best2 = conns[j];
							break;
						}
					}
					if (best1 != null) break;
				}

				if (best1 != null && best2 != null)
				{
					XYZ p1 = best1.Origin;
					XYZ p2 = best2.Origin;
					XYZ v = p2 - p1;
					if (v.GetLength() > 0.001)
					{
						return Line.CreateUnbound(p1, v.Normalize());
					}
					else
					{
						return Line.CreateUnbound(p1, best1.CoordinateSystem.BasisZ.Normalize());
					}
				}
			}

			// Fallback: Lay connector gan hintPoint nhat hoac connector dau tien
			Connector targetConn = conns.FirstOrDefault();
			if (hintPoint != null && conns.Count > 0)
			{
				targetConn = conns.OrderBy(c => c.Origin.DistanceTo(hintPoint)).FirstOrDefault();
			}

			if (targetConn != null)
			{
				XYZ origin = targetConn.Origin;
				XYZ dir = targetConn.CoordinateSystem.BasisZ;
				if (!dir.IsAlmostEqualTo(XYZ.Zero))
				{
					return Line.CreateUnbound(origin, dir.Normalize());
				}
			}
		}

		return null;
	}
}