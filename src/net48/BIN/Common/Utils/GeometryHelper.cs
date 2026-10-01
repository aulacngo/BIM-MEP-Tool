using System;
using Autodesk.Revit.DB;

namespace BIN.Common.Utils;

public static class GeometryHelper
{
	public static XYZ LineIntersectPlane(Line line, Plane plane)
	{
		XYZ normal = plane.Normal;
		XYZ origin = plane.Origin;
		XYZ pStart = ((Curve)line).GetEndPoint(0);
		XYZ pEnd = ((Curve)line).GetEndPoint(1);
		XYZ direction = (pEnd - pStart).Normalize();
		double denominator = normal.DotProduct(direction);
		if (Math.Abs(denominator) < 1E-09)
		{
			return null;
		}
		double t = (normal.DotProduct(origin) - normal.DotProduct(pStart)) / denominator;
		return pStart + direction * t;
	}

	public static XYZ FindPointOnLine(Line line, double distance, bool fromStart)
	{
		XYZ p1 = (fromStart ? ((Curve)line).GetEndPoint(0) : ((Curve)line).GetEndPoint(1));
		XYZ p2 = (fromStart ? ((Curve)line).GetEndPoint(1) : ((Curve)line).GetEndPoint(0));
		XYZ dir = (p2 - p1).Normalize();
		return p1 + dir * distance;
	}

	public static double ToRadian(double degree)
	{
		return degree * Math.PI / 180.0;
	}

	public static bool IsVertical(XYZ p1, XYZ p2, double tolerance = 0.001)
	{
		return Math.Abs(p1.X - p2.X) < tolerance && Math.Abs(p1.Y - p2.Y) < tolerance;
	}

	public static XYZ SnapPointToCurve(Curve curve, XYZ point)
	{
		IntersectionResult result = curve.Project(point);
		if (result == null)
		{
			return point;
		}
		return curve.Evaluate(result.Parameter, false);
	}
}
