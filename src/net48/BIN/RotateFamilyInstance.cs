using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class RotateFamilyInstance : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Expected O, but got Unknown
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			Reference pickedRef = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new FamilyInstanceSelectionFilter(), "Chọn một đối tượng làm mẫu");
			Element element = doc.GetElement(pickedRef);
			FamilyInstance sample = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
			string familyName = ((Element)sample.Symbol.Family).Name;
			string typeName = ((Element)sample.Symbol).Name;
			List<FamilyInstance> allInView = (from FamilyInstance fi in (IEnumerable)new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id).OfClass(typeof(FamilyInstance))
				where ((Element)fi.Symbol.Family).Name == familyName && ((Element)fi.Symbol).Name == typeName
				select fi).ToList();
			RotateFamilyInstanceWindow dialog = new RotateFamilyInstanceWindow(familyName, typeName, allInView.Count);
			if (dialog.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			double angleRadians = dialog.AngleValue * Math.PI / 180.0;
			List<FamilyInstance> targets = new List<FamilyInstance>();
			if (dialog.IsSelectMode)
			{
				try
				{
					IList<Reference> pickedRefs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new FamilyInstanceSelectionFilter(familyName, typeName), "Quét chọn các Family Instance cùng loại cần xoay");
					foreach (Reference r in pickedRefs)
					{
						List<FamilyInstance> list = targets;
						Element element2 = doc.GetElement(r);
						list.Add((FamilyInstance)(object)((element2 is FamilyInstance) ? element2 : null));
					}
				}
				catch (Autodesk.Revit.Exceptions.OperationCanceledException)
				{
					return Result.Cancelled;
				}
			}
			else
			{
				targets = allInView;
			}
			int count = 0;
			Transaction trans = new Transaction(doc, "Rotate Families");
			try
			{
				trans.Start();
				foreach (FamilyInstance fi2 in targets)
				{
					if (RotateElement(doc, fi2, angleRadians))
					{
						count++;
					}
				}
				trans.Commit();
			}
			finally
			{
				((IDisposable)trans)?.Dispose();
			}
			MessageBox.Show($"Thành công: {count}/{targets.Count}", "Kết quả");
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private bool RotateElement(Document doc, FamilyInstance fi, double rad)
	{
		Location location = ((Element)fi).Location;
		LocationPoint lp = (LocationPoint)(object)((location is LocationPoint) ? location : null);
		object obj2;
		if (lp == null)
		{
			Location location2 = ((Element)fi).Location;
			Location obj = ((location2 is LocationCurve) ? location2 : null);
			obj2 = ((obj != null) ? ((LocationCurve)obj).Curve.Evaluate(0.5, true) : null);
		}
		else
		{
			obj2 = lp.Point;
		}
		XYZ origin = (XYZ)obj2;
		if (origin == null)
		{
			return false;
		}
		Line axis = Line.CreateBound(origin, origin + XYZ.BasisZ);
		ElementTransformUtils.RotateElement(doc, ((Element)fi).Id, axis, rad);
		return true;
	}
}
