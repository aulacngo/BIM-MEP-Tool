using Document = Autodesk.Revit.DB.Document;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class PlaceSupport : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c5: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			List<FamilySymbol> supportTypes = ((IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2001140)).WhereElementIsElementType()).Cast<FamilySymbol>().ToList();
			supportTypes.AddRange(((IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2008055)).WhereElementIsElementType()).Cast<FamilySymbol>());
			supportTypes.AddRange(((IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2008016)).WhereElementIsElementType()).Cast<FamilySymbol>());
			PlaceSupportWindow window = new PlaceSupportWindow(supportTypes);
			if (window.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			double distanceStart = window.DistanceStart;
			double distanceOffset = window.DistanceOffset;
			FamilySymbol symbol = window.SelectedSymbol;
			int mepType = window.MEPType;
			if (symbol == null)
			{
				return Result.Failed;
			}
			Reference r = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new LinkInstanceFilter(), "Chọn file Link kết cấu");
			Element element = doc.GetElement(r.ElementId);
			RevitLinkInstance linkInstance = (RevitLinkInstance)(object)((element is RevitLinkInstance) ? element : null);
			Document linkDoc = linkInstance.GetLinkDocument();
			Transform linkTransform = ((Instance)linkInstance).GetTotalTransform();
			List<Floor> allLinkedFloors = ((IEnumerable)new FilteredElementCollector(linkDoc).OfClass(typeof(Floor))).Cast<Floor>().ToList();
			return (Result)(mepType switch
			{
				0 => ExecutePipeSupport(uidoc, doc, distanceStart, distanceOffset, symbol, linkTransform, allLinkedFloors), 
				1 => ExecuteDuctSupport(uidoc, doc, distanceStart, distanceOffset, symbol, linkTransform, allLinkedFloors), 
				2 => ExecuteRoundDuctSupport(uidoc, doc, distanceStart, distanceOffset, symbol, linkTransform, allLinkedFloors), 
				3 => ExecuteCableTraySupport(uidoc, doc, distanceStart, distanceOffset, symbol, linkTransform, allLinkedFloors), 
				4 => ExecuteConduitSupport(uidoc, doc, distanceStart, distanceOffset, symbol, linkTransform, allLinkedFloors), 
				_ => Result.Failed, 
			});
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private Result ExecutePipeSupport(UIDocument uidoc, Document doc, double distanceStart, double distanceOffset, FamilySymbol symbol, Transform linkTransform, List<Floor> allLinkedFloors)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_032c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0341: Unknown result type (might be due to invalid IL or missing references)
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectPipe(), "Chọn các ống Pipe");
		List<Pipe> pipes = refs.Select((Reference x) => doc.GetElement(x.ElementId)).OfType<Pipe>().ToList();
		Transaction t = new Transaction(doc, "Place Supports");
		try
		{
			t.Start();
			if (!symbol.IsActive)
			{
				symbol.Activate();
			}
			foreach (Pipe pipe in pipes)
			{
				Location location = ((Element)pipe).Location;
				LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
				Curve curve = locationCurve.Curve;
				Line pipeLine = (Line)(object)((curve is Line) ? curve : null);
				double outsideDiameter = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140238)).AsDouble();
				Element element = doc.GetElement(((Element)pipe).get_Parameter((BuiltInParameter)(-1114000)).AsElementId());
				Level level = (Level)(object)((element is Level) ? element : null);
				Line flatLine = FlatLine(pipeLine);
				List<XYZ> supportPositions = CalculateSupportPositions(flatLine, distanceStart, distanceOffset);
				double insulationThickness = GetPipeInsulationThickness(doc, pipe);
				foreach (XYZ pos in supportPositions)
				{
					XYZ pointOnLine = PointOnLine(pos, pipeLine);
					if (pointOnLine == null)
					{
						continue;
					}
					Face bestFace = null;
					XYZ pointOnFace = null;
					double minDistance = double.MaxValue;
					foreach (Floor f in allLinkedFloors)
					{
						Face bottomFace = GetBottomFace(f);
						if ((GeometryObject)(object)bottomFace == (GeometryObject)null)
						{
							continue;
						}
						XYZ intersectPoint = IntersectPointInHost(pointOnLine, bottomFace, linkTransform);
						if (intersectPoint != null)
						{
							double dist = intersectPoint.Z - pointOnLine.Z;
							if (dist > 0.0 && dist < minDistance)
							{
								minDistance = dist;
								pointOnFace = intersectPoint;
								bestFace = bottomFace;
							}
						}
					}
					FamilyInstance support = doc.Create.NewFamilyInstance(pos, symbol, level, (StructuralType)0);
					((Element)support).get_Parameter((BuiltInParameter)(-1001360)).Set(pointOnLine.Z - level.Elevation);
					Parameter pDia = ((Element)support).LookupParameter("Pipe_Outside Diameter");
					if (pDia != null)
					{
						pDia.Set(outsideDiameter + 2.0 * insulationThickness);
					}
					if (pointOnFace != null)
					{
						Parameter pDist = ((Element)support).LookupParameter("PipeCenterToFloor");
						if (pDist != null)
						{
							pDist.Set(minDistance);
						}
					}
					XYZ direction = flatLine.Direction;
					double angle = XYZ.BasisY.AngleTo(direction);
					if (XYZ.BasisY.CrossProduct(direction).Z < 0.0)
					{
						angle = 0.0 - angle;
					}
					ElementTransformUtils.RotateElement(doc, ((Element)support).Id, Line.CreateBound(pos, pos + XYZ.BasisZ), angle);
				}
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteDuctSupport(UIDocument uidoc, Document doc, double distanceStart, double distanceOffset, FamilySymbol symbol, Transform linkTransform, List<Floor> allLinkedFloors)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_036f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0384: Unknown result type (might be due to invalid IL or missing references)
		//IL_0388: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectHorizontalDucts(), "Chọn các ống gió");
		List<Duct> ducts = refs.Select((Reference x) => doc.GetElement(x.ElementId)).OfType<Duct>().ToList();
		Transaction t = new Transaction(doc, "Place Supports");
		try
		{
			t.Start();
			if (!symbol.IsActive)
			{
				symbol.Activate();
			}
			foreach (Duct duct in ducts)
			{
				Location location = ((Element)duct).Location;
				LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
				Curve curve = locationCurve.Curve;
				Line ductLine = (Line)(object)((curve is Line) ? curve : null);
				double width = ((MEPCurve)duct).Width;
				double height = ((MEPCurve)duct).Height;
				Element element = doc.GetElement(((Element)duct).get_Parameter((BuiltInParameter)(-1114000)).AsElementId());
				Level level = (Level)(object)((element is Level) ? element : null);
				double insulationThickness = GetDuctInsulationThickness(doc, duct);
				List<XYZ> supportPositions = CalculateSupportPositions(ductLine, distanceStart, distanceOffset);
				foreach (XYZ pos in supportPositions)
				{
					XYZ pointOnLine = PointOnLine(pos, ductLine);
					if (pointOnLine == null)
					{
						continue;
					}
					Face bestFace = null;
					XYZ pointOnFace = null;
					double minDistance = double.MaxValue;
					foreach (Floor f in allLinkedFloors)
					{
						Face bottomFace = GetBottomFace(f);
						if ((GeometryObject)(object)bottomFace == (GeometryObject)null)
						{
							continue;
						}
						XYZ intersectPoint = IntersectPointInHost(pointOnLine, bottomFace, linkTransform);
						if (intersectPoint != null)
						{
							double dist = intersectPoint.Z - pointOnLine.Z;
							if (dist > 0.0 && dist < minDistance)
							{
								minDistance = dist;
								pointOnFace = intersectPoint;
								bestFace = bottomFace;
							}
						}
					}
					FamilyInstance support = doc.Create.NewFamilyInstance(pos, symbol, level, (StructuralType)0);
					((Element)support).get_Parameter((BuiltInParameter)(-1001360)).Set(pointOnLine.Z - level.Elevation - height / 2.0 - insulationThickness);
					Parameter WidthSupport = ((Element)support).LookupParameter("ElementWidth");
					if (WidthSupport != null)
					{
						WidthSupport.Set(width + 2.0 * insulationThickness);
					}
					Parameter HeightSupport = ((Element)support).LookupParameter("ElementHeight");
					if (HeightSupport != null)
					{
						HeightSupport.Set(height + 2.0 * insulationThickness);
					}
					if (pointOnFace != null)
					{
						Parameter pDist = ((Element)support).LookupParameter("ElementBottomToFloor");
						if (pDist != null)
						{
							pDist.Set(minDistance + height / 2.0 + insulationThickness);
						}
					}
					XYZ direction = ductLine.Direction;
					double angle = XYZ.BasisX.AngleTo(direction);
					if (XYZ.BasisX.CrossProduct(direction).Z < 0.0)
					{
						angle = 0.0 - angle;
					}
					ElementTransformUtils.RotateElement(doc, ((Element)support).Id, Line.CreateBound(pos, pos + XYZ.BasisZ), angle);
				}
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteRoundDuctSupport(UIDocument uidoc, Document doc, double distanceStart, double distanceOffset, FamilySymbol symbol, Transform linkTransform, List<Floor> allLinkedFloors)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_032b: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectRoundDuct(), "Chọn các ống gió");
		List<Duct> ducts = refs.Select((Reference x) => doc.GetElement(x.ElementId)).OfType<Duct>().ToList();
		Transaction t = new Transaction(doc, "Place Supports");
		try
		{
			t.Start();
			if (!symbol.IsActive)
			{
				symbol.Activate();
			}
			foreach (Duct duct in ducts)
			{
				Location location = ((Element)duct).Location;
				LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
				Curve curve = locationCurve.Curve;
				Line ductLine = (Line)(object)((curve is Line) ? curve : null);
				double diameter = ((MEPCurve)duct).Diameter;
				Element element = doc.GetElement(((Element)duct).get_Parameter((BuiltInParameter)(-1114000)).AsElementId());
				Level level = (Level)(object)((element is Level) ? element : null);
				double insulationThickness = GetDuctInsulationThickness(doc, duct);
				List<XYZ> supportPositions = CalculateSupportPositions(ductLine, distanceStart, distanceOffset);
				foreach (XYZ pos in supportPositions)
				{
					XYZ pointOnLine = PointOnLine(pos, ductLine);
					if (pointOnLine == null)
					{
						continue;
					}
					Face bestFace = null;
					XYZ pointOnFace = null;
					double minDistance = double.MaxValue;
					foreach (Floor f in allLinkedFloors)
					{
						Face bottomFace = GetBottomFace(f);
						if ((GeometryObject)(object)bottomFace == (GeometryObject)null)
						{
							continue;
						}
						XYZ intersectPoint = IntersectPointInHost(pointOnLine, bottomFace, linkTransform);
						if (intersectPoint != null)
						{
							double dist = intersectPoint.Z - pointOnLine.Z;
							if (dist > 0.0 && dist < minDistance)
							{
								minDistance = dist;
								pointOnFace = intersectPoint;
								bestFace = bottomFace;
							}
						}
					}
					FamilyInstance support = doc.Create.NewFamilyInstance(pos, symbol, level, (StructuralType)0);
					((Element)support).get_Parameter((BuiltInParameter)(-1001360)).Set(pointOnLine.Z - level.Elevation);
					Parameter diameterSupport = ((Element)support).LookupParameter("Duct_Outside Diameter");
					if (diameterSupport != null)
					{
						diameterSupport.Set(diameter + 2.0 * insulationThickness);
					}
					if (pointOnFace != null)
					{
						Parameter pDist = ((Element)support).LookupParameter("DuctCenterToFloor");
						if (pDist != null)
						{
							pDist.Set(minDistance);
						}
					}
					XYZ direction = ductLine.Direction;
					double angle = XYZ.BasisX.AngleTo(direction);
					if (XYZ.BasisX.CrossProduct(direction).Z < 0.0)
					{
						angle = 0.0 - angle;
					}
					ElementTransformUtils.RotateElement(doc, ((Element)support).Id, Line.CreateBound(pos, pos + XYZ.BasisZ), angle);
				}
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteCableTraySupport(UIDocument uidoc, Document doc, double distanceStart, double distanceOffset, FamilySymbol symbol, Transform linkTransform, List<Floor> allLinkedFloors)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0340: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		//IL_0359: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectCableTray(), "Chọn các Cable tray");
		List<CableTray> trays = refs.Select((Reference x) => doc.GetElement(x.ElementId)).OfType<CableTray>().ToList();
		Transaction t = new Transaction(doc, "Place Supports");
		try
		{
			t.Start();
			if (!symbol.IsActive)
			{
				symbol.Activate();
			}
			foreach (CableTray tray in trays)
			{
				Location location = ((Element)tray).Location;
				LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
				Curve curve = locationCurve.Curve;
				Line trayLine = (Line)(object)((curve is Line) ? curve : null);
				double width = ((MEPCurve)tray).Width;
				double height = ((MEPCurve)tray).Height;
				Element element = doc.GetElement(((Element)tray).get_Parameter((BuiltInParameter)(-1114000)).AsElementId());
				Level level = (Level)(object)((element is Level) ? element : null);
				List<XYZ> supportPositions = CalculateSupportPositions(trayLine, distanceStart, distanceOffset);
				foreach (XYZ pos in supportPositions)
				{
					XYZ pointOnLine = PointOnLine(pos, trayLine);
					if (pointOnLine == null)
					{
						continue;
					}
					Face bestFace = null;
					XYZ pointOnFace = null;
					double minDistance = double.MaxValue;
					foreach (Floor f in allLinkedFloors)
					{
						Face bottomFace = GetBottomFace(f);
						if ((GeometryObject)(object)bottomFace == (GeometryObject)null)
						{
							continue;
						}
						XYZ intersectPoint = IntersectPointInHost(pointOnLine, bottomFace, linkTransform);
						if (intersectPoint != null)
						{
							double dist = intersectPoint.Z - pointOnLine.Z;
							if (dist > 0.0 && dist < minDistance)
							{
								minDistance = dist;
								pointOnFace = intersectPoint;
								bestFace = bottomFace;
							}
						}
					}
					FamilyInstance support = doc.Create.NewFamilyInstance(pos, symbol, level, (StructuralType)0);
					((Element)support).get_Parameter((BuiltInParameter)(-1001360)).Set(pointOnLine.Z - level.Elevation - height / 2.0);
					Parameter WidthSupport = ((Element)support).LookupParameter("ElementWidth");
					if (WidthSupport != null)
					{
						WidthSupport.Set(width);
					}
					Parameter HeightSupport = ((Element)support).LookupParameter("ElementHeight");
					if (HeightSupport != null)
					{
						HeightSupport.Set(height);
					}
					if (pointOnFace != null)
					{
						Parameter pDist = ((Element)support).LookupParameter("ElementBottomToFloor");
						if (pDist != null)
						{
							pDist.Set(minDistance + height / 2.0);
						}
					}
					XYZ direction = trayLine.Direction;
					double angle = XYZ.BasisX.AngleTo(direction);
					if (XYZ.BasisX.CrossProduct(direction).Z < 0.0)
					{
						angle = 0.0 - angle;
					}
					ElementTransformUtils.RotateElement(doc, ((Element)support).Id, Line.CreateBound(pos, pos + XYZ.BasisZ), angle);
				}
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return Result.Succeeded;
	}

	private Result ExecuteConduitSupport(UIDocument uidoc, Document doc, double distanceStart, double distanceOffset, FamilySymbol symbol, Transform linkTransform, List<Floor> allLinkedFloors)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0410: Unknown result type (might be due to invalid IL or missing references)
		//IL_0414: Unknown result type (might be due to invalid IL or missing references)
		IList<Reference> refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectConduit(), "Chọn các ống Conduit");
		List<Conduit> conduits = refs.Select((Reference x) => doc.GetElement(x.ElementId)).OfType<Conduit>().ToList();
		Transaction t = new Transaction(doc, "Place Supports");
		try
		{
			t.Start();
			if (!symbol.IsActive)
			{
				symbol.Activate();
			}
			foreach (Conduit conduit in conduits)
			{
				Location location = ((Element)conduit).Location;
				LocationCurve locationCurve = (LocationCurve)(object)((location is LocationCurve) ? location : null);
				Curve curve = locationCurve.Curve;
				Line conduitLine = (Line)(object)((curve is Line) ? curve : null);
				double outsideDiameter = 0.0;
				Parameter pOuter = ((Element)conduit).get_Parameter((BuiltInParameter)(-1140127));
				if (pOuter != null)
				{
					outsideDiameter = pOuter.AsDouble();
				}
				else
				{
					Parameter pDia = ((Element)conduit).get_Parameter((BuiltInParameter)(-1140123));
					outsideDiameter = ((pDia == null) ? ((MEPCurve)conduit).Diameter : pDia.AsDouble());
				}
				Level level = null;
				Parameter pLevel = ((Element)conduit).get_Parameter((BuiltInParameter)(-1114000));
				if (pLevel != null && pLevel.AsElementId() != ElementId.InvalidElementId)
				{
					Element element = doc.GetElement(pLevel.AsElementId());
					level = (Level)(object)((element is Level) ? element : null);
				}
				if (level == null && ((MEPCurve)conduit).ReferenceLevel != null)
				{
					level = ((MEPCurve)conduit).ReferenceLevel;
				}
				Line flatLine = FlatLine(conduitLine);
				List<XYZ> supportPositions = CalculateSupportPositions(flatLine, distanceStart, distanceOffset);
				double insulationThickness = 0.0;
				foreach (XYZ pos in supportPositions)
				{
					XYZ pointOnLine = PointOnLine(pos, conduitLine);
					if (pointOnLine == null)
					{
						continue;
					}
					Face bestFace = null;
					XYZ pointOnFace = null;
					double minDistance = double.MaxValue;
					foreach (Floor f in allLinkedFloors)
					{
						Face bottomFace = GetBottomFace(f);
						if ((GeometryObject)(object)bottomFace == (GeometryObject)null)
						{
							continue;
						}
						XYZ intersectPoint = IntersectPointInHost(pointOnLine, bottomFace, linkTransform);
						if (intersectPoint != null)
						{
							double dist = intersectPoint.Z - pointOnLine.Z;
							if (dist > 0.0 && dist < minDistance)
							{
								minDistance = dist;
								pointOnFace = intersectPoint;
								bestFace = bottomFace;
							}
						}
					}
					FamilyInstance support = doc.Create.NewFamilyInstance(pos, symbol, level, (StructuralType)0);
					if (level != null)
					{
						((Element)support).get_Parameter((BuiltInParameter)(-1001360)).Set(pointOnLine.Z - level.Elevation);
					}
					Parameter pDia2 = ((Element)support).LookupParameter("Conduit_Outside Diameter");
					if (pDia2 == null)
					{
						pDia2 = ((Element)support).LookupParameter("Pipe_Outside Diameter");
					}
					if (pDia2 != null)
					{
						pDia2.Set(outsideDiameter + 2.0 * insulationThickness);
					}
					if (pointOnFace != null)
					{
						Parameter pDist = ((Element)support).LookupParameter("ConduitCenterToFloor");
						if (pDist == null)
						{
							pDist = ((Element)support).LookupParameter("PipeCenterToFloor");
						}
						if (pDist != null)
						{
							pDist.Set(minDistance);
						}
					}
					XYZ direction = flatLine.Direction;
					double angle = XYZ.BasisY.AngleTo(direction);
					if (XYZ.BasisY.CrossProduct(direction).Z < 0.0)
					{
						angle = 0.0 - angle;
					}
					ElementTransformUtils.RotateElement(doc, ((Element)support).Id, Line.CreateBound(pos, pos + XYZ.BasisZ), angle);
				}
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		return Result.Succeeded;
	}

	private static double GetPipeInsulationThickness(Document doc, Pipe pipe)
	{
		ICollection<ElementId> insulationIds = InsulationLiningBase.GetInsulationIds(doc, ((Element)pipe).Id);
		if (insulationIds == null || insulationIds.Count == 0)
		{
			return 0.0;
		}
		foreach (ElementId id in insulationIds)
		{
			Element element = doc.GetElement(id);
			PipeInsulation insulation = (PipeInsulation)(object)((element is PipeInsulation) ? element : null);
			if (insulation != null)
			{
				Parameter p = ((Element)insulation).get_Parameter((BuiltInParameter)(-1114359));
				if (p != null)
				{
					return p.AsDouble();
				}
			}
		}
		return 0.0;
	}

	private static double GetDuctInsulationThickness(Document doc, Duct duct)
	{
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Invalid comparison between Unknown and I4
		if (doc == null || duct == null)
		{
			return 0.0;
		}
		ICollection<ElementId> insulationIds = InsulationLiningBase.GetInsulationIds(doc, ((Element)duct).Id);
		if (insulationIds == null || insulationIds.Count == 0)
		{
			return 0.0;
		}
		foreach (ElementId id in insulationIds)
		{
			if (id == (ElementId)null || id == ElementId.InvalidElementId)
			{
				continue;
			}
			Element element = doc.GetElement(id);
			DuctInsulation insulation = (DuctInsulation)(object)((element is DuctInsulation) ? element : null);
			if (insulation != null)
			{
				Parameter p = ((Element)insulation).get_Parameter((BuiltInParameter)(-1114358));
				if (p != null && (int)p.StorageType == 2)
				{
					return p.AsDouble();
				}
			}
		}
		return 0.0;
	}

	private List<XYZ> CalculateSupportPositions(Line flatLine, double startDist, double offsetDist)
	{
		List<XYZ> positions = new List<XYZ>();
		double len = ((Curve)flatLine).Length;
		XYZ pStart = ((Curve)flatLine).GetEndPoint(0);
		XYZ pEnd = ((Curve)flatLine).GetEndPoint(1);
		if (len <= startDist)
		{
			positions.Add(((Curve)flatLine).Evaluate(0.5, true));
			return positions;
		}
		for (double currentDist = startDist; currentDist <= len; currentDist += offsetDist)
		{
			positions.Add(((Curve)flatLine).Evaluate(currentDist / len, true));
		}
		XYZ lastSupportPos = positions.Last();
		double distFromLastToEnd = lastSupportPos.DistanceTo(pEnd);
		if (distFromLastToEnd > startDist * 2.0)
		{
			double finalSupportParam = (len - startDist) / len;
			if (finalSupportParam > 0.0 && finalSupportParam < 1.0)
			{
				positions.Add(((Curve)flatLine).Evaluate(finalSupportParam, true));
			}
		}
		return positions;
	}

	private XYZ IntersectPointInHost(XYZ hostPoint, Face linkedFace, Transform linkTransform)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		XYZ pointInLink = linkTransform.Inverse.OfPoint(hostPoint);
		Line rayInLink = Line.CreateBound(pointInLink, pointInLink + linkTransform.Inverse.OfVector(XYZ.BasisZ) * 50.0);
		IntersectionResultArray array = default(IntersectionResultArray);
		linkedFace.Intersect(rayInLink, out array);
		if (array != null && !array.IsEmpty)
		{
			return linkTransform.OfPoint(array.get_Item(0).XYZPoint);
		}
		return null;
	}

	private Face GetBottomFace(Floor floor)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Expected O, but got Unknown
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected O, but got Unknown
		Options opt = new Options
		{
			ComputeReferences = true,
			DetailLevel = (ViewDetailLevel)3
		};
		GeometryElement ge = ((Element)floor).get_Geometry(opt);
		if ((GeometryObject)(object)ge == (GeometryObject)null)
		{
			return null;
		}
		foreach (GeometryObject obj in ge)
		{
			Solid solid = (Solid)(object)((obj is Solid) ? obj : null);
			if (solid == null || !(solid.Volume > 0.0))
			{
				continue;
			}
			foreach (Face face in solid.Faces)
			{
				Face f = face;
				PlanarFace pf = (PlanarFace)(object)((f is PlanarFace) ? f : null);
				if (pf != null && pf.FaceNormal.IsAlmostEqualTo(-XYZ.BasisZ))
				{
					return f;
				}
			}
		}
		return null;
	}

	private Line FlatLine(Line line)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_004a: Expected O, but got Unknown
		XYZ p0 = ((Curve)line).GetEndPoint(0);
		XYZ p1 = ((Curve)line).GetEndPoint(1);
		return Line.CreateBound(new XYZ(p0.X, p0.Y, 0.0), new XYZ(p1.X, p1.Y, 0.0));
	}

	private XYZ PointOnLine(XYZ point, Line pipeLine)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		XYZ pTop = new XYZ(point.X, point.Y, ((Curve)pipeLine).GetEndPoint(0).Z + 20.0);
		XYZ pBot = new XYZ(point.X, point.Y, ((Curve)pipeLine).GetEndPoint(0).Z - 20.0);
		Line ray = Line.CreateBound(pBot, pTop);
		IntersectionResultArray array = default(IntersectionResultArray);
		((Curve)pipeLine).Intersect((Curve)(object)ray, out array);
		return (array != null && array.Size > 0) ? array.get_Item(0).XYZPoint : null;
	}
}
