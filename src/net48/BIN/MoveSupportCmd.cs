using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class MoveSupportCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0352: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_0367: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Expected O, but got Unknown
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_034b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ba: Invalid comparison between Unknown and I4
		//IL_0270: Unknown result type (might be due to invalid IL or missing references)
		//IL_0276: Invalid comparison between Unknown and I4
		UIApplication uiapp = commandData.Application;
		UIDocument uidoc = uiapp.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			Reference refSupport = uidoc.Selection.PickObject((ObjectType)1, "Chọn support cần di dời (Click 1)");
			if (refSupport == null)
			{
				return Result.Cancelled;
			}
			Element supportElem = doc.GetElement(refSupport);
			if (supportElem == null)
			{
				return Result.Failed;
			}
			Reference refCurve = uidoc.Selection.PickObject((ObjectType)2, (ISelectionFilter)(object)new MEPCurveSelectionFilter(), "Chọn 1 điểm trên Pipe, Duct, hoặc Cable Tray (Click 2)");
			if (refCurve == null)
			{
				return Result.Cancelled;
			}
			XYZ rawPickPoint = refCurve.GlobalPoint;
			XYZ targetPoint = rawPickPoint;
			Element element = doc.GetElement(refCurve.ElementId);
			MEPCurve mepCurve = (MEPCurve)(object)((element is MEPCurve) ? element : null);
			if (mepCurve != null)
			{
				Location location = ((Element)mepCurve).Location;
				LocationCurve locCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
				if (locCurve != null && (GeometryObject)(object)locCurve.Curve != (GeometryObject)null)
				{
					IntersectionResult projection = locCurve.Curve.Project(rawPickPoint);
					if (projection != null)
					{
						targetPoint = projection.XYZPoint;
					}
				}
			}
			Transaction tx = new Transaction(doc, "Move Support");
			try
			{
				tx.Start();
				Location location2 = supportElem.Location;
				LocationPoint lp = (LocationPoint)(object)((location2 is LocationPoint) ? location2 : null);
				if (lp != null)
				{
					lp.Point = targetPoint;
					doc.Regenerate();
					double diffZ = Math.Abs(lp.Point.Z - targetPoint.Z);
					if (diffZ > 0.0001)
					{
						BuiltInParameter[] bipSearch = (BuiltInParameter[])(object)new BuiltInParameter[2]
						{
							(BuiltInParameter)(-1001364),
							(BuiltInParameter)(-1001360)
						};
						bool paramSet = false;
						BuiltInParameter[] array = bipSearch;
						foreach (BuiltInParameter bip in array)
						{
							Parameter p = supportElem.get_Parameter(bip);
							if (p != null && !((APIObject)p).IsReadOnly && (int)p.StorageType == 2)
							{
								double currentVal = p.AsDouble();
								double newVal = currentVal + (targetPoint.Z - lp.Point.Z);
								p.Set(newVal);
								doc.Regenerate();
								paramSet = true;
								break;
							}
						}
						if (!paramSet)
						{
							string[] offsetParams = new string[4] { "Offset", "Elevation", "Offset from Host", "Elevation from Level" };
							string[] array2 = offsetParams;
							foreach (string paramName in array2)
							{
								Parameter p2 = supportElem.LookupParameter(paramName);
								if (p2 != null && !((APIObject)p2).IsReadOnly && (int)p2.StorageType == 2)
								{
									double currentVal2 = p2.AsDouble();
									double newVal2 = currentVal2 + (targetPoint.Z - lp.Point.Z);
									p2.Set(newVal2);
									doc.Regenerate();
									break;
								}
							}
						}
					}
				}
				else
				{
					XYZ currentPoint = null;
					BoundingBoxXYZ bbox = supportElem.get_BoundingBox(null);
					if (bbox != null)
					{
						currentPoint = (bbox.Min + bbox.Max) / 2.0;
					}
					if (currentPoint != null)
					{
						XYZ delta = targetPoint - currentPoint;
						ElementTransformUtils.MoveElement(doc, supportElem.Id, delta);
					}
				}
				tx.Commit();
			}
			finally
			{
				((IDisposable)tx)?.Dispose();
			}
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}
}
