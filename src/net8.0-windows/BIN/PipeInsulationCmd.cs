using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class PipeInsulationCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0457: Unknown result type (might be due to invalid IL or missing references)
		//IL_0473: Unknown result type (might be due to invalid IL or missing references)
		//IL_047a: Unknown result type (might be due to invalid IL or missing references)
		//IL_047e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0449: Unknown result type (might be due to invalid IL or missing references)
		//IL_0450: Unknown result type (might be due to invalid IL or missing references)
		//IL_0259: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Expected O, but got Unknown
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Invalid comparison between Unknown and I4
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e6: Invalid comparison between Unknown and I4
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_023f: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			List<InsulationTypeItem> insulationTypes = GetPipeInsulationTypes(doc);
			if (insulationTypes.Count == 0)
			{
				TaskDialog.Show("BIM - Pipe Insulation", "Không tìm thấy Pipe Insulation Type nào trong dự án.\nVui lòng load Pipe Insulation Type trước khi sử dụng tool.");
				return Result.Cancelled;
			}
			PipeInsulationWindow ui = new PipeInsulationWindow(insulationTypes);
			if (ui.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			ElementId insulationTypeId = (ElementId)ui.SelectedInsulationType.Tag;
			bool removeExisting = ui.RemoveExisting;
			bool applyToAll = ui.ApplyToAll;
			List<PipeInsulationRule> systemRules = ui.Rules.ToList();
			List<Element> pipeElements;
			List<Element> fittingElements;
			if (applyToAll)
			{
				pipeElements = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id).OfCategory((BuiltInCategory)(-2008044)).WhereElementIsNotElementType().ToElements()
					.ToList();
				fittingElements = new FilteredElementCollector(doc, ((Element)doc.ActiveView).Id).OfCategory((BuiltInCategory)(-2008049)).WhereElementIsNotElementType().ToElements()
					.ToList();
			}
			else
			{
				ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
				pipeElements = new List<Element>();
				fittingElements = new List<Element>();
				foreach (ElementId id in selectedIds)
				{
					Element elem = doc.GetElement(id);
					if (elem != null && elem.Category != null)
					{
						BuiltInCategory cat = (BuiltInCategory)elem.Category.GetIdInt();
						if ((int)cat == -2008044)
						{
							pipeElements.Add(elem);
						}
						else if ((int)cat == -2008049)
						{
							fittingElements.Add(elem);
						}
					}
				}
				if (pipeElements.Count == 0 && fittingElements.Count == 0)
				{
					TaskDialog.Show("BIM - Pipe Insulation", "Không có Pipe hoặc Pipe Fitting nào được chọn.\nVui lòng chọn Pipe/Pipe Fitting trước khi chạy tool.");
					return Result.Cancelled;
				}
			}
			int insCount = 0;
			int skipCount = 0;
			int removeCount = 0;
			int noMatchCount = 0;
			Transaction trans = new Transaction(doc, "BIN_PipeInsulation");
			try
			{
				trans.Start();
				FailureHandlingOptions failOpts = trans.GetFailureHandlingOptions();
				failOpts.SetClearAfterRollback(true);
				trans.SetFailureHandlingOptions(failOpts);
				if (removeExisting)
				{
					removeCount = RemoveExistingInsulation(doc, pipeElements, fittingElements);
					doc.Regenerate();
				}
				foreach (Element pipe in pipeElements)
				{
					double pipeSize = GetPipeDiameter(pipe);
					double thickness = pipe is Pipe typedPipe ? PipeInsulationRules.GetThicknessMm(typedPipe, systemRules) / 304.8 : 0.0;
					if (thickness <= 0.0)
					{
						noMatchCount++;
						continue;
					}
					try
					{
						PipeInsulation.Create(doc, pipe.Id, insulationTypeId, thickness);
						insCount++;
					}
					catch
					{
						skipCount++;
					}
				}
				foreach (Element fitting in fittingElements)
				{
					double fittingSize = GetFittingSize(fitting);
					double thickness2 = PipeInsulationRules.GetFittingThicknessMm(fitting, fittingSize, systemRules) / 304.8;
					if (thickness2 <= 0.0)
					{
						noMatchCount++;
						continue;
					}
					try
					{
						PipeInsulation.Create(doc, fitting.Id, insulationTypeId, thickness2);
						insCount++;
					}
					catch
					{
						skipCount++;
					}
				}
				trans.Commit();
			}
			finally
			{
				((IDisposable)trans)?.Dispose();
			}
			string resultMsg = "Hoàn thành!\n" + $"• Đã bọc insulation: {insCount} phần tử\n" + $"• Bỏ qua (đã có insulation hoặc lỗi): {skipCount}\n" + $"• Không tìm thấy kích thước phù hợp: {noMatchCount}";
			if (removeExisting && removeCount > 0)
			{
				resultMsg += $"\n• Đã xóa insulation cũ: {removeCount}";
			}
			TaskDialog.Show("BIM - Pipe Insulation", resultMsg);
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			TaskDialog.Show("BIM TOOL - Error", ex.ToString());
			return Result.Failed;
		}
	}

	private List<InsulationTypeItem> GetPipeInsulationTypes(Document doc)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		List<InsulationTypeItem> result = new List<InsulationTypeItem>();
		FilteredElementCollector collector = new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2008122)).WhereElementIsElementType();
		foreach (Element elem in collector)
		{
			result.Add(new InsulationTypeItem
			{
				Name = elem.Name,
				Tag = elem.Id
			});
		}
		result.Sort((InsulationTypeItem a, InsulationTypeItem b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
		return result;
	}

	private double GetPipeDiameter(Element pipe)
	{
		Parameter pDia = pipe.get_Parameter((BuiltInParameter)(-1140225));
		if (pDia != null)
		{
			return pDia.AsDouble();
		}
		Parameter pNomDia = pipe.LookupParameter("Nominal Diameter");
		if (pNomDia != null)
		{
			return pNomDia.AsDouble();
		}
		Parameter pSize = pipe.LookupParameter("Size");
		if (pSize != null)
		{
			string sizeStr = pSize.AsString();
			if (!string.IsNullOrEmpty(sizeStr))
			{
				string numStr = Regex.Replace(sizeStr, "[^\\d.]", "");
				if (double.TryParse(numStr, out var sizeMM))
				{
					return sizeMM / 304.8;
				}
			}
		}
		return 0.0;
	}

	private double GetFittingSize(Element fitting)
	{
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Expected O, but got Unknown
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Invalid comparison between Unknown and I4
		FamilyInstance fi = (FamilyInstance)(object)((fitting is FamilyInstance) ? fitting : null);
		object obj;
		if (fi == null)
		{
			obj = null;
		}
		else
		{
			MEPModel mEPModel = fi.MEPModel;
			obj = ((mEPModel != null) ? mEPModel.ConnectorManager : null);
		}
		if (obj != null)
		{
			double maxSize = 0.0;
			foreach (Connector connector in fi.MEPModel.ConnectorManager.Connectors)
			{
				Connector conn = connector;
				if ((int)conn.Shape == 0)
				{
					double radius = conn.Radius;
					double diameter = radius * 2.0;
					if (diameter > maxSize)
					{
						maxSize = diameter;
					}
				}
			}
			if (maxSize > 0.0)
			{
				return maxSize;
			}
		}
		Parameter pNomDia = fitting.LookupParameter("Nominal Diameter");
		if (pNomDia != null)
		{
			return pNomDia.AsDouble();
		}
		Parameter pSize = fitting.LookupParameter("Size");
		if (pSize != null)
		{
			string sizeStr = pSize.AsString();
			if (!string.IsNullOrEmpty(sizeStr))
			{
				string numStr = Regex.Replace(sizeStr, "[^\\d.]", "");
				if (double.TryParse(numStr, out var sizeMM))
				{
					return sizeMM / 304.8;
				}
			}
		}
		return 0.0;
	}

	private double GetInsulationThickness(double pipeSizeFt, Dictionary<double, double> thicknessTable)
	{
		if (pipeSizeFt <= 0.0)
		{
			return 0.0;
		}
		double tolerance = 5.0 / 762.0;
		double bestMatch = -1.0;
		double bestDiff = double.MaxValue;
		foreach (KeyValuePair<double, double> kvp in thicknessTable)
		{
			double diff = Math.Abs(kvp.Key - pipeSizeFt);
			if (diff < bestDiff)
			{
				bestDiff = diff;
				bestMatch = kvp.Key;
			}
		}
		if (bestMatch >= 0.0 && bestDiff <= tolerance)
		{
			return thicknessTable[bestMatch];
		}
		return 0.0;
	}

	private int RemoveExistingInsulation(Document doc, List<Element> pipes, List<Element> fittings)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		int count = 0;
		IList<Element> allInsulations = new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2008122)).WhereElementIsNotElementType().ToElements();
		HashSet<ElementId> targetIds = new HashSet<ElementId>();
		foreach (Element p in pipes)
		{
			targetIds.Add(p.Id);
		}
		foreach (Element f in fittings)
		{
			targetIds.Add(f.Id);
		}
		List<ElementId> toDelete = new List<ElementId>();
		foreach (Element ins in allInsulations)
		{
			PipeInsulation pipeIns = (PipeInsulation)(object)((ins is PipeInsulation) ? ins : null);
			if (pipeIns != null && targetIds.Contains(((InsulationLiningBase)pipeIns).HostElementId))
			{
				toDelete.Add(ins.Id);
			}
		}
		foreach (ElementId id in toDelete)
		{
			try
			{
				doc.Delete(id);
				count++;
			}
			catch
			{
			}
		}
		return count;
	}
}
