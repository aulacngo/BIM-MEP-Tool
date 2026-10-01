using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class ReducerSelectFilter : ISelectionFilter
{
	private const double tolerance = 1E-06;

	private const double Size15mmInFeet = 0.049212598425196846;

	private const double Size25mmInFeet = 0.08202099737532809;

	private const double Size65mmInFeet = 0.21325459317585302;

	public bool AllowElement(Element element)
	{
		if (element.Category == null || element.Category.GetIdInt() != -2008049)
		{
			return false;
		}
		FamilyInstance fitting = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
		if (fitting != null)
		{
			MEPModel mEPModel = fitting.MEPModel;
			if (((mEPModel != null) ? mEPModel.ConnectorManager : null) != null)
			{
				ConnectorSet connectors = fitting.MEPModel.ConnectorManager.Connectors;
				List<Connector> pipingConnectors = (from Connector c in (IEnumerable)connectors
					where (int)c.Domain == 3
					select c).ToList();
				if (pipingConnectors.Count != 2)
				{
					return false;
				}
				double diameter1 = pipingConnectors[0].Radius * 2.0;
				double diameter2 = pipingConnectors[1].Radius * 2.0;
				if (Math.Abs(diameter1 - diameter2) <= 1E-06)
				{
					return false;
				}
				if (diameter1 >= 0.21325359317585302 || diameter2 >= 0.21325359317585302)
				{
					return false;
				}
				double[] diameters = new double[2] { diameter1, diameter2 };
				Array.Sort(diameters);
				if (Math.Abs(diameters[0] - 0.049212598425196846) <= 1E-06 && Math.Abs(diameters[1] - 0.08202099737532809) <= 1E-06)
				{
					return false;
				}
				return true;
			}
		}
		return false;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return true;
	}
}
