using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class PlaceFamily : IExternalCommand
{
	[Obsolete]
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_0283: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Expected O, but got Unknown
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		//IL_027a: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		TaskDialog.Show("Hướng dẫn", "Vui lòng chọn Link Cad và Family Revit mẫu.");
		ImportInstance filecad = null;
		FamilyInstance instance = null;
		try
		{
			Reference r1 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectCadLink(), "Chọn Link Cad");
			Element element = doc.GetElement(r1);
			filecad = (ImportInstance)(object)((element is ImportInstance) ? element : null);
			Reference r2 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)new SelectFamily(), "Chọn Family Revit");
			Element element2 = doc.GetElement(r2);
			instance = (FamilyInstance)(object)((element2 is FamilyInstance) ? element2 : null);
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
		if (filecad != null && instance != null)
		{
			try
			{
				Element element3 = doc.GetElement(((Element)filecad).GetTypeId());
				CADLinkType cadLinkType = (CADLinkType)(object)((element3 is CADLinkType) ? element3 : null);
				if (cadLinkType == null)
				{
					TaskDialog.Show("Lỗi", "Không lấy được CADLinkType từ Link CAD đã chọn.");
					return Result.Failed;
				}
				string fileName = ((Element)cadLinkType).Name;
				List<string> listBlock = PlaceFamilyUtils.GetListBlockCad(filecad, cadLinkType);
				try { PlaceFamilyUtils.DumpDebugToFile(filecad, cadLinkType); } catch { }
				List<string> listFamily = PlaceFamilyUtils.GetListFamily(doc, instance);
				PlaceFamilyWindow window = new PlaceFamilyWindow(doc, instance, fileName, listBlock, listFamily);
				window.ShowDialog();
				if (window.DialogResult == true)
				{
					string cadBlock = window.blockName;
					string familyname = window.familyName;
					string typename = window.typeName;
					string levelname = window.levelName;
					string selectedCadUnit = window.SelectedCadUnit;
					double elevation = window.elevation / 304.8;
					bool calibrate = window.IsCalibrateBasepoint;
					double deltaX = 0.0;
					double deltaY = 0.0;
					if (calibrate)
					{
						try
						{
							XYZ cadRefPoint = uidoc.Selection.PickPoint(
								ObjectSnapTypes.Intersections | ObjectSnapTypes.Endpoints | ObjectSnapTypes.Centers | ObjectSnapTypes.Nearest,
								"BƯỚC 1/2: Click chọn 1 điểm mốc trên bản vẽ CAD (ví dụ: giao điểm 2 trục X/Y hoặc tim cột)");
							XYZ revitRefPoint = uidoc.Selection.PickPoint(
								ObjectSnapTypes.Intersections | ObjectSnapTypes.Endpoints | ObjectSnapTypes.Centers | ObjectSnapTypes.Nearest,
								"BƯỚC 2/2: Click chọn điểm mốc tương ứng trên Revit (giao điểm trục X/Y hoặc tim cột tương ứng)");
							deltaX = revitRefPoint.X - cadRefPoint.X;
							deltaY = revitRefPoint.Y - cadRefPoint.Y;
						}
						catch (Autodesk.Revit.Exceptions.OperationCanceledException)
						{
							return Result.Cancelled;
						}
					}
					Level level = PlaceFamilyUtils.GetLevelByName(doc, levelname);
					if (level == null)
					{
						TaskDialog.Show("Lỗi", "Không tìm thấy Level: " + levelname);
						return Result.Failed;
					}
					FamilySymbol symbol = PlaceFamilyUtils.GetFamilySymbolByName(doc, instance, familyname, typename);
					if (symbol == null)
					{
						TaskDialog.Show("Lỗi", "Không tìm thấy Family Symbol: " + familyname + " / " + typename);
						return Result.Failed;
					}
					List<XYZ> listBlockCad = PlaceFamilyUtils.GetListBlockCadByName(filecad, cadLinkType, cadBlock, selectedCadUnit);
					if (listBlockCad == null || listBlockCad.Count == 0)
					{
						TaskDialog.Show("Lỗi", "Không tìm thấy block '" + cadBlock + "' trong Link CAD (không có điểm chèn nào).");
						return Result.Failed;
					}
					int rawPointCount = listBlockCad.Count;
					List<XYZ> calibratedCadPoints = listBlockCad.ConvertAll(point => new XYZ(point.X + deltaX, point.Y + deltaY, point.Z));
					List<XYZ> uniqueCadPoints = DeduplicatePointsByXY(calibratedCadPoints, 1.0 / 304.8);
					List<XYZ> existingPoints = GetExistingSymbolPoints(doc, symbol, level);
					List<XYZ> pointsToPlace = uniqueCadPoints
						.FindAll(point => !ContainsPointByXY(existingPoints, point, 1.0 / 304.8));
					int skippedExisting = uniqueCadPoints.Count - pointsToPlace.Count;
					string firstPoint = uniqueCadPoints.Count > 0
						? $"({uniqueCadPoints[0].X:0.###}, {uniqueCadPoints[0].Y:0.###}, {uniqueCadPoints[0].Z:0.###}) ft"
						: "None";
					CommandDiagnostics.Write("PlaceFamily", "scan", commandData, Result.Succeeded,
						$"Block={cadBlock}; Raw={rawPointCount}; Unique={uniqueCadPoints.Count}; SkippedExisting={skippedExisting}; ToPlace={pointsToPlace.Count}; FirstPoint={firstPoint}; Scan={PlaceFamilyUtils.LastBlockScanDiagnostics}");
					if (pointsToPlace.Count == 0)
					{
						TaskDialog.Show("Place Family", $"Đã tìm thấy {rawPointCount} block ({uniqueCadPoints.Count} vị trí duy nhất), nhưng tất cả vị trí đã có Family cùng type trên Level '{((Element)level).Name}'.");
						return Result.Succeeded;
					}
					using (ProgressBarView bv = new ProgressBarView("Đang đặt đối tượng...", pointsToPlace.Count))
					{
						bv.Show();
						List<ElementId> createdIds = new List<ElementId>();
						Transaction t = new Transaction(doc, "BIN_PlaceFamily");
						try
						{
							t.Start();
							if (!symbol.IsActive)
							{
								symbol.Activate();
							}
							foreach (XYZ cadLocation in pointsToPlace)
							{
								// CAD insertion points are project XY coordinates.  Z=0 in a reflected
								// ceiling-plan DWG is not a valid Revit elevation for an upper floor.
								XYZ revitLocation = new XYZ(cadLocation.X, cadLocation.Y, level.Elevation + elevation);
								FamilyInstance newInst = doc.Create.NewFamilyInstance(revitLocation, symbol, level, (StructuralType)0);
								createdIds.Add(((Element)newInst).Id);
								SetInstanceElevation(newInst, elevation);
								if (bv.Update())
								{
									break;
								}
							}
							t.Commit();
							if (createdIds.Count > 0)
							{
								// Keep the user's current view; selection alone does not navigate or zoom.
								uidoc.Selection.SetElementIds(createdIds);
							}
							string actualPlacement = DescribePlacedInstances(doc, createdIds);
							string idSummary = FormatIdSummary(createdIds);
							CommandDiagnostics.Write("PlaceFamily", "placement", commandData, Result.Succeeded,
								$"Block={cadBlock}; Raw={rawPointCount}; Unique={uniqueCadPoints.Count}; SkippedExisting={skippedExisting}; Created={createdIds.Count}; FirstPoint={firstPoint}; Ids={string.Join(",", createdIds)}; Actual={actualPlacement}; Scan={PlaceFamilyUtils.LastBlockScanDiagnostics}");
							string viewRangeWarning = doc.ActiveView.ViewType == ViewType.FloorPlan && elevation > 2000.0 / 304.8
								? $"\n\nLưu ý: đối tượng được đặt ở cao trên ({elevation * 304.8:0} mm), có thể không hiển thị trên Mặt bằng sàn (Floor Plan) do View Range. Vui lòng mở Reflected Ceiling Plan (Mặt bằng trần) hoặc 3D View để quan sát."
								: string.Empty;
							TaskDialog.Show("Hoàn tất", $"Đã đặt {createdIds.Count} đối tượng.\nID: {idSummary}\n\nView hiện tại được giữ nguyên. Danh sách ID đầy đủ đã ghi vào diagnostics.{viewRangeWarning}");
						}
						finally
						{
							((IDisposable)t)?.Dispose();
						}
					}
				}
			}
			catch (Exception ex2)
			{
				TaskDialog.Show("Lỗi Place Family", ex2.ToString());
				return Result.Cancelled;
			}
		}
		return Result.Succeeded;
	}

	private static List<XYZ> DeduplicatePointsByXY(List<XYZ> points, double tolerance)
	{
		List<XYZ> unique = new List<XYZ>();
		foreach (XYZ point in points)
		{
			if (!ContainsPointByXY(unique, point, tolerance)) unique.Add(point);
		}
		return unique;
	}

	private static void SetInstanceElevation(FamilyInstance instance, double elevation)
	{
		foreach (BuiltInParameter parameterId in new[]
		{
			BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM,
			BuiltInParameter.INSTANCE_ELEVATION_PARAM
		})
		{
			Parameter parameter = ((Element)instance).get_Parameter(parameterId);
			if (parameter != null && !((APIObject)parameter).IsReadOnly)
			{
				parameter.Set(elevation);
				return;
			}
		}
	}

	private static string DescribePlacedInstances(Document doc, List<ElementId> ids)
	{
		if (ids == null || ids.Count == 0) return "None";
		FamilyInstance instance = doc.GetElement(ids[0]) as FamilyInstance;
		if (instance == null) return "First created element is unavailable";
		LocationPoint location = ((Element)instance).Location as LocationPoint;
		Level actualLevel = doc.GetElement(instance.LevelId) as Level;
		string point = location == null ? "(no LocationPoint)" : $"({location.Point.X:0.###}, {location.Point.Y:0.###}, {location.Point.Z:0.###}) ft";
		string levelName = actualLevel == null ? "(no LevelId)" : ((Element)actualLevel).Name;
		return $"FirstId={ids[0].IntegerValue}; Level={levelName}; Point={point}";
	}

	private static string FormatIdSummary(List<ElementId> ids)
	{
		if (ids == null || ids.Count == 0) return "None";
		bool consecutive = true;
		for (int i = 1; i < ids.Count; i++)
		{
			if (ids[i].IntegerValue != ids[i - 1].IntegerValue + 1)
			{
				consecutive = false;
				break;
			}
		}
		return consecutive ? $"{ids[0].IntegerValue}–{ids[ids.Count - 1].IntegerValue}" : string.Join(", ", ids);
	}

	private static bool ContainsPointByXY(List<XYZ> points, XYZ target, double tolerance)
	{
		double toleranceSquared = tolerance * tolerance;
		foreach (XYZ point in points)
		{
			double dx = point.X - target.X;
			double dy = point.Y - target.Y;
			if (dx * dx + dy * dy <= toleranceSquared) return true;
		}
		return false;
	}

	private static List<XYZ> GetExistingSymbolPoints(Document doc, FamilySymbol symbol, Level level)
	{
		List<XYZ> points = new List<XYZ>();
		foreach (FamilyInstance familyInstance in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType())
		{
			if (familyInstance.Symbol == null || familyInstance.Symbol.Id.IntegerValue != symbol.Id.IntegerValue) continue;
			if (familyInstance.LevelId == null || familyInstance.LevelId.IntegerValue != level.Id.IntegerValue) continue;
			LocationPoint location = ((Element)familyInstance).Location as LocationPoint;
			if (location != null) points.Add(location.Point);
		}
		return points;
	}
}
