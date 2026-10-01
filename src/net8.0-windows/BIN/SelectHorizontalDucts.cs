using System;
using System.Collections;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectHorizontalDucts : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Invalid comparison between Unknown and I4
		if (element != null && element.Category.GetIdInt() == -2008000)
		{
			Duct duct = (Duct)(object)((element is Duct) ? element : null);
			if (duct != null)
			{
				ConnectorManager cm = ((MEPCurve)duct).ConnectorManager;
				if (cm != null)
				{
					{
						IEnumerator enumerator = cm.Connectors.GetEnumerator();
						try
						{
							if (enumerator.MoveNext())
							{
								Connector c = (Connector)enumerator.Current;
								if ((int)c.Shape != 1)
								{
									return false;
								}
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
					}
				}
			}
			Location location = element.Location;
			LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
			if (locCurve != null)
			{
				Curve curve = locCurve.Curve;
				Line line = (Line)(object)((curve is Line) ? curve : null);
				if (line != null)
				{
					XYZ startPt = ((Curve)line).GetEndPoint(0);
					XYZ endPt = ((Curve)line).GetEndPoint(1);
					double horizontalLength = Math.Sqrt(Math.Pow(endPt.X - startPt.X, 2.0) + Math.Pow(endPt.Y - startPt.Y, 2.0));
					if (horizontalLength < 0.001)
					{
						return false;
					}
					double deltaZ = Math.Abs(startPt.Z - endPt.Z);
					double totalLength = ((Curve)line).Length;
					if (totalLength > 0.0)
					{
						double slope = deltaZ / totalLength;
						if (slope >= 0.05 && slope <= 1.0)
						{
							return false;
						}
					}
				}
			}
			return true;
		}
		return false;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return false;
	}
}
