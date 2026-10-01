using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class GetIdCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		UIApplication uiApp = commandData.Application;
		UIDocument uidoc = uiApp.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
			if (selectedIds.Count == 1)
			{
				Element selectedElement = null;
				foreach (ElementId selectedId in selectedIds)
				{
					selectedElement = doc.GetElement(selectedId);
					break;
				}
				if (selectedElement != null)
				{
					ShowIdDialog(GetSelectedElementInspection(doc, uidoc.ActiveView, selectedElement), uiApp.MainWindowHandle);
					return Result.Succeeded;
				}
			}
			Reference refer = uidoc.Selection.PickObject((ObjectType)5, "Chọn đối tượng để lấy ID (hỗ trợ cả file link)");
			if (refer == null)
			{
				return Result.Cancelled;
			}
			string idInfo = GetElementIdInfo(doc, refer);
			ShowIdDialog(idInfo, uiApp.MainWindowHandle);
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

	private string GetElementIdInfo(Document doc, Reference refer)
	{
		StringBuilder sb = new StringBuilder();
		if (refer.LinkedElementId != ElementId.InvalidElementId)
		{
			Element element = doc.GetElement(refer.ElementId);
			RevitLinkInstance linkInstance = (RevitLinkInstance)(object)((element is RevitLinkInstance) ? element : null);
			if (linkInstance != null)
			{
				Document linkDoc = linkInstance.GetLinkDocument();
				ElementId linkedElemId = refer.LinkedElementId;
				sb.AppendLine("=== LINKED FILE ELEMENT ===");
				sb.AppendLine($"Link Instance ID : {refer.ElementId.GetIdInt()}");
				sb.AppendLine("Link File Name   : " + ((linkDoc != null) ? linkDoc.Title : ((Element)linkInstance).Name));
				sb.AppendLine($"Element ID       : {linkedElemId.GetIdInt()}");
				if (linkDoc != null)
				{
					Element linkedElem = linkDoc.GetElement(linkedElemId);
					if (linkedElem != null)
					{
						sb.AppendLine("Element Name     : " + linkedElem.Name);
						Category category = linkedElem.Category;
						sb.AppendLine("Category         : " + (((category != null) ? category.Name : null) ?? "N/A"));
						FamilyInstance fi = (FamilyInstance)(object)((linkedElem is FamilyInstance) ? linkedElem : null);
						if (fi != null)
						{
							FamilySymbol symbol = fi.Symbol;
							sb.AppendLine("Family           : " + (((symbol != null) ? ((ElementType)symbol).FamilyName : null) ?? "N/A"));
							FamilySymbol symbol2 = fi.Symbol;
							sb.AppendLine("Type             : " + (((symbol2 != null) ? ((Element)symbol2).Name : null) ?? "N/A"));
						}
						sb.AppendLine("UniqueId         : " + linkedElem.UniqueId);
					}
				}
			}
		}
		else
		{
			Element elem = doc.GetElement(refer.ElementId);
			if (elem != null)
			{
				sb.AppendLine("=== MAIN MODEL ELEMENT ===");
				sb.AppendLine($"Element ID       : {elem.Id.GetIdInt()}");
				sb.AppendLine("Element Name     : " + elem.Name);
				Category category2 = elem.Category;
				sb.AppendLine("Category         : " + (((category2 != null) ? category2.Name : null) ?? "N/A"));
				FamilyInstance fi2 = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
				if (fi2 != null)
				{
					FamilySymbol symbol3 = fi2.Symbol;
					sb.AppendLine("Family           : " + (((symbol3 != null) ? ((ElementType)symbol3).FamilyName : null) ?? "N/A"));
					FamilySymbol symbol4 = fi2.Symbol;
					sb.AppendLine("Type             : " + (((symbol4 != null) ? ((Element)symbol4).Name : null) ?? "N/A"));
				}
				sb.AppendLine("UniqueId         : " + elem.UniqueId);
			}
		}
		return sb.ToString().TrimEnd();
	}

	private string GetSelectedElementInspection(Document doc, View view, Element element)
	{
		StringBuilder sb = new StringBuilder(GetMainModelElementInfo(doc, element));
		sb.AppendLine();
		sb.AppendLine();
		sb.AppendLine("=== CURRENT VIEW VISIBILITY ===");
		sb.AppendLine("View              : " + ((view != null) ? view.Name : "N/A"));
		sb.AppendLine("View type         : " + ((view != null) ? view.ViewType.ToString() : "N/A"));
		if (view == null) return sb.ToString().TrimEnd();

		Category category = element.Category;
		bool individuallyHidden = element.IsHidden(view);
		bool categoryHidden = category != null && view.GetCategoryHidden(category.Id);
		BoundingBoxXYZ modelBox = element.get_BoundingBox(null);
		BoundingBoxXYZ viewBox = element.get_BoundingBox(view);
		sb.AppendLine("Hidden individually : " + individuallyHidden);
		sb.AppendLine("Category hidden     : " + categoryHidden);
		sb.AppendLine("Geometry in view    : " + ((viewBox != null) ? "YES" : "NO"));
		sb.AppendLine("Model bounding box  : " + FormatBoundingBox(modelBox));
		sb.AppendLine("View bounding box   : " + FormatBoundingBox(viewBox));
		try
		{
			Workset workset = doc.GetWorksetTable().GetWorkset(element.WorksetId);
			sb.AppendLine("Workset            : " + ((workset != null) ? workset.Name : "N/A"));
			sb.AppendLine("Workset in view     : " + view.GetWorksetVisibility(element.WorksetId));
		}
		catch (Exception ex)
		{
			sb.AppendLine("Workset check       : " + ex.Message);
		}
		sb.AppendLine("Created phase       : " + GetElementName(doc, element.CreatedPhaseId));
		sb.AppendLine("Demolished phase    : " + GetElementName(doc, element.DemolishedPhaseId));
		Parameter viewPhase = view.get_Parameter(BuiltInParameter.VIEW_PHASE);
		Parameter viewPhaseFilter = view.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER);
		sb.AppendLine("View phase          : " + GetElementName(doc, viewPhase?.AsElementId()));
		sb.AppendLine("View phase filter   : " + GetElementName(doc, viewPhaseFilter?.AsElementId()));
		return sb.ToString().TrimEnd();
	}

	private string GetMainModelElementInfo(Document doc, Element elem)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("=== MAIN MODEL ELEMENT ===");
		sb.AppendLine($"Element ID       : {elem.Id.GetIdInt()}");
		sb.AppendLine("Element Name     : " + elem.Name);
		Category category = elem.Category;
		sb.AppendLine("Category         : " + ((category != null ? category.Name : null) ?? "N/A"));
		FamilyInstance familyInstance = elem as FamilyInstance;
		if (familyInstance != null)
		{
			sb.AppendLine("Family           : " + ((familyInstance.Symbol != null ? familyInstance.Symbol.FamilyName : null) ?? "N/A"));
			sb.AppendLine("Type             : " + ((familyInstance.Symbol != null ? familyInstance.Symbol.Name : null) ?? "N/A"));
			Level level = doc.GetElement(familyInstance.LevelId) as Level;
			sb.AppendLine("Reference Level  : " + ((level != null) ? level.Name : "N/A"));
			LocationPoint location = elem.Location as LocationPoint;
			if (location != null) sb.AppendLine($"Location          : ({location.Point.X:0.###}, {location.Point.Y:0.###}, {location.Point.Z:0.###}) ft");
		}
		sb.AppendLine("UniqueId         : " + elem.UniqueId);
		return sb.ToString().TrimEnd();
	}

	private static string FormatBoundingBox(BoundingBoxXYZ boundingBox)
	{
		if (boundingBox == null) return "N/A";
		return $"Min=({boundingBox.Min.X:0.###}, {boundingBox.Min.Y:0.###}, {boundingBox.Min.Z:0.###}); Max=({boundingBox.Max.X:0.###}, {boundingBox.Max.Y:0.###}, {boundingBox.Max.Z:0.###}) ft";
	}

	private static string GetElementName(Document doc, ElementId id)
	{
		if (id == null || id == ElementId.InvalidElementId) return "None";
		Element element = doc.GetElement(id);
		return element == null ? id.GetIdInt().ToString() : element.Name;
	}

	private void ShowIdDialog(string idInfo, IntPtr ownerHandle)
	{
		GetIdWindow win = new GetIdWindow(idInfo);
		try
		{
			WindowInteropHelper helper = new WindowInteropHelper(win);
			helper.Owner = ownerHandle;
		}
		catch
		{
		}
		win.ShowDialog();
	}
}
