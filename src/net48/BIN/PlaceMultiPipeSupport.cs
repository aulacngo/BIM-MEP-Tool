using Document = Autodesk.Revit.DB.Document;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class PlaceMultiPipeSupport : IExternalCommand
{
	public class PipeSectionData
	{
		public Pipe Pipe { get; set; }

		public XYZ PointOnCenter { get; set; }

		public double XOffset { get; set; }

		public double DNValue { get; set; }

		public double TotalOD { get; set; }

		public double BottomZ { get; set; }
	}

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0664: Unknown result type (might be due to invalid IL or missing references)
		//IL_0675: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_067e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Expected O, but got Unknown
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0648: Unknown result type (might be due to invalid IL or missing references)
		//IL_067a: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ef: Expected O, but got Unknown
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			List<FamilySymbol> supportTypes = ((IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2001140)).WhereElementIsElementType()).Cast<FamilySymbol>().ToList();
			MultiPipeSupportWindow window = new MultiPipeSupportWindow(supportTypes);
			if (window.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			double distanceStart = window.DistanceStartFeet;
			double distanceOffset = window.DistanceOffsetFeet;
			FamilySymbol symbol = window.SelectedSymbol;
			if (symbol == null)
			{
				return Result.Failed;
			}
			Reference r = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new LinkInstanceFilterSelector(), "Ch?n file Link k?t c?u");
			Element element = doc.GetElement(r.ElementId);
			RevitLinkInstance linkInstance = (RevitLinkInstance)(object)((element is RevitLinkInstance) ? element : null);
			Document linkDoc = linkInstance.GetLinkDocument();
			Transform linkTransform = ((Instance)linkInstance).GetTotalTransform();
			IList<Reference> refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SelectedPipe(), "Ch?n c\ufffdc ?ng Pipe");
			List<Pipe> pipes = refs.Select((Reference x) => doc.GetElement(x.ElementId)).OfType<Pipe>().ToList();
			Transaction t = new Transaction(doc, "Place Multi Pipe Supports");
			try
			{
				t.Start();
				if (!symbol.IsActive)
				{
					symbol.Activate();
				}
				Pipe refPipe = pipes.OrderByDescending((Pipe p) => ((Curve)(Line)((LocationCurve)((Element)p).Location).Curve).Length).First();
				Location location = ((Element)refPipe).Location;
				Curve curve = ((LocationCurve)((location is LocationCurve) ? location : null)).Curve;
				Line refLine = (Line)(object)((curve is Line) ? curve : null);
				Line flatRefLine = FlatLine(refLine);
				XYZ travelDir = flatRefLine.Direction;
				XYZ crossDir = new XYZ(0.0 - travelDir.Y, travelDir.X, 0.0).Normalize();
				List<XYZ> supportPositions = CalculateSupportPositions(flatRefLine, distanceStart, distanceOffset);
				foreach (XYZ pos in supportPositions)
				{
					List<PipeSectionData> sectionPipes = new List<PipeSectionData>();
					foreach (Pipe pipe in pipes)
					{
						Location location2 = ((Element)pipe).Location;
						Curve curve2 = ((LocationCurve)((location2 is LocationCurve) ? location2 : null)).Curve;
						Line pLine = (Line)(object)((curve2 is Line) ? curve2 : null);
						XYZ pointOnPipe = ProjectPointOntoPipe(pos, travelDir, pLine);
						if (pointOnPipe != null)
						{
							double insulation = GetPipeInsulationThickness(doc, pipe);
							double outerDia = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140238)).AsDouble();
							double totalOD = outerDia + 2.0 * insulation;
							sectionPipes.Add(new PipeSectionData
							{
								Pipe = pipe,
								PointOnCenter = pointOnPipe,
								XOffset = pointOnPipe.DotProduct(crossDir),
								DNValue = GetDNFromOD(totalOD),
								TotalOD = totalOD,
								BottomZ = pointOnPipe.Z - totalOD / 2.0
							});
						}
					}
					if (sectionPipes.Count == 0)
					{
						continue;
					}
					List<PipeSectionData> sortedPipes = sectionPipes.OrderBy((PipeSectionData x) => x.XOffset).ToList();
					double xMin = sortedPipes.First().XOffset - sortedPipes.First().TotalOD / 2.0;
					double xMax = sortedPipes.Last().XOffset + sortedPipes.Last().TotalOD / 2.0;
					double widthA = xMax - xMin;
					double absoluteMinBottomZ = sortedPipes.Min((PipeSectionData x) => x.BottomZ);
					double midXOffset = (xMin + xMax) / 2.0;
					XYZ finalPlacementPoint = pos + (midXOffset - pos.DotProduct(crossDir)) * crossDir;
					finalPlacementPoint = new XYZ(finalPlacementPoint.X, finalPlacementPoint.Y, absoluteMinBottomZ);
					Element element2 = doc.GetElement(((Element)sortedPipes.First().Pipe).get_Parameter((BuiltInParameter)(-1114000)).AsElementId());
					Level level = (Level)(object)((element2 is Level) ? element2 : null);
					FamilyInstance support = doc.Create.NewFamilyInstance(finalPlacementPoint, symbol, level, (StructuralType)0);
					double distanceToFloor = CalculateDistanceToLinkFloor(finalPlacementPoint, linkDoc, linkTransform);
					if (distanceToFloor > 0.0)
					{
						Parameter pFloorDist = ((Element)support).LookupParameter("ElementBottomToFloor");
						if (pFloorDist != null)
						{
							pFloorDist.Set(distanceToFloor);
						}
					}
					double offsetLeft = 125.0 / 381.0;
					for (int i = 1; i <= 6; i++)
					{
						Parameter pUBolt = ((Element)support).LookupParameter($"U BOLT {i}");
						Parameter pDN = ((Element)support).LookupParameter($"DN {i}");
						Parameter pSpace = ((Element)support).LookupParameter($"SPACE U BOLT {i}");
						if (i <= sortedPipes.Count)
						{
							PipeSectionData pipeData = sortedPipes[i - 1];
							if (pUBolt != null)
							{
								pUBolt.Set(1);
							}
							if (pDN != null)
							{
								pDN.Set(pipeData.DNValue / 304.8);
							}
							double originalSpace = pipeData.XOffset - xMin;
							double shiftedSpace = originalSpace + offsetLeft;
							if (pSpace != null)
							{
								pSpace.Set(shiftedSpace);
							}
						}
						else
						{
							if (pUBolt != null)
							{
								pUBolt.Set(0);
							}
							if (pDN != null)
							{
								pDN.Set(0);
							}
							if (pSpace != null)
							{
								pSpace.Set(0);
							}
						}
					}
					Parameter obj = ((Element)support).LookupParameter("ElementWidth");
					if (obj != null)
					{
						obj.Set(widthA);
					}
					((Element)support).get_Parameter((BuiltInParameter)(-1001360)).Set(absoluteMinBottomZ - level.Elevation);
					double angle = XYZ.BasisX.AngleTo(travelDir);
					if (XYZ.BasisX.CrossProduct(travelDir).Z < 0.0)
					{
						angle = 0.0 - angle;
					}
					ElementTransformUtils.RotateElement(doc, ((Element)support).Id, Line.CreateBound(finalPlacementPoint, finalPlacementPoint + XYZ.BasisZ), angle);
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
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
		return Result.Succeeded;
	}

	private double CalculateDistanceToLinkFloor(XYZ startPtHost, Document linkDoc, Transform linkTransform)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Expected O, but got Unknown
		View3D view3d = ((IEnumerable)new FilteredElementCollector(linkDoc).OfClass(typeof(View3D))).Cast<View3D>().FirstOrDefault((View3D v) => !((View)v).IsTemplate);
		if (view3d == null)
		{
			return -1.0;
		}
		XYZ startPtInLink = linkTransform.Inverse.OfPoint(startPtHost);
		XYZ rayDir = XYZ.BasisZ;
		ElementClassFilter floorFilter = new ElementClassFilter(typeof(Floor));
		ReferenceIntersector intersector = new ReferenceIntersector((ElementFilter)(object)floorFilter, (FindReferenceTarget)16, view3d);
		ReferenceWithContext refContext = intersector.FindNearest(startPtInLink, rayDir);
		if (refContext != null)
		{
			return refContext.Proximity;
		}
		return -1.0;
	}

	private double GetDNFromOD(double odFeet)
	{
		double odMm = odFeet * 304.8;
		(double, double)[] dnToMaxOD = new(double, double)[21]
		{
			(15.0, 22.0),
			(20.0, 27.0),
			(25.0, 35.0),
			(32.0, 43.0),
			(40.0, 51.0),
			(50.0, 64.0),
			(65.0, 77.0),
			(80.0, 91.0),
			(100.0, 115.0),
			(125.0, 141.0),
			(150.0, 170.0),
			(175.0, 193.0),
			(200.0, 226.0),
			(225.0, 245.0),
			(250.0, 280.0),
			(300.0, 330.0),
			(350.0, 365.0),
			(400.0, 420.0),
			(450.0, 470.0),
			(500.0, 520.0),
			(600.0, 630.0)
		};
		(double, double)[] array = dnToMaxOD;
		for (int i = 0; i < array.Length; i++)
		{
			(double, double) item = array[i];
			if (odMm <= item.Item2)
			{
				return item.Item1;
			}
		}
		return Math.Ceiling(odMm / 50.0) * 50.0;
	}

	private XYZ ProjectPointOntoPipe(XYZ pos, XYZ direction, Line pipeLine)
	{
		XYZ pipeStart = ((Curve)pipeLine).GetEndPoint(0);
		XYZ pipeDir = pipeLine.Direction;
		double denominator = pipeDir.DotProduct(direction);
		if (Math.Abs(denominator) < 0.0001)
		{
			return null;
		}
		double t = (pos - pipeStart).DotProduct(direction) / denominator;
		return pipeStart + t * pipeDir;
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
				return ((Element)insulation).get_Parameter((BuiltInParameter)(-1114359)).AsDouble();
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
}
