using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;

namespace BIN;

public class MEPLibrary
{
	public static BitmapImage Convert(Bitmap bitmap)
	{
		MemoryStream memory = new MemoryStream();
		bitmap.Save(memory, ImageFormat.Png);
		memory.Position = 0L;
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.StreamSource = memory;
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.EndInit();
		return bitmapImage;
	}

	public static XYZ LineIntersectPlan(Line line, Plane plane)
	{
		XYZ normal = plane.Normal;
		XYZ origin = plane.Origin;
		XYZ sp = ((Curve)line).GetEndPoint(0);
		XYZ ep = ((Curve)line).GetEndPoint(1);
		XYZ normalize = (ep - sp).Normalize();
		double distance = (normal.DotProduct(origin) - normal.DotProduct(sp)) / normal.DotProduct(normalize);
		return sp + distance * normalize;
	}

	public static double ToRadian(double degree)
	{
		return degree * Math.PI / 180.0;
	}

	public static XYZ FindPointOnLineFromStartPoint(Line line, double distance)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		XYZ A = ((Curve)line).GetEndPoint(0);
		XYZ B = ((Curve)line).GetEndPoint(1);
		XYZ BA = B - A;
		double tile = distance / BA.GetLength();
		double x = tile * BA.X + A.X;
		double y = tile * BA.Y + A.Y;
		double z = tile * BA.Z + A.Z;
		return new XYZ(x, y, z);
	}

	public static XYZ FindPointOnLineFromEndPoint(Line line, double distance)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		XYZ A = ((Curve)line).GetEndPoint(0);
		XYZ B = ((Curve)line).GetEndPoint(1);
		XYZ AB = A - B;
		double tile = distance / AB.GetLength();
		double x = tile * AB.X + B.X;
		double y = tile * AB.Y + B.Y;
		double z = tile * AB.Z + B.Z;
		return new XYZ(x, y, z);
	}

	public static void CreateElbowFitting(Document doc, Duct duct1, Duct duct2)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		ConnectorManager cM1 = ((MEPCurve)duct1).ConnectorManager;
		ConnectorManager cM2 = ((MEPCurve)duct2).ConnectorManager;
		ConnectorSet cS1 = cM1.Connectors;
		ConnectorSet cS2 = cM2.Connectors;
		List<Connector> list = new List<Connector>();
		foreach (Connector item in cS1)
		{
			Connector c1 = item;
			foreach (Connector item2 in cS2)
			{
				Connector c2 = item2;
				XYZ o1 = c1.Origin;
				XYZ o2 = c2.Origin;
				double kc = Math.Round(o1.DistanceTo(o2), 3);
				if (kc == 0.0)
				{
					list.Add(c1);
					list.Add(c2);
					break;
				}
			}
		}
		try
		{
			doc.Create.NewElbowFitting(list[0], list[1]);
		}
		catch
		{
		}
	}

	public static void CreateElbowPipeFitting(Document doc, Pipe pipe1, Pipe pipe2)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		ConnectorManager cM1 = ((MEPCurve)pipe1).ConnectorManager;
		ConnectorManager cM2 = ((MEPCurve)pipe2).ConnectorManager;
		ConnectorSet cS1 = cM1.Connectors;
		ConnectorSet cS2 = cM2.Connectors;
		List<Connector> list = new List<Connector>();
		foreach (Connector item in cS1)
		{
			Connector c1 = item;
			foreach (Connector item2 in cS2)
			{
				Connector c2 = item2;
				XYZ o1 = c1.Origin;
				XYZ o2 = c2.Origin;
				double kc = Math.Round(o1.DistanceTo(o2), 3);
				if (kc == 0.0)
				{
					list.Add(c1);
					list.Add(c2);
					break;
				}
			}
		}
		try
		{
			doc.Create.NewElbowFitting(list[0], list[1]);
		}
		catch
		{
		}
	}

	public static void CreateElbowCabletrayFitting(Document doc, CableTray ct1, CableTray ct2)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		ConnectorManager cM1 = ((MEPCurve)ct1).ConnectorManager;
		ConnectorManager cM2 = ((MEPCurve)ct2).ConnectorManager;
		ConnectorSet cS1 = cM1.Connectors;
		ConnectorSet cS2 = cM2.Connectors;
		List<Connector> list = new List<Connector>();
		foreach (Connector item in cS1)
		{
			Connector c1 = item;
			foreach (Connector item2 in cS2)
			{
				Connector c2 = item2;
				XYZ o1 = c1.Origin;
				XYZ o2 = c2.Origin;
				double kc = Math.Round(o1.DistanceTo(o2), 3);
				if (kc == 0.0)
				{
					list.Add(c1);
					list.Add(c2);
					break;
				}
			}
		}
		try
		{
			doc.Create.NewElbowFitting(list[0], list[1]);
		}
		catch
		{
		}
	}

	public static Solid GetMEPSolid(Element ele)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		Options options = new Options
		{
			ComputeReferences = true,
			DetailLevel = (ViewDetailLevel)2
		};
		GeometryElement geometryElement = ele.get_Geometry(options);
		foreach (GeometryObject geoOb in geometryElement)
		{
			Solid solid = (Solid)(object)((geoOb is Solid) ? geoOb : null);
			if (solid != null)
			{
				return solid;
			}
		}
		return null;
	}

	public static bool CheckSolid(Element ele1, Element ele2)
	{
		Solid solid1 = GetMEPSolid(ele1);
		Solid solid2 = GetMEPSolid(ele2);
		Solid intersectSolid = BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, (BooleanOperationsType)2);
		if ((GeometryObject)(object)intersectSolid != (GeometryObject)null && intersectSolid.Volume > 0.0)
		{
			return true;
		}
		return false;
	}

	public static void DeleteElement(Document doc, IList<ElementId> ids, double length, out List<ElementId> result)
	{
		result = new List<ElementId>();
		foreach (ElementId id in ids)
		{
			LocationCurve locationCurve = null;
			Element element = doc.GetElement(id);
			Duct duct = (Duct)(object)((element is Duct) ? element : null);
			if (duct != null)
			{
				Location location = ((Element)duct).Location;
				locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
			}
			Pipe pipe = (Pipe)(object)((element is Pipe) ? element : null);
			if (pipe != null)
			{
				Location location2 = ((Element)pipe).Location;
				locationCurve = (LocationCurve)(object)((location2 is LocationCurve) ? location2 : null);
			}
			CableTray cableTray = (CableTray)(object)((element is CableTray) ? element : null);
			if (cableTray != null)
			{
				Location location3 = ((Element)cableTray).Location;
				locationCurve = (LocationCurve)(object)((location3 is LocationCurve) ? location3 : null);
			}
			double curvelength = locationCurve.Curve.Length;
			double value = Math.Round(curvelength - length, 3);
			if (value == 0.0)
			{
				doc.Delete(id);
			}
			else
			{
				result.Add(id);
			}
		}
	}
}
