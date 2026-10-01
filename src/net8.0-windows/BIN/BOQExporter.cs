using FillPattern = NPOI.SS.UserModel.FillPattern;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace BIN;

public static class BOQExporter
{
	private static bool MatchesAnyFilter(string value, List<BOQFilterItem> filters)
	{
		if (filters == null || !filters.Any())
		{
			return false;
		}
		return filters.Any((BOQFilterItem f) => f.FilterValues.Any((BOQFilterValue v) => MatchesFilter(value, f.FilterType, v.Value)));
	}

	private static bool MatchesAnyFilterOrEmpty(string value, List<BOQFilterItem> filters)
	{
		if (filters == null || !filters.Any())
		{
			return true;
		}
		if (filters.All((BOQFilterItem f) => !f.FilterValues.Any() || f.FilterValues.All((BOQFilterValue v) => string.IsNullOrWhiteSpace(v.Value))))
		{
			return true;
		}
		return filters.Any((BOQFilterItem f) => f.FilterValues.Any((BOQFilterValue v) => MatchesFilter(value, f.FilterType, v.Value)));
	}

	private static bool MatchesBuswayFilter(string value, List<BOQFilterItem> filters)
	{
		if (filters == null || !filters.Any())
		{
			return false;
		}
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}
		return filters.Any((BOQFilterItem f) => f.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value) && value.Trim().Equals(v.Value.Trim(), StringComparison.OrdinalIgnoreCase)));
	}

	private static List<string> SortLevels(List<string> levels)
	{
		return levels.OrderBy(delegate(string l)
		{
			if (string.IsNullOrEmpty(l) || l == "N/A")
			{
				return 999999;
			}
			string s = new string(l.Where(char.IsDigit).ToArray());
			int result;
			return int.TryParse(s, out result) ? ((l.IndexOf("B", StringComparison.OrdinalIgnoreCase) >= 0 || l.IndexOf("Basement", StringComparison.OrdinalIgnoreCase) >= 0) ? (-result) : result) : 99999;
		}).ThenBy((string l) => l).ToList();
	}

	public static void Export(Document doc, List<BOQCategoryItem> selectedCategories, string filePath, bool selectedOnly, bool inViewOnly, bool autoOpen, BOQSettings settings, string sysMode = "All", Action<int, int> progressCallback = null)
	{
		//IL_091b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0922: Expected O, but got Unknown
		//IL_0864: Unknown result type (might be due to invalid IL or missing references)
		//IL_086b: Expected O, but got Unknown
		//IL_0857: Unknown result type (might be due to invalid IL or missing references)
		//IL_085e: Expected O, but got Unknown
		//IL_077d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1602: Unknown result type (might be due to invalid IL or missing references)
		//IL_1609: Expected O, but got Unknown
		//IL_17c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_17ca: Expected O, but got Unknown
		//IL_14b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_14b8: Expected O, but got Unknown
		//IL_14c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_14ce: Invalid comparison between Unknown and I4
		//IL_16e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_16e9: Invalid comparison between Unknown and I4
		//IL_1714: Unknown result type (might be due to invalid IL or missing references)
		//IL_171a: Invalid comparison between Unknown and I4
		//IL_182d: Unknown result type (might be due to invalid IL or missing references)
		//IL_1833: Invalid comparison between Unknown and I4
		//IL_185c: Unknown result type (might be due to invalid IL or missing references)
		//IL_1862: Invalid comparison between Unknown and I4
		//IL_498e: Unknown result type (might be due to invalid IL or missing references)
		//IL_4994: Invalid comparison between Unknown and I4
		//IL_4901: Unknown result type (might be due to invalid IL or missing references)
		//IL_490a: Expected O, but got Unknown
		if (settings.HvacDuctExportMode == 1 && !settings.HvacDuctAggregate)
		{
			settings.HvacDuctExportMode = 2;
		}
		IWorkbook workbook = new XSSFWorkbook();
		ICellStyle titleStyle = CreateTitleStyle(workbook);
		ICellStyle headerStyle = CreateHeaderStyle(workbook);
		ICellStyle bodyStyle = CreateBodyStyle(workbook);
		ICellStyle numericStyle = CreateNumericStyle(workbook);
		string sheetName = "BOQ Report";
		int checkedCount = 0;
		if (settings.ExportDienCableTray)
		{
			checkedCount++;
		}
		if (settings.ExportDienBusway)
		{
			checkedCount++;
		}
		if (settings.ExportDienConduit)
		{
			checkedCount++;
		}
		if (settings.ExportNuocCap)
		{
			checkedCount++;
		}
		if (settings.ExportNuocThoat)
		{
			checkedCount++;
		}
		if (settings.ExportHvacDuct)
		{
			checkedCount++;
		}
		if (settings.ExportHvacDuctFitting)
		{
			checkedCount++;
		}
		if (settings.ExportHvacPipe)
		{
			checkedCount++;
		}
		if (settings.ExportHvacGas)
		{
			checkedCount++;
		}
		if (settings.ExportPcccPipe)
		{
			checkedCount++;
		}
		bool isBuswayExport = checkedCount == 1 && settings.ExportDienBusway;
		bool excludeServiceType = isBuswayExport;
		bool excludeCategoryAndType = isBuswayExport && settings.BWExportMode != 1;
		bool excludeHvacGasColumns = checkedCount == 1 && settings.ExportHvacGas;
		bool isHvacDuctExport = checkedCount == 1 && settings.ExportHvacDuct;
		bool isHvacDuctFittingExport = checkedCount == 1 && settings.ExportHvacDuctFitting;
		int colIdxCategory = -1;
		int colIdxType = -1;
		int colIdxNewType = -1;
		int colIdxServiceType = -1;
		int colIdxSize = -1;
		int colIdxAngle = -1;
		int colIdxDauMuc = -1;
		int colIdxUnit = -1;
		int startLevelColIdx = -1;
		if (checkedCount != 1)
		{
			sheetName = sysMode switch
			{
				"Dien" => "Electrical System", 
				"Nuoc" => "Plumbing System", 
				"Hvac" => "HVAC System", 
				"Pccc" => "Fire Protection System", 
				_ => "All Systems", 
			};
		}
		else if (settings.ExportDienCableTray)
		{
			sheetName = "Electrical System-Cable Tray";
		}
		else if (settings.ExportDienBusway)
		{
			sheetName = "Electrical System-Busway";
		}
		else if (settings.ExportDienConduit)
		{
			sheetName = "Electrical System-Conduit";
		}
		else if (settings.ExportNuocCap)
		{
			sheetName = "Plumbing System-Water Supply";
		}
		else if (settings.ExportNuocThoat)
		{
			sheetName = "Plumbing System-Drainage";
		}
		else if (settings.ExportHvacDuct)
		{
			sheetName = "HVAC System-HVAC Duct";
		}
		else if (settings.ExportHvacDuctFitting)
		{
			sheetName = "HVAC System-HVAC Duct Fitting";
		}
		else if (settings.ExportHvacPipe)
		{
			sheetName = "HVAC System-HVAC Pipe";
		}
		else if (settings.ExportHvacGas)
		{
			sheetName = "HVAC System-HVAC Gas";
		}
		else if (settings.ExportPcccPipe)
		{
			sheetName = "Fire Protection System-Pipe";
		}
		ISheet sheet = workbook.CreateSheet(sheetName);
		IRow titleRow = sheet.CreateRow(0);
		ICell titleCell = titleRow.CreateCell(0);
		titleCell.SetCellValue("BILL OF QUANTITIES - CENTRAL BIM TOOLS");
		titleCell.CellStyle = titleStyle;
		IRow infoRow = sheet.CreateRow(1);
		infoRow.CreateCell(0).SetCellValue("Project:");
		infoRow.CreateCell(1).SetCellValue(doc.Title);
		infoRow.CreateCell(4).SetCellValue("Date:");
		infoRow.CreateCell(5).SetCellValue(DateTime.Now.ToString("dd/MM/yyyy"));
		int totalCategories = selectedCategories.Count;
		int processedCategoriesCount = 0;
		List<BOQExportItem> allItems = new List<BOQExportItem>();
		foreach (BOQCategoryItem item in selectedCategories)
		{
			int catId = item.Category.Id.GetIdInt();
			bool isApplicable = true;
			if (!((catId != -2008130 && catId != -2008126) ? ((catId != -2008132 && catId != -2008128) ? ((catId == -2008000) ? (settings.ExportDienBusway || settings.ExportHvacDuct) : ((catId == -2008010) ? settings.ExportDienBusway : ((catId != -2008013 && catId != -2008016 && catId != -2001140) ? ((catId == -2008044 || catId == -2008049 || catId == -2008055) ? (settings.ExportNuocCap || settings.ExportNuocThoat || settings.ExportHvacPipe || settings.ExportHvacGas || settings.ExportPcccPipe) : ((catId == -2001160) ? (settings.ExportNuocCap || settings.ExportNuocThoat) : ((catId != -2008099) ? settings.ExportAll : settings.ExportPcccPipe))) : settings.ExportDienBusway))) : (settings.ExportDienConduit || settings.ExportHvacGas)) : settings.ExportDienCableTray))
			{
				processedCategoriesCount++;
				progressCallback?.Invoke(processedCategoriesCount, totalCategories);
				continue;
			}
			List<Document> list = new List<Document>();
			list.Add(doc);
			List<Document> docsToScan = list;
			if (settings.IncludeFileLink)
			{
				List<RevitLinkInstance> linkInstances = ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance))).Cast<RevitLinkInstance>().ToList();
				foreach (RevitLinkInstance link in linkInstances)
				{
					Document linkedDoc = link.GetLinkDocument();
					if (linkedDoc != null)
					{
						docsToScan.Add(linkedDoc);
					}
				}
			}
			List<Element> allDocElements = new List<Element>();
			foreach (Document scanDoc in docsToScan)
			{
				FilteredElementCollector collector = ((!inViewOnly || scanDoc != doc || doc.ActiveView == null) ? new FilteredElementCollector(scanDoc) : new FilteredElementCollector(scanDoc, ((Element)doc.ActiveView).Id));
				try
				{
					Category catInDoc = ((IEnumerable)scanDoc.Settings.Categories).Cast<Category>().FirstOrDefault((Category c) => c.Id.GetIdInt() == catId);
					if (catInDoc != null)
					{
						collector.OfCategoryId(catInDoc.Id).WhereElementIsNotElementType();
						allDocElements.AddRange(collector.ToElements());
					}
				}
				catch
				{
				}
			}
			if (selectedOnly)
			{
				UIDocument uiDoc = new UIDocument(doc);
				ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
				if (selectedIds.Count > 0)
				{
					allDocElements = allDocElements.Where((Element e) => selectedIds.Contains(e.Id)).ToList();
				}
			}
			List<Element> elements = allDocElements.Where(delegate(Element e)
			{
				Parameter obj2 = e.get_Parameter((BuiltInParameter)(-1002050));
				string typeName = ((obj2 != null) ? obj2.AsValueString() : null) ?? e.Name;
				string systemTypeName = GetSystemTypeName(e, doc);
				if (catId == -2008130 || catId == -2008126)
				{
					if (!settings.ExportDienCableTray)
					{
						return false;
					}
					List<BOQFilterItem> cTSystems = settings.CTSystems;
					string mVal = typeName;
					if (settings.CTCutMode == "ServiceType")
					{
						Parameter val = e.get_Parameter((BuiltInParameter)(-1140128));
						mVal = ((val != null) ? val.AsString() : null) ?? "";
					}
					if (cTSystems == null || !cTSystems.Any((BOQFilterItem f) => f.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()))))
					{
						return true;
					}
					return cTSystems.Any((BOQFilterItem f) => f.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(mVal, f.FilterType, v.Value)));
				}
				if (catId == -2008132)
				{
					if (typeName.StartsWith("ø", StringComparison.OrdinalIgnoreCase))
					{
						if (!settings.ExportHvacGas)
						{
							return false;
						}
						if (settings.HVACGasGroups == null || !settings.HVACGasGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()))))
						{
							return true;
						}
						return settings.HVACGasGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(typeName, g.FilterType, v.Value?.Trim())));
					}
					if (!settings.ExportDienConduit)
					{
						return false;
					}
					List<BOQFilterItem> condSystems = settings.CondSystems;
					string mVal2 = typeName;
					if (settings.CondCutMode == "ServiceType")
					{
						mVal2 = GetServiceType(e);
					}
					if (condSystems == null || !condSystems.Any((BOQFilterItem f) => f.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()))))
					{
						return true;
					}
					return condSystems.Any((BOQFilterItem f) => f.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(mVal2, f.FilterType, v.Value)));
				}
				if (catId == -2008128)
				{
					if (typeName.StartsWith("ø", StringComparison.OrdinalIgnoreCase))
					{
						return settings.ExportHvacGas;
					}
					return settings.ExportDienConduit;
				}
				if (catId == -2008000 || catId == -2008010 || catId == -2008013 || catId == -2008016 || catId == -2001140)
				{
					if ((settings.BWSystems != null && MatchesBuswayFilter(systemTypeName, settings.BWSystems)) || systemTypeName.IndexOf("BW", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return settings.ExportDienBusway;
					}
					if (catId == -2008010)
					{
						if (!settings.ExportHvacDuct)
						{
							return false;
						}
						if (settings.HVACDuctFittingGroups == null || !settings.HVACDuctFittingGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()))))
						{
							return true;
						}
						return settings.HVACDuctFittingGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(systemTypeName, g.FilterType, v.Value?.Trim())));
					}
					if (!settings.ExportHvacDuct)
					{
						return false;
					}
					if (settings.HVACDuctGroups == null || !settings.HVACDuctGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()))))
					{
						return true;
					}
					return settings.HVACDuctGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(systemTypeName, g.FilterType, v.Value?.Trim())));
				}
				if (catId == -2008044 || catId == -2008049 || catId == -2008055)
				{
					bool flag = settings.CapNuocGroups != null && settings.CapNuocGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(systemTypeName, g.FilterType, v.Value?.Trim())));
					bool flag2 = settings.ThoatNuocGroups != null && settings.ThoatNuocGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(systemTypeName, g.FilterType, v.Value?.Trim())));
					bool flag3 = settings.HVACPipeGroups != null && settings.HVACPipeGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(systemTypeName, g.FilterType, v.Value?.Trim())));
					bool flag4 = settings.PCCCGroups != null && settings.PCCCGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()) && MatchesFilter(systemTypeName, g.FilterType, v.Value?.Trim())));
					if (flag)
					{
						return settings.ExportNuocCap;
					}
					if (flag2)
					{
						return settings.ExportNuocThoat;
					}
					if (flag3)
					{
						return settings.ExportHvacPipe;
					}
					if (flag4)
					{
						return settings.ExportPcccPipe;
					}
					bool flag5 = settings.CapNuocGroups != null && settings.CapNuocGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim())));
					bool flag6 = settings.ThoatNuocGroups != null && settings.ThoatNuocGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim())));
					bool flag7 = settings.HVACPipeGroups != null && settings.HVACPipeGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim())));
					bool flag8 = settings.PCCCGroups != null && settings.PCCCGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim())));
					if (flag5 || flag6 || flag7 || flag8)
					{
						return false;
					}
					bool flag9 = settings.CapThoatNuocSystems != null && MatchesAnyFilterOrEmpty(systemTypeName, settings.CapThoatNuocSystems);
					bool flag10 = settings.HVACPipeSystems != null && MatchesAnyFilterOrEmpty(systemTypeName, settings.HVACPipeSystems);
					if (flag9 && (settings.ExportNuocCap || settings.ExportNuocThoat))
					{
						return true;
					}
					if (flag10 && settings.ExportHvacPipe)
					{
						return true;
					}
					return false;
				}
				if (catId == -2001160)
				{
					if (!settings.ExportNuocCap && !settings.ExportNuocThoat)
					{
						return false;
					}
					List<BOQFilterItem> capThoatNuocSystems = settings.CapThoatNuocSystems;
					if (capThoatNuocSystems == null || !capThoatNuocSystems.Any())
					{
						return true;
					}
					if (capThoatNuocSystems.All((BOQFilterItem f) => !f.FilterValues.Any() || f.FilterValues.All((BOQFilterValue v) => string.IsNullOrWhiteSpace(v.Value))))
					{
						return true;
					}
					return capThoatNuocSystems.Any((BOQFilterItem f) => f.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName, f.FilterType, v.Value)));
				}
				return (catId == -2008099) ? settings.ExportPcccPipe : settings.ExportAll;
			}).ToList();
			foreach (Element e2 in elements)
			{
				Parameter obj3 = e2.get_Parameter((BuiltInParameter)(-1002050));
				string typeName2 = ((obj3 != null) ? obj3.AsValueString() : null) ?? e2.Name;
				string systemTypeName2 = GetSystemTypeName(e2, doc);
				if (catId == -2008049 || catId == -2008055)
				{
					string fNameCheck = GetFamilyName(e2) ?? "";
					if (typeName2.IndexOf("PPR", StringComparison.OrdinalIgnoreCase) >= 0 || typeName2.IndexOf("uPVC", StringComparison.OrdinalIgnoreCase) >= 0 || fNameCheck.IndexOf("PPR", StringComparison.OrdinalIgnoreCase) >= 0 || fNameCheck.IndexOf("uPVC", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						string combined = typeName2 + " " + fNameCheck;
						if (combined.IndexOf("ren", StringComparison.OrdinalIgnoreCase) >= 0 || combined.IndexOf("VAN", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							continue;
						}
					}
				}
				string groupName = item.Name;
				bool isElementBusway = false;
				bool isBuswayPcs = false;
				if (catId == -2008000 || catId == -2008010 || catId == -2008013 || catId == -2008016 || catId == -2001140)
				{
					isElementBusway = (settings.BWSystems != null && MatchesBuswayFilter(systemTypeName2, settings.BWSystems)) || systemTypeName2.IndexOf("BW", StringComparison.OrdinalIgnoreCase) >= 0;
					if (isElementBusway)
					{
						isBuswayPcs = settings.BWExportMode == 1;
					}
				}
				List<BOQGroupItem> activeGroups = null;
				string mVal3 = typeName2;
				if (catId == -2008130 || catId == -2008126)
				{
					activeGroups = settings.CTGroups;
					if (settings.CTCutMode == "ServiceType")
					{
						Parameter serviceTypeParam = e2.get_Parameter((BuiltInParameter)(-1140128));
						mVal3 = ((serviceTypeParam != null) ? serviceTypeParam.AsString() : null) ?? "";
					}
					else
					{
						mVal3 = typeName2;
					}
				}
				else if (catId == -2008132 || catId == -2008128)
				{
					if (typeName2.StartsWith("ø", StringComparison.OrdinalIgnoreCase))
					{
						activeGroups = settings.HVACGasGroups;
						mVal3 = typeName2;
					}
					else
					{
						activeGroups = settings.CondGroups;
						if (settings.CondCutMode == "ServiceType")
						{
							mVal3 = GetServiceType(e2);
						}
						else
						{
							mVal3 = typeName2;
						}
					}
				}
				else if (catId == -2008000 || catId == -2008010 || catId == -2008016 || catId == -2008013)
				{
					activeGroups = (isElementBusway ? settings.BWGroups : ((catId != -2008010) ? settings.HVACDuctGroups : settings.HVACDuctFittingGroups));
					mVal3 = systemTypeName2;
				}
				else if (catId == -2008044 || catId == -2008049 || catId == -2008055)
				{
					bool matchesCapNuoc = settings.CapNuocGroups != null && settings.CapNuocGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName2, g.FilterType, v.Value?.Trim())));
					bool matchesThoatNuoc = settings.ThoatNuocGroups != null && settings.ThoatNuocGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName2, g.FilterType, v.Value?.Trim())));
					bool matchesHvacPipe = settings.HVACPipeGroups != null && settings.HVACPipeGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName2, g.FilterType, v.Value?.Trim())));
					bool matchesPccc = settings.PCCCGroups != null && settings.PCCCGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName2, g.FilterType, v.Value?.Trim())));
					activeGroups = (matchesCapNuoc ? settings.CapNuocGroups : (matchesThoatNuoc ? settings.ThoatNuocGroups : (matchesHvacPipe ? settings.HVACPipeGroups : ((!matchesPccc) ? settings.PipeGroups : settings.PCCCGroups))));
					mVal3 = systemTypeName2;
				}
				if (activeGroups != null)
				{
					List<BOQGroupItem> orderedGroups = activeGroups.OrderByDescending((BOQGroupItem g) => g.FilterValues.Any() ? g.FilterValues.Max((BOQFilterValue v) => (v.Value ?? "").Trim().Length) : 0).ToList();
					BOQGroupItem match = orderedGroups.FirstOrDefault((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(mVal3, g.FilterType, v.Value?.Trim())));
					if (match != null)
					{
						groupName = match.GroupName;
					}
					else if (activeGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()))))
					{
						continue;
					}
				}
				if (isElementBusway)
				{
					groupName = systemTypeName2;
				}
				int subSystemOrder = 99;
				if (catId == -2008130 || catId == -2008126)
				{
					subSystemOrder = 1;
				}
				else if (isElementBusway)
				{
					subSystemOrder = 2;
				}
				else if (catId == -2008132 || catId == -2008128)
				{
					subSystemOrder = ((!typeName2.StartsWith("ø", StringComparison.OrdinalIgnoreCase)) ? 3 : 8);
				}
				else if (catId == -2001160)
				{
					subSystemOrder = 4;
				}
				else if (catId == -2008000 || catId == -2008010 || catId == -2008013 || catId == -2008016 || catId == -2001140)
				{
					subSystemOrder = ((catId != -2008010) ? 5 : 6);
				}
				else if (catId == -2008044 || catId == -2008049 || catId == -2008055)
				{
					bool matchesPlumbing = settings.CapThoatNuocSystems != null && MatchesAnyFilter(systemTypeName2, settings.CapThoatNuocSystems);
					bool matchesCapNuoc2 = settings.CapNuocGroups != null && settings.CapNuocGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName2, g.FilterType, v.Value?.Trim())));
					bool matchesThoatNuoc2 = settings.ThoatNuocGroups != null && settings.ThoatNuocGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName2, g.FilterType, v.Value?.Trim())));
					bool matchesHvacPipe2 = settings.HVACPipeGroups != null && settings.HVACPipeGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName2, g.FilterType, v.Value?.Trim())));
					bool matchesPccc2 = settings.PCCCGroups != null && settings.PCCCGroups.Any((BOQGroupItem g) => g.FilterValues.Any((BOQFilterValue v) => MatchesFilter(systemTypeName2, g.FilterType, v.Value?.Trim())));
					subSystemOrder = ((matchesPlumbing || matchesCapNuoc2 || matchesThoatNuoc2) ? 4 : (matchesHvacPipe2 ? 7 : (matchesPccc2 ? 9 : ((!settings.ExportHvacPipe) ? 4 : 7))));
				}
				else if (catId == -2008099)
				{
					subSystemOrder = 9;
				}
				string serviceType = (isElementBusway ? "" : GetServiceType(e2));
				if (subSystemOrder == 9 && !string.IsNullOrEmpty(groupName))
				{
					serviceType = groupName;
				}
				string angleValue = "";
				bool isElbow = false;
				if (item.Category.Id.GetIdInt() == -2008049 || item.Category.Id.GetIdInt() == -2008128)
				{
					bool isTee = false;
					FamilyInstance fi = (FamilyInstance)(object)((e2 is FamilyInstance) ? e2 : null);
					if (fi != null)
					{
						FamilySymbol symbol = fi.Symbol;
						if (((symbol != null) ? symbol.Family : null) != null)
						{
							Parameter partTypeParam = ((Element)fi.Symbol.Family).get_Parameter((BuiltInParameter)(-1114206));
							if (partTypeParam != null)
							{
								int partTypeVal = partTypeParam.AsInteger();
								isElbow = partTypeVal == 5;
								isTee = partTypeVal == 6;
							}
						}
					}
					if (!isElbow && !isTee)
					{
						string fName = GetFamilyName(e2) ?? "";
						string tName = typeName2 ?? "";
						string combined2 = (tName + " " + fName).ToLower();
						isElbow = combined2.Contains("elbow") || combined2.Contains("cút") || combined2.Contains("lơi") || combined2.Contains(" co ");
						isTee = combined2.Contains("tee") || combined2.Contains(" tê") || combined2.Contains("chữ t");
					}
					if (isElbow || isTee)
					{
						Parameter angleParam = null;
						foreach (Parameter parameter in e2.Parameters)
						{
							Parameter p = parameter;
							if (p != null && p.HasValue && (int)p.StorageType == 2)
							{
								Definition definition = p.Definition;
								string pName = ((definition != null) ? definition.Name : null) ?? "";
								if (pName.IndexOf("Angle", StringComparison.OrdinalIgnoreCase) >= 0)
								{
									angleParam = p;
									break;
								}
							}
						}
						if (angleParam != null && angleParam.HasValue)
						{
							double deg = angleParam.AsDouble() * 180.0 / Math.PI;
							deg = ((!(deg >= 80.0) || !(deg <= 100.0)) ? 45.0 : 90.0);
							angleValue = ((int)deg).ToString();
						}
						isElbow = isElbow || isTee;
					}
				}
				else if (isElementBusway && catId == -2008010)
				{
					foreach (Parameter parameter2 in e2.Parameters)
					{
						Parameter p2 = parameter2;
						if (p2 == null || !p2.HasValue)
						{
							continue;
						}
						Definition definition2 = p2.Definition;
						string pName2 = ((definition2 != null) ? definition2.Name : null) ?? "";
						if (pName2.IndexOf("Angle", StringComparison.OrdinalIgnoreCase) < 0)
						{
							continue;
						}
						string vStr = p2.AsValueString();
						if (!string.IsNullOrEmpty(vStr))
						{
							string clean = new string(vStr.Where((char c) => char.IsDigit(c) || c == '.').ToArray());
							if (double.TryParse(clean, out var parsedVal))
							{
								angleValue = ((int)Math.Round(parsedVal)).ToString();
								isElbow = true;
								break;
							}
						}
						double rawVal = 0.0;
						if ((int)p2.StorageType == 2)
						{
							rawVal = p2.AsDouble() * 180.0 / Math.PI;
						}
						else if ((int)p2.StorageType == 1)
						{
							rawVal = p2.AsInteger();
						}
						angleValue = ((int)Math.Round(rawVal)).ToString();
						isElbow = true;
						break;
					}
				}
				else if (catId == -2008126 && (typeName2 ?? "").IndexOf("Co thang cáp", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					foreach (Parameter parameter3 in e2.Parameters)
					{
						Parameter p3 = parameter3;
						if (p3 == null || !p3.HasValue)
						{
							continue;
						}
						Definition definition3 = p3.Definition;
						string pName3 = ((definition3 != null) ? definition3.Name : null) ?? "";
						if (pName3.IndexOf("Angle", StringComparison.OrdinalIgnoreCase) < 0)
						{
							continue;
						}
						double deg2 = 0.0;
						if ((int)p3.StorageType == 2)
						{
							deg2 = p3.AsDouble() * 180.0 / Math.PI;
						}
						else if ((int)p3.StorageType == 1)
						{
							deg2 = p3.AsInteger();
						}
						else
						{
							string vStr2 = p3.AsValueString() ?? "";
							string clean2 = new string(vStr2.Where((char c) => char.IsDigit(c) || c == '.').ToArray());
							double.TryParse(clean2, out deg2);
						}
						if (!(deg2 > 0.0))
						{
							continue;
						}
						angleValue = ((int)Math.Round(deg2)).ToString();
						isElbow = true;
						break;
					}
				}
				if (subSystemOrder == 8)
				{
					List<string> parsedSizes = ExtractGasSizes(typeName2);
					if (parsedSizes.Any())
					{
						foreach (string s in parsedSizes)
						{
							if (settings.GasSizes == null || settings.GasSizes.Any((BOQGasSizeItem gs) => gs.Name == s && gs.IsChecked))
							{
								allItems.Add(new BOQExportItem
								{
									Element = e2,
									Category = item.Category,
									GroupName = groupName,
									FamilyName = GetFamilyName(e2),
									TypeName = "ø" + s,
									Size = s,
									Angle = angleValue,
									IsElbow = isElbow,
									ServiceType = serviceType,
									Level = GetLevel(e2),
									IsBusway = isElementBusway,
									IsBuswayPcs = isBuswayPcs,
									SubSystemOrder = subSystemOrder
								});
							}
						}
						continue;
					}
				}
				string finalGroupName = groupName;
				string elementSize = GetPlumbingPipeSize(e2, typeName2, subSystemOrder, catId);
				double thicknessVal = 0.0;
				if ((subSystemOrder == 5 || subSystemOrder == 6) && !isElementBusway && (catId == -2008000 || catId == -2008010))
				{
					thicknessVal = GetDuctThickness(e2, elementSize, settings);
				}
				allItems.Add(new BOQExportItem
				{
					Element = e2,
					Category = item.Category,
					GroupName = finalGroupName,
					FamilyName = GetFamilyName(e2),
					TypeName = typeName2,
					Size = elementSize,
					Angle = angleValue,
					IsElbow = isElbow,
					ServiceType = serviceType,
					Level = GetLevel(e2),
					IsBusway = isElementBusway,
					IsBuswayPcs = isBuswayPcs,
					SubSystemOrder = subSystemOrder,
					DuctThickness = thicknessVal,
					EiType = ((catId == -2008000) ? GetDuctEiType(e2) : "")
				});
			}
			processedCategoriesCount++;
			progressCallback?.Invoke(processedCategoriesCount, totalCategories);
		}
		bool hideSystemTypeColumn = false;
		HashSet<string> hvacGroupsWithMultipleSystems = new HashSet<string>();
		if (isHvacDuctExport || isHvacDuctFittingExport)
		{
			hvacGroupsWithMultipleSystems = (from x in allItems
				where x.SubSystemOrder == 5 || x.SubSystemOrder == 6
				group x by x.GroupName into g
				where g.Select((BOQExportItem x) => x.ServiceType).Distinct().Count((string st) => !string.IsNullOrEmpty(st)) >= 2
				select g.Key).ToHashSet();
			if (hvacGroupsWithMultipleSystems.Any())
			{
				hideSystemTypeColumn = true;
			}
		}
		HashSet<string> conduitGroupsWithMultipleSystems = (from x in allItems
			where x.SubSystemOrder == 3
			group x by x.GroupName into g
			where g.Select((BOQExportItem x) => x.ServiceType).Distinct().Count((string st) => !string.IsNullOrEmpty(st)) >= 2
			select g.Key).ToHashSet();
		bool isConduitExport = checkedCount == 1 && settings.ExportDienConduit;
		int nextCol = 1;
		if (!excludeCategoryAndType)
		{
			colIdxCategory = nextCol++;
			colIdxType = nextCol++;
		}
		if (!excludeServiceType && !excludeHvacGasColumns && !hideSystemTypeColumn)
		{
			colIdxServiceType = nextCol++;
		}
		if (!excludeHvacGasColumns)
		{
			if (!isBuswayExport && (!isHvacDuctExport || settings.HvacDuctExportMode != 1 || allItems.Any((BOQExportItem x) => x.Category.Id.GetIdInt() != -2008000)))
			{
				colIdxSize = nextCol++;
			}
			if ((!isHvacDuctExport || settings.HvacDuctExportMode != 3) && (!isBuswayExport || settings.BWExportMode == 1))
			{
				colIdxAngle = nextCol++;
			}
		}
		colIdxDauMuc = nextCol++;
		colIdxUnit = nextCol++;
		startLevelColIdx = nextCol;
		List<string> uniqueLevels = allItems.Select((BOQExportItem x) => x.Level).Distinct().ToList();
		List<string> levels = SortLevels(uniqueLevels);
		IRow headerRow3 = sheet.CreateRow(3);
		IRow headerRow4 = sheet.CreateRow(4);
		headerRow3.CreateCell(0).SetCellValue("STT");
		if (!excludeCategoryAndType)
		{
			headerRow3.CreateCell(colIdxCategory).SetCellValue("Category");
			headerRow3.CreateCell(colIdxType).SetCellValue("Type");
		}
		if (colIdxServiceType != -1)
		{
			string headerName = (isConduitExport ? "Service Type" : "System Type");
			headerRow3.CreateCell(colIdxServiceType).SetCellValue(headerName);
		}
		if (colIdxSize != -1)
		{
			headerRow3.CreateCell(colIdxSize).SetCellValue("Size");
		}
		if (colIdxAngle != -1)
		{
			if (isHvacDuctExport)
			{
				headerRow3.CreateCell(colIdxAngle).SetCellValue("Duct Thickness");
			}
			else
			{
				headerRow3.CreateCell(colIdxAngle).SetCellValue("Angle");
			}
		}
		headerRow3.CreateCell(colIdxDauMuc).SetCellValue("Item Code");
		headerRow3.CreateCell(colIdxUnit).SetCellValue("Unit");
		headerRow4.CreateCell(0);
		if (!excludeCategoryAndType)
		{
			headerRow4.CreateCell(colIdxCategory);
			headerRow4.CreateCell(colIdxType);
		}
		if (colIdxServiceType != -1)
		{
			headerRow4.CreateCell(colIdxServiceType);
		}
		if (colIdxSize != -1)
		{
			headerRow4.CreateCell(colIdxSize);
		}
		if (colIdxAngle != -1)
		{
			headerRow4.CreateCell(colIdxAngle);
		}
		headerRow4.CreateCell(colIdxDauMuc);
		headerRow4.CreateCell(colIdxUnit);
		int headerColumnsCount = startLevelColIdx;
		for (int i = 0; i < headerColumnsCount; i++)
		{
			headerRow3.GetCell(i).CellStyle = headerStyle;
			headerRow4.GetCell(i).CellStyle = headerStyle;
			AddMergedRegionSafe(sheet, 3, 4, i, i);
		}
		string towerName = (string.IsNullOrEmpty(doc.Title) ? "PROJECT BOQ" : doc.Title);
		if (towerName.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase))
		{
			towerName = towerName.Substring(0, towerName.Length - 4);
		}
		towerName = towerName.ToUpper();
		if (levels.Any())
		{
			ICell towerCell = headerRow3.CreateCell(startLevelColIdx);
			towerCell.SetCellValue(towerName);
			towerCell.CellStyle = headerStyle;
			for (int j = 1; j < levels.Count; j++)
			{
				ICell cell = headerRow3.CreateCell(startLevelColIdx + j);
				cell.CellStyle = headerStyle;
			}
			AddMergedRegionSafe(sheet, 3, 3, startLevelColIdx, startLevelColIdx + levels.Count - 1);
			for (int k = 0; k < levels.Count; k++)
			{
				ICell cell2 = headerRow4.CreateCell(startLevelColIdx + k);
				cell2.SetCellValue(levels[k]);
				cell2.CellStyle = headerStyle;
			}
		}
		int totalColIndex = startLevelColIdx + levels.Count;
		headerRow3.CreateCell(totalColIndex).SetCellValue("Total Qty");
		headerRow4.CreateCell(totalColIndex);
		headerRow3.GetCell(totalColIndex).CellStyle = headerStyle;
		headerRow4.GetCell(totalColIndex).CellStyle = headerStyle;
		AddMergedRegionSafe(sheet, 3, 4, totalColIndex, totalColIndex);
		AddMergedRegionSafe(sheet, 0, 0, 0, totalColIndex);
		List<string> definedGroupNames = new List<string>();
		if (settings.CTGroups != null)
		{
			definedGroupNames.AddRange(settings.CTGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.CondGroups != null)
		{
			definedGroupNames.AddRange(settings.CondGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.BWGroups != null)
		{
			definedGroupNames.AddRange(settings.BWGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.PipeGroups != null)
		{
			definedGroupNames.AddRange(settings.PipeGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.CapThoatNuocGroups != null)
		{
			definedGroupNames.AddRange(settings.CapThoatNuocGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.CapNuocGroups != null)
		{
			definedGroupNames.AddRange(settings.CapNuocGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.ThoatNuocGroups != null)
		{
			definedGroupNames.AddRange(settings.ThoatNuocGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.HVACDuctGroups != null)
		{
			definedGroupNames.AddRange(settings.HVACDuctGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.HVACDuctFittingGroups != null)
		{
			definedGroupNames.AddRange(settings.HVACDuctFittingGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.HVACPipeGroups != null)
		{
			definedGroupNames.AddRange(settings.HVACPipeGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.HVACGasGroups != null)
		{
			definedGroupNames.AddRange(settings.HVACGasGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		if (settings.PCCCGroups != null)
		{
			definedGroupNames.AddRange(settings.PCCCGroups.Select((BOQGroupItem g) => g.GroupName));
		}
		definedGroupNames = definedGroupNames.Distinct().ToList();
		Func<string, int> getGroupOrder = delegate(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return int.MaxValue;
			}
			string item2 = name;
			int num = name.IndexOf(" (t=");
			if (num > 0)
			{
				item2 = name.Substring(0, num);
			}
			int num2 = definedGroupNames.IndexOf(item2);
			return (num2 >= 0) ? num2 : int.MaxValue;
		};
		var groupedItems = (from g in allItems.GroupBy(delegate(BOQExportItem x)
			{
				bool flag11 = (x.SubSystemOrder == 5 || x.SubSystemOrder == 6) && hvacGroupsWithMultipleSystems.Contains(x.GroupName);
				bool flag12 = x.SubSystemOrder == 3 && conduitGroupsWithMultipleSystems.Contains(x.GroupName);
				string text = ((flag11 || flag12) ? "" : x.ServiceType);
				if (x.SubSystemOrder == 5 && settings.HvacDuctExportMode == 1 && x.Category.Id.GetIdInt() == -2008000)
				{
					string typeName3 = "Ống gió thẳng";
					return new
					{
						SubSystemOrder = x.SubSystemOrder,
						GroupName = x.GroupName,
						TypeName = typeName3,
						Size = "",
						Angle = "",
						IsElbow = false,
						CategoryId = 0,
						DuctThickness = x.DuctThickness,
						ServiceType = "",
						IsBusway = x.IsBusway,
						IsBuswayPcs = false,
						EiType = ""
					};
				}
				string text2 = x.Size;
				string text3 = x.TypeName;
				string text4 = x.Angle;
				if (x.IsBusway)
				{
					if (x.Category.Id.GetIdInt() == -2008010)
					{
						text2 = "";
						if (!x.IsBuswayPcs)
						{
							string text5 = x.TypeName ?? "";
							string text6 = "";
							int num3 = text5.IndexOf("Busway", StringComparison.OrdinalIgnoreCase);
							if (num3 >= 0)
							{
								text6 = text5.Substring(num3 + "Busway".Length).Trim();
							}
							else
							{
								int num4 = text5.LastIndexOf(' ');
								text6 = ((num4 >= 0) ? text5.Substring(num4 + 1).Trim() : text5.Trim());
							}
							text3 = "Fitting busway";
							text4 = text6;
						}
					}
					else if (x.Category.Id.GetIdInt() == -2008000 && !x.IsBuswayPcs)
					{
						text2 = "";
						string text7 = x.TypeName ?? "";
						string text8 = "";
						int num5 = text7.IndexOf("Busway", StringComparison.OrdinalIgnoreCase);
						if (num5 >= 0)
						{
							text8 = text7.Substring(num5 + "Busway".Length).Trim();
						}
						else
						{
							int num6 = text7.LastIndexOf(' ');
							text8 = ((num6 >= 0) ? text7.Substring(num6 + 1).Trim() : text7.Trim());
						}
						text3 = "Busway";
						text4 = text8;
					}
				}
				string text9 = x.GroupName;
				bool flag13 = x.IsElbow;
				if (x.IsBusway)
				{
					flag13 = false;
				}
				if (x.IsBusway && !x.IsBuswayPcs)
				{
					text9 = "Busway " + text4;
				}
				string groupName2 = text9;
				string text10 = text3;
				string text11 = text2;
				string angle = text4;
				bool isElbow2 = flag13;
				int categoryId = x.Category.Id.GetIdInt();
				string serviceType2 = text;
				string eiType = x.EiType ?? "";
				if (x.SubSystemOrder == 7)
				{
					text10 = ComputeHvacPipeItemCode(text3, text4, text2);
					text11 = "";
					angle = "";
					isElbow2 = false;
					categoryId = 0;
					serviceType2 = "";
				}
				else if (x.SubSystemOrder == 8)
				{
					groupName2 = "";
					text11 = "";
					angle = "";
					isElbow2 = false;
					categoryId = 0;
					serviceType2 = "";
				}
				else if (x.SubSystemOrder == 5 || x.SubSystemOrder == 6)
				{
					categoryId = 0;
					serviceType2 = "";
					if (settings.HvacDuctExportMode == 1)
					{
						text10 = "Ống gió thẳng";
						text11 = "";
						angle = "";
						isElbow2 = false;
						eiType = "";
					}
					else if (settings.HvacDuctExportMode == 2)
					{
						text10 = "";
						angle = "";
						isElbow2 = false;
						eiType = "";
					}
				}
				else if (x.SubSystemOrder == 4)
				{
					serviceType2 = "";
					string text12 = text10 ?? "";
					bool flag14 = text12.IndexOf("Lưi", StringComparison.OrdinalIgnoreCase) >= 0;
					bool flag15 = !flag14 && text12.IndexOf("Co", StringComparison.OrdinalIgnoreCase) >= 0;
					if ((flag14 || flag15) && text11.Contains("-"))
					{
						text11 = text11.Split('-')[0].Trim();
					}
				}
				if (x.Category.Id.GetIdInt() == -2008126 && text11.Contains("-"))
				{
					text11 = text11.Split('-')[0].Trim();
				}
				return new
				{
					SubSystemOrder = x.SubSystemOrder,
					GroupName = groupName2,
					TypeName = text10,
					Size = text11,
					Angle = angle,
					IsElbow = isElbow2,
					CategoryId = categoryId,
					DuctThickness = ((x.SubSystemOrder == 5 && settings.HvacDuctExportMode == 3) ? 0.0 : x.DuctThickness),
					ServiceType = serviceType2,
					IsBusway = x.IsBusway,
					IsBuswayPcs = x.IsBuswayPcs,
					EiType = eiType
				};
			})
			orderby g.Key.SubSystemOrder, g.Key.IsBusway ? (((g.Key.GroupName ?? "").IndexOf("FR", StringComparison.OrdinalIgnoreCase) >= 0 || (g.Key.TypeName ?? "").IndexOf("FR", StringComparison.OrdinalIgnoreCase) >= 0) ? 1 : 0) : 0, g.Key.IsBusway ? GetPipeCategoryOrder(g.Key.CategoryId) : 0
			select g).ThenBy(g =>
		{
			if (g.Key.IsBusway)
			{
				string input = g.Key.GroupName ?? "";
				Match match2 = Regex.Match(input, "\\d+");
				if (match2.Success && double.TryParse(match2.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
				{
					return result;
				}
				return 0.0;
			}
			return 0.0;
		}).ThenBy(g =>
		{
			if (g.Key.IsBusway)
			{
				string[] array = new string[2]
				{
					g.Key.Angle ?? "",
					g.Key.GroupName ?? ""
				};
				foreach (string input2 in array)
				{
					Match match3 = Regex.Match(input2, "(\\d{3,4})A", RegexOptions.IgnoreCase);
					if (match3.Success && double.TryParse(match3.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result2))
					{
						return result2;
					}
				}
				return 0.0;
			}
			return 0.0;
		}).ThenBy(g => (g.Key.SubSystemOrder == 3 && (settings.CondGroups == null || !settings.CondGroups.Any((BOQGroupItem cg) => cg.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()))))) ? (g.Key.ServiceType ?? "") : getGroupOrder(g.Key.GroupName).ToString("D10"))
			.ThenBy(g =>
			{
				string input3 = g.Key.TypeName ?? "";
				Match match4 = Regex.Match(input3, "(\\d{3,4})A\\b", RegexOptions.IgnoreCase);
				int result3;
				return (match4.Success && int.TryParse(match4.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out result3)) ? result3.ToString("D10") : (g.Key.GroupName ?? "");
			})
			.ThenBy(g => (g.Key.SubSystemOrder == 7) ? ((!g.Any((BOQExportItem x) => x.Category.Id.GetIdInt() == -2008044)) ? 1 : 0) : GetPipeCategoryOrder(g.Key.CategoryId))
			.ThenBy(g =>
			{
				if (g.Key.SubSystemOrder == 7)
				{
					Match match5 = Regex.Match(g.Key.TypeName ?? "", "DN(\\d+(\\.\\d+)?)", RegexOptions.IgnoreCase);
					if (match5.Success && double.TryParse(match5.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result4))
					{
						return result4;
					}
					return 9999.0;
				}
				return 0.0;
			})
			.ThenBy(g => (g.Key.SubSystemOrder == 5 && settings.HvacDuctExportMode == 3) ? (g.Key.TypeName ?? "") : "")
			.ThenBy(g => (g.Key.SubSystemOrder == 5 && settings.HvacDuctExportMode == 3) ? 0.0 : g.Key.DuctThickness)
			.ThenBy(g => (g.Key.SubSystemOrder != 3 && g.Key.SubSystemOrder != 8 && (g.Key.SubSystemOrder != 5 || settings.HvacDuctExportMode != 2)) ? 1 : 0)
			.ThenBy(g =>
			{
				if (g.Key.SubSystemOrder == 3 || g.Key.SubSystemOrder == 8 || (g.Key.SubSystemOrder == 5 && settings.HvacDuctExportMode == 2))
				{
					string input4 = g.Key.Size ?? "";
					Match match6 = Regex.Match(input4, "\\d+(\\.\\d+)?");
					if (match6.Success && double.TryParse(match6.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result5))
					{
						return result5;
					}
					return 9999.0;
				}
				return 0.0;
			})
			.ThenBy(g =>
			{
				if (g.Key.SubSystemOrder == 4)
				{
					string text13 = g.Key.TypeName ?? "";
					int num7 = text13.IndexOf("PN", StringComparison.OrdinalIgnoreCase);
					return (num7 > 0) ? text13.Substring(0, num7).TrimEnd() : text13;
				}
				return "";
			})
			.ThenBy(g =>
			{
				if (g.Key.SubSystemOrder == 4)
				{
					Match match7 = Regex.Match(g.Key.TypeName ?? "", "PN(\\d+)", RegexOptions.IgnoreCase);
					if (match7.Success && double.TryParse(match7.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result6))
					{
						return result6;
					}
					return 9999.0;
				}
				return 0.0;
			})
			.ThenBy(g => (g.Key.SubSystemOrder == 4 || (g.Key.SubSystemOrder == 5 && settings.HvacDuctExportMode == 3) || g.Key.SubSystemOrder == 8) ? "" : (g.Key.TypeName ?? ""))
			.ThenBy(g => (g.Key.SubSystemOrder != 5 || settings.HvacDuctExportMode != 3) ? 1 : 0)
			.ThenBy(g =>
			{
				if (g.Key.SubSystemOrder == 5 && settings.HvacDuctExportMode == 3)
				{
					string input5 = g.Key.Size ?? "";
					Match match8 = Regex.Match(input5, "\\d+(\\.\\d+)?");
					if (match8.Success && double.TryParse(match8.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result7))
					{
						return result7;
					}
					return 9999.0;
				}
				return 0.0;
			})
			.ThenBy(g =>
			{
				if (g.Key.SubSystemOrder == 4 || g.Key.SubSystemOrder == 9)
				{
					Match match9 = Regex.Match(g.Key.Size ?? "", "D(?:N)?(\\d+(\\.\\d+)?)", RegexOptions.IgnoreCase);
					if (match9.Success && double.TryParse(match9.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result8))
					{
						return result8;
					}
					return 9999.0;
				}
				return 0.0;
			})
			.ThenBy(g => g.Key.CategoryId switch
			{
				-2008130 => 0, 
				-2008126 => 1, 
				_ => 0, 
			})
			.ThenBy(g => (g.Key.SubSystemOrder == 8 || g.Key.SubSystemOrder == 4 || g.Key.SubSystemOrder == 9) ? "" : g.Key.Size)
			.ToList();
		int currentRow = 5;
		if (excludeHvacGasColumns)
		{
			WriteHvacGasSheet(sheet, allItems, levels, settings, bodyStyle, numericStyle, headerStyle, colIdxCategory, colIdxType, colIdxDauMuc, colIdxUnit, startLevelColIdx, totalColIndex, ref currentRow);
		}
		else
		{
			int stt = 1;
			int lastSubSystemOrder = -1;
			string lastGroupName = null;
			bool isFirstRow = true;
			string lastServiceType = null;
			bool lastHasFR = false;
			int lastCatId = 0;
			foreach (var group in groupedItems)
			{
				BOQExportItem firstItem = group.First();
				Category groupCategory = firstItem.Category;
				bool currentIsLinear = IsLinearCategory(groupCategory);
				bool currentHasFR = group.Key.IsBusway && ((group.Key.GroupName ?? "").IndexOf("FR", StringComparison.OrdinalIgnoreCase) >= 0 || (group.Key.TypeName ?? "").IndexOf("FR", StringComparison.OrdinalIgnoreCase) >= 0);
				int currentCatId = groupCategory.Id.GetIdInt();
				if (!isFirstRow)
				{
					if (group.Key.SubSystemOrder != lastSubSystemOrder)
					{
						sheet.CreateRow(currentRow++);
						stt = 1;
					}
					else if (group.Key.IsBusway)
					{
						if (currentHasFR != lastHasFR || currentCatId != lastCatId)
						{
							sheet.CreateRow(currentRow++);
							stt = 1;
						}
					}
					else if (group.Key.SubSystemOrder == 3)
					{
						if (settings.CondGroups != null && settings.CondGroups.Any((BOQGroupItem cg) => cg.FilterValues.Any((BOQFilterValue v) => !string.IsNullOrEmpty(v.Value?.Trim()))))
						{
							if (group.Key.GroupName != lastGroupName)
							{
								sheet.CreateRow(currentRow++);
								stt = 1;
							}
						}
						else if (group.Key.ServiceType != lastServiceType)
						{
							sheet.CreateRow(currentRow++);
							stt = 1;
						}
					}
					else if (group.Key.GroupName != lastGroupName)
					{
						sheet.CreateRow(currentRow++);
						stt = 1;
					}
				}
				isFirstRow = false;
				lastSubSystemOrder = group.Key.SubSystemOrder;
				lastGroupName = group.Key.GroupName;
				lastServiceType = group.Key.ServiceType;
				lastHasFR = currentHasFR;
				lastCatId = currentCatId;
				bool isGroupBusway = group.Any((BOQExportItem x) => x.IsBusway);
				IRow row = sheet.CreateRow(currentRow++);
				bool isHvacPipeMerged = group.Key.SubSystemOrder == 7;
				string displayTypeName = (isHvacPipeMerged ? firstItem.TypeName : group.Key.TypeName);
				string displaySize = (isHvacPipeMerged ? (firstItem.Size ?? "") : (group.Key.Size ?? ""));
				string displayAngle = (isHvacPipeMerged ? (firstItem.Angle ?? "") : (group.Key.Angle ?? ""));
				string newType = displayTypeName ?? "";
				int dashIdx = newType.IndexOf('-');
				if (dashIdx > 0)
				{
					newType = newType.Substring(0, dashIdx).Trim();
				}
				string raw = displayTypeName ?? "";
				if (raw.Length > 0 && raw[0] == 'Y' && displayAngle == "90")
				{
					string modified = "Tê" + raw.Substring(1);
					int modDash = modified.IndexOf('-');
					newType = ((modDash > 0) ? modified.Substring(0, modDash).Trim() : modified.Trim());
				}
				string raw2 = displayTypeName ?? "";
				int loiIdx = raw2.IndexOf("Lơi", StringComparison.OrdinalIgnoreCase);
				if (loiIdx >= 0)
				{
					if (displayAngle == "45")
					{
						newType = raw2;
					}
					else if (displayAngle == "90")
					{
						string modified2 = raw2.Substring(0, loiIdx) + "Co" + raw2.Substring(loiIdx + "Lơi".Length);
						int modDash2 = modified2.IndexOf('-');
						newType = ((modDash2 > 0) ? modified2.Substring(0, modDash2).Trim() : modified2.Trim());
					}
				}
				string raw3 = displayTypeName ?? "";
				if (raw3.IndexOf("Lơi", StringComparison.OrdinalIgnoreCase) < 0)
				{
					int coIdx = raw3.IndexOf("Co", StringComparison.OrdinalIgnoreCase);
					if (coIdx >= 0)
					{
						if (displayAngle == "90")
						{
							newType = raw3;
						}
						else if (displayAngle == "45")
						{
							string modified3 = raw3.Substring(0, coIdx) + "Lơi" + raw3.Substring(coIdx + "Co".Length);
							int modDash3 = modified3.IndexOf('-');
							newType = ((modDash3 > 0) ? modified3.Substring(0, modDash3).Trim() : modified3.Trim());
						}
					}
				}
				bool isConduit = group.Key.SubSystemOrder == 3;
				int catId2 = groupCategory.Id.GetIdInt();
				row.CreateCell(0).SetCellValue(stt++);
				if (!excludeCategoryAndType)
				{
					string categoryCell = ((group.Key.SubSystemOrder == 8) ? "Conduits & Fittings" : group.Key.GroupName);
					row.CreateCell(colIdxCategory).SetCellValue(categoryCell);
					if (isGroupBusway)
					{
						row.CreateCell(colIdxType).SetCellValue(group.Key.TypeName);
					}
					else
					{
						bool isHvacDuctMode2 = (group.Key.SubSystemOrder == 5 || group.Key.SubSystemOrder == 6) && settings.HvacDuctExportMode == 2;
						row.CreateCell(colIdxType).SetCellValue(isHvacDuctMode2 ? "" : displayTypeName);
					}
				}
				if (!excludeServiceType && colIdxServiceType != -1)
				{
					if (isConduit)
					{
						row.CreateCell(colIdxServiceType).SetCellValue(group.Key.ServiceType);
					}
					else if (isHvacDuctExport || isHvacDuctFittingExport)
					{
						row.CreateCell(colIdxServiceType).SetCellValue(group.Key.ServiceType);
					}
					else
					{
						string serviceTypeCell = ((group.Key.SubSystemOrder == 8) ? "Conduits & Fittings" : group.Key.GroupName);
						row.CreateCell(colIdxServiceType).SetCellValue(serviceTypeCell);
					}
				}
				if (colIdxSize != -1)
				{
					string sizeCellVal = displaySize;
					if (isConduit && catId2 == -2008128 && sizeCellVal.Contains("-"))
					{
						sizeCellVal = sizeCellVal.Split('-')[0].Trim();
					}
					row.CreateCell(colIdxSize).SetCellValue(sizeCellVal);
				}
				if (colIdxAngle != -1)
				{
					if (isConduit)
					{
						if (catId2 == -2008128)
						{
							row.CreateCell(colIdxAngle).SetCellValue(group.Key.Angle);
						}
						else
						{
							row.CreateCell(colIdxAngle).SetCellValue("");
						}
					}
					else if (isHvacDuctExport || isHvacDuctFittingExport)
					{
						if (group.Key.DuctThickness > 0.0)
						{
							row.CreateCell(colIdxAngle).SetCellValue(group.Key.DuctThickness);
						}
						else
						{
							row.CreateCell(colIdxAngle).SetCellValue("");
						}
					}
					else if (isGroupBusway)
					{
						if (group.Key.IsBuswayPcs && catId2 == -2008010)
						{
							row.CreateCell(colIdxAngle).SetCellValue(group.Key.Angle);
						}
						else
						{
							row.CreateCell(colIdxAngle).SetCellValue("");
						}
					}
					else if (catId2 == -2008126 && (group.Key.TypeName ?? "").IndexOf("Co thang cáp", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						row.CreateCell(colIdxAngle).SetCellValue(group.Key.Angle ?? "");
					}
					else
					{
						row.CreateCell(colIdxAngle).SetCellValue(displayAngle);
					}
				}
				catId2 = groupCategory.Id.GetIdInt();
				string dauMuc = "";
				if (group.Key.SubSystemOrder == 7)
				{
					dauMuc = group.Key.TypeName ?? "";
				}
				else if (group.Key.SubSystemOrder == 8)
				{
					dauMuc = "Gas " + group.Key.TypeName;
				}
				else if (isGroupBusway)
				{
					dauMuc = ((catId2 == -2008010 && group.Key.IsBuswayPcs) ? ((!(group.Key.Angle != "90") || string.IsNullOrEmpty(group.Key.Angle)) ? group.Key.TypeName : (group.Key.TypeName + " " + group.Key.Angle)) : (group.Key.IsBuswayPcs ? group.Key.TypeName : (group.Key.TypeName + " " + group.Key.Angle).Trim()));
				}
				else if (catId2 == -2008130)
				{
					dauMuc = group.Key.GroupName + " " + group.Key.Size;
				}
				else if (catId2 == -2008126)
				{
					string fittingSize = group.Key.Size ?? "";
					if (fittingSize.Contains("-"))
					{
						fittingSize = fittingSize.Split('-')[0].Trim();
					}
					if ((group.Key.TypeName ?? "").IndexOf("Co thang cáp", StringComparison.OrdinalIgnoreCase) >= 0 && double.TryParse(group.Key.Angle, out var angleDeg) && angleDeg >= 40.0 && angleDeg <= 50.0)
					{
						dauMuc = (group.Key.TypeName ?? "").Trim() + " " + (int)Math.Round(angleDeg) + "°" + (string.IsNullOrEmpty(fittingSize) ? "" : (" " + fittingSize));
					}
					else
					{
						dauMuc = (group.Key.TypeName ?? "").Trim();
						if (!string.IsNullOrEmpty(fittingSize))
						{
							dauMuc = dauMuc + " " + fittingSize;
						}
					}
					dauMuc = dauMuc.Trim();
				}
				else if (catId2 == -2008000 || catId2 == -2008010)
				{
					bool isHvacDuctSub = group.Key.SubSystemOrder == 5 || group.Key.SubSystemOrder == 6;
					if (isHvacDuctSub && settings.HvacDuctExportMode == 1)
					{
						dauMuc = "Ống gió tole dày " + group.Key.DuctThickness.ToString("0.00", CultureInfo.InvariantCulture) + "mm";
					}
					else if (!isHvacDuctSub || settings.HvacDuctExportMode != 2)
					{
						dauMuc = ((!isHvacDuctSub) ? ("Ống gió tole dày " + group.Key.DuctThickness.ToString("0.00", CultureInfo.InvariantCulture) + "mm") : (group.Key.TypeName + " " + group.Key.Size));
					}
					else
					{
						string thickStr = group.Key.DuctThickness.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
						dauMuc = "Ống gió tole dày " + thickStr + "mm " + group.Key.Size;
					}
				}
				else if (catId2 == -2008132 || catId2 == -2008128)
				{
					string cleanSize = group.Key.Size ?? "";
					if (catId2 == -2008128 && cleanSize.Contains("-"))
					{
						cleanSize = cleanSize.Split('-')[0].Trim();
					}
					cleanSize = Regex.Replace(cleanSize, "\\s*mm\\b", "", RegexOptions.IgnoreCase);
					cleanSize = Regex.Replace(cleanSize, "\\b(?<!D)(\\d+(\\.\\d+)?)", "D$1", RegexOptions.IgnoreCase);
					string typePart = group.Key.TypeName ?? "";
					dauMuc = typePart;
					if (catId2 == -2008128 && !string.IsNullOrEmpty(group.Key.Angle))
					{
						dauMuc = dauMuc + " " + group.Key.Angle;
					}
					if (!string.IsNullOrEmpty(cleanSize))
					{
						dauMuc = dauMuc + " " + cleanSize;
					}
					dauMuc = dauMuc.Trim();
				}
				else if (catId2 == -2008044 || catId2 == -2008049 || catId2 == -2008055 || catId2 == -2008099)
				{
					string rawTypeName = displayTypeName ?? "";
					bool startsWithY = rawTypeName.Length > 0 && rawTypeName[0] == 'Y';
					bool containsLoi = rawTypeName.IndexOf("Lơi", StringComparison.OrdinalIgnoreCase) >= 0;
					bool containsCo = !containsLoi && rawTypeName.IndexOf("Co", StringComparison.OrdinalIgnoreCase) >= 0;
					if (startsWithY || containsLoi || containsCo)
					{
						string sizeForDauMuc = displaySize;
						if ((containsCo || containsLoi) && group.Key.SubSystemOrder == 4 && sizeForDauMuc.Contains("-"))
						{
							sizeForDauMuc = sizeForDauMuc.Split('-')[0].Trim();
						}
						dauMuc = newType;
						if (!string.IsNullOrEmpty(sizeForDauMuc))
						{
							dauMuc = dauMuc + " " + sizeForDauMuc;
						}
						dauMuc = dauMuc.Trim();
					}
					else
					{
						dauMuc = newType;
						if (displayAngle == "45")
						{
							dauMuc += " 45";
						}
						if (!string.IsNullOrEmpty(displaySize))
						{
							dauMuc = dauMuc + " " + displaySize;
						}
						dauMuc = dauMuc.Trim();
					}
				}
				row.CreateCell(colIdxDauMuc).SetCellValue(dauMuc);
				string unit = "Set";
				if (group.Key.IsBusway)
				{
					unit = ((!group.Key.IsBuswayPcs || groupCategory.Id.GetIdInt() != -2008010) ? "m" : "Set");
				}
				else if (groupCategory.Id.GetIdInt() == -2008000 || groupCategory.Id.GetIdInt() == -2008010)
				{
					unit = ((group.Key.SubSystemOrder != 5 && group.Key.SubSystemOrder != 6) ? "m2" : ((settings.HvacDuctExportMode == 1) ? "m2" : "m"));
				}
				else if (group.Key.SubSystemOrder == 8)
				{
					unit = "m";
				}
				else if (IsLinearCategory(groupCategory) || (group.Key.SubSystemOrder == 8 && groupCategory.Id.GetIdInt() == -2008128))
				{
					unit = "m";
				}
				else if (IsAreaCategory(groupCategory))
				{
					unit = "m2";
				}
				else if (IsVolumeCategory(groupCategory))
				{
					unit = "m3";
				}
				row.CreateCell(colIdxUnit).SetCellValue(unit);
				double totalRowQty = 0.0;
				for (int m = 0; m < levels.Count; m++)
				{
					string currentLevelName = levels[m];
					List<BOQExportItem> levelItems = group.Where((BOQExportItem x) => x.Level == currentLevelName).ToList();
					if (levelItems.Any())
					{
						double levelQty = levelItems.Count();
						if (group.Key.IsBusway)
						{
							if (group.Key.IsBuswayPcs && groupCategory.Id.GetIdInt() == -2008010)
							{
								levelQty = levelItems.Count();
							}
							else if (!group.Key.IsBuswayPcs && groupCategory.Id.GetIdInt() == -2008010)
							{
								double sumFittingLength = 0.0;
								foreach (BOQExportItem item3 in levelItems)
								{
									double val2 = 0.0;
									Parameter p4 = item3.Element.LookupParameter("Chiều Dài Phụ Kiện");
									if (p4 != null && p4.HasValue)
									{
										string textVal = p4.AsString()?.Trim() ?? p4.AsValueString()?.Trim() ?? "";
										string clean3 = new string(textVal.Where((char c) => char.IsDigit(c) || c == '.' || c == ',').ToArray());
										clean3 = clean3.Replace(',', '.');
										if (double.TryParse(clean3, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
										{
											val2 = parsed / 1000.0;
										}
									}
									sumFittingLength += val2;
								}
								levelQty = sumFittingLength;
							}
							else
							{
								double rawLength = levelItems.Sum((BOQExportItem x) => GetParameterValue(x.Element, (BuiltInParameter)(-1004005)).GetValueOrDefault());
								levelQty = UnitUtils.ConvertFromInternalUnits(rawLength, UnitTypeId.Meters);
							}
						}
						else if (groupCategory.Id.GetIdInt() == -2008010)
						{
							levelQty = levelItems.Sum((BOQExportItem x) => GetDuctFittingSurfaceArea(x.Element));
						}
						else if (group.Key.SubSystemOrder == 8)
						{
							double totalMeters = 0.0;
							foreach (BOQExportItem item4 in levelItems)
							{
								switch (item4.Category.Id.GetIdInt())
								{
								case -2008132:
								{
									double rawLen = GetParameterValue(item4.Element, (BuiltInParameter)(-1004005)).GetValueOrDefault();
									totalMeters += UnitUtils.ConvertFromInternalUnits(rawLen, UnitTypeId.Meters);
									break;
								}
								case -2008128:
								{
									Parameter p5 = item4.Element.LookupParameter("Center to End") ?? item4.Element.LookupParameter("Center To End");
									if (p5 == null)
									{
										foreach (Parameter parameter4 in item4.Element.Parameters)
										{
											Parameter param = parameter4;
											if (param.Definition != null && param.Definition.Name.Equals("Center to End", StringComparison.OrdinalIgnoreCase))
											{
												p5 = param;
												break;
											}
										}
									}
									double feetVal = ((p5 != null && (int)p5.StorageType == 2) ? p5.AsDouble() : 0.0);
									totalMeters += UnitUtils.ConvertFromInternalUnits(feetVal, UnitTypeId.Meters) * 2.0;
									break;
								}
								}
							}
							levelQty = totalMeters;
						}
						else if (groupCategory.Id.GetIdInt() == -2008000)
						{
							if (settings.HvacDuctExportMode == 1)
							{
								double rawArea = levelItems.Sum(delegate(BOQExportItem x)
								{
									Parameter obj4 = x.Element.get_Parameter((BuiltInParameter)(-1114120)) ?? x.Element.get_Parameter((BuiltInParameter)(-1012805)) ?? x.Element.LookupParameter("Area");
									return (obj4 != null) ? obj4.AsDouble() : 0.0;
								});
								levelQty = UnitUtils.ConvertFromInternalUnits(rawArea, UnitTypeId.SquareMeters);
							}
							else
							{
								double rawLength2 = levelItems.Sum((BOQExportItem x) => GetParameterValue(x.Element, (BuiltInParameter)(-1004005)).GetValueOrDefault());
								levelQty = UnitUtils.ConvertFromInternalUnits(rawLength2, UnitTypeId.Meters);
							}
						}
						else if (IsLinearCategory(groupCategory))
						{
							double rawLength3 = levelItems.Sum((BOQExportItem x) => GetParameterValue(x.Element, (BuiltInParameter)(-1004005)).GetValueOrDefault());
							levelQty = UnitUtils.ConvertFromInternalUnits(rawLength3, UnitTypeId.Meters);
						}
						else if (IsAreaCategory(groupCategory))
						{
							double rawArea2 = levelItems.Sum(delegate(BOQExportItem x)
							{
								Parameter obj5 = x.Element.get_Parameter((BuiltInParameter)(-1114120)) ?? x.Element.get_Parameter((BuiltInParameter)(-1012805)) ?? x.Element.LookupParameter("Area");
								return (obj5 != null) ? obj5.AsDouble() : 0.0;
							});
							levelQty = UnitUtils.ConvertFromInternalUnits(rawArea2, UnitTypeId.SquareMeters);
						}
						else if (IsVolumeCategory(groupCategory))
						{
							double rawVolume = levelItems.Sum(delegate(BOQExportItem x)
							{
								Parameter obj6 = x.Element.get_Parameter((BuiltInParameter)(-1012806)) ?? x.Element.LookupParameter("Volume") ?? x.Element.LookupParameter("Structural Volume");
								return (obj6 != null) ? obj6.AsDouble() : 0.0;
							});
							levelQty = UnitUtils.ConvertFromInternalUnits(rawVolume, UnitTypeId.CubicMeters);
						}
						ICell qtyCell = row.CreateCell(startLevelColIdx + m);
						qtyCell.SetCellValue(levelQty);
						qtyCell.CellStyle = numericStyle;
						totalRowQty += levelQty;
					}
					else
					{
						ICell qtyCell2 = row.CreateCell(startLevelColIdx + m);
						qtyCell2.CellStyle = bodyStyle;
					}
				}
				ICell totalQtyCell = row.CreateCell(totalColIndex);
				totalQtyCell.SetCellValue(totalRowQty);
				totalQtyCell.CellStyle = numericStyle;
				for (int n = 0; n <= colIdxUnit; n++)
				{
					row.GetCell(n).CellStyle = bodyStyle;
				}
			}
		}
		int totalColumnsCount = 7 + levels.Count + 1;
		MethodInfo getColWidthMethod = sheet.GetType().GetMethod("GetColumnWidth", new Type[1] { typeof(int) }) ?? typeof(ISheet).GetMethod("GetColumnWidth", new Type[1] { typeof(int) });
		MethodInfo setColWidthMethod = sheet.GetType().GetMethod("SetColumnWidth", new Type[2]
		{
			typeof(int),
			typeof(int)
		}) ?? sheet.GetType().GetMethod("SetColumnWidth", new Type[2]
		{
			typeof(int),
			typeof(double)
		}) ?? typeof(ISheet).GetMethod("SetColumnWidth", new Type[2]
		{
			typeof(int),
			typeof(int)
		}) ?? typeof(ISheet).GetMethod("SetColumnWidth", new Type[2]
		{
			typeof(int),
			typeof(double)
		});
		for (int num8 = 0; num8 < totalColumnsCount; num8++)
		{
			sheet.AutoSizeColumn(num8);
			int width = 2000;
			if (getColWidthMethod != null)
			{
				try
				{
					object val3 = getColWidthMethod.Invoke(sheet, new object[1] { num8 });
					width = Convert.ToInt32(val3);
				}
				catch
				{
				}
			}
			int newWidth = width + 1500;
			if (newWidth < 2500)
			{
				newWidth = 2500;
			}
			if (!(setColWidthMethod != null))
			{
				continue;
			}
			try
			{
				ParameterInfo[] parameters = setColWidthMethod.GetParameters();
				if (parameters[1].ParameterType == typeof(double))
				{
					setColWidthMethod.Invoke(sheet, new object[2]
					{
						num8,
						(double)newWidth
					});
				}
				else
				{
					setColWidthMethod.Invoke(sheet, new object[2] { num8, newWidth });
				}
			}
			catch
			{
			}
		}
		using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
		{
			workbook.Write(fs);
		}
		if (autoOpen)
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(filePath);
			processStartInfo.UseShellExecute = true;
			Process.Start(processStartInfo);
		}
	}

	private static double GetDuctFittingSurfaceArea(Element e)
	{
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Invalid comparison between Unknown and I4
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Invalid comparison between Unknown and I4
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Invalid comparison between Unknown and I4
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Invalid comparison between Unknown and I4
		//IL_0977: Unknown result type (might be due to invalid IL or missing references)
		//IL_097d: Invalid comparison between Unknown and I4
		//IL_0da7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0dad: Invalid comparison between Unknown and I4
		//IL_09d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_09dd: Invalid comparison between Unknown and I4
		//IL_054f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0555: Invalid comparison between Unknown and I4
		//IL_0e16: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e1c: Invalid comparison between Unknown and I4
		//IL_09fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a03: Invalid comparison between Unknown and I4
		//IL_0577: Unknown result type (might be due to invalid IL or missing references)
		//IL_057d: Invalid comparison between Unknown and I4
		//IL_0b33: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b39: Invalid comparison between Unknown and I4
		//IL_0a37: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a3d: Invalid comparison between Unknown and I4
		//IL_0581: Unknown result type (might be due to invalid IL or missing references)
		//IL_0587: Invalid comparison between Unknown and I4
		//IL_0386: Unknown result type (might be due to invalid IL or missing references)
		//IL_038c: Invalid comparison between Unknown and I4
		//IL_02e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f0: Expected O, but got Unknown
		//IL_0e85: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e8b: Invalid comparison between Unknown and I4
		//IL_065a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0316: Unknown result type (might be due to invalid IL or missing references)
		//IL_031c: Invalid comparison between Unknown and I4
		//IL_0663: Unknown result type (might be due to invalid IL or missing references)
		//IL_0669: Invalid comparison between Unknown and I4
		//IL_0eab: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eb1: Invalid comparison between Unknown and I4
		//IL_03ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Invalid comparison between Unknown and I4
		//IL_0ee5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0eeb: Invalid comparison between Unknown and I4
		//IL_0723: Unknown result type (might be due to invalid IL or missing references)
		//IL_0729: Invalid comparison between Unknown and I4
		//IL_0f1f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f25: Invalid comparison between Unknown and I4
		//IL_0462: Unknown result type (might be due to invalid IL or missing references)
		//IL_0468: Invalid comparison between Unknown and I4
		//IL_0439: Unknown result type (might be due to invalid IL or missing references)
		//IL_043f: Invalid comparison between Unknown and I4
		//IL_0f59: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f5f: Invalid comparison between Unknown and I4
		//IL_0f93: Unknown result type (might be due to invalid IL or missing references)
		//IL_0f99: Invalid comparison between Unknown and I4
		try
		{
			List<Connector> connectors = new List<Connector>();
			FamilyInstance fi = (FamilyInstance)(object)((e is FamilyInstance) ? e : null);
			if (fi != null && fi.MEPModel != null && fi.MEPModel.ConnectorManager != null)
			{
				foreach (Connector connector in fi.MEPModel.ConnectorManager.Connectors)
				{
					Connector conn = connector;
					if ((int)conn.ConnectorType == 19 || (int)conn.ConnectorType == 1)
					{
						connectors.Add(conn);
					}
				}
			}
			if (connectors.Count == 0)
			{
				return GetDuctFittingAreaFallback(e);
			}
			if (connectors.Count == 1)
			{
				Connector conn2 = connectors[0];
				if ((int)conn2.Shape == 1)
				{
					double w = conn2.Width * 304.8;
					double h = conn2.Height * 304.8;
					return w * h / 1000000.0;
				}
				if ((int)conn2.Shape == 0)
				{
					double d = conn2.Radius * 2.0 * 304.8;
					return Math.PI * d * d / 4000000.0;
				}
				return GetDuctFittingAreaFallback(e);
			}
			if (connectors.Count == 2)
			{
				Connector conn3 = connectors[0];
				Connector conn4 = connectors[1];
				bool isElbow = false;
				FamilyInstance fi_elbow = (FamilyInstance)(object)((e is FamilyInstance) ? e : null);
				if (fi_elbow != null)
				{
					FamilySymbol symbol = fi_elbow.Symbol;
					if (((symbol != null) ? symbol.Family : null) != null)
					{
						Parameter partTypeParam = ((Element)fi_elbow.Symbol.Family).get_Parameter((BuiltInParameter)(-1114206));
						if (partTypeParam != null && partTypeParam.AsInteger() == 5)
						{
							isElbow = true;
						}
					}
				}
				if (!isElbow)
				{
					string fName = (e.Name ?? "").ToLower();
					string famName = (GetFamilyName(e) ?? "").ToLower();
					string combined = fName + " " + famName;
					isElbow = combined.Contains("elbow") || combined.Contains("cút") || combined.Contains("lơi") || combined.Contains("co");
				}
				if (isElbow)
				{
					double theta = 90.0;
					Parameter angleParam = e.LookupParameter("Angle") ?? e.LookupParameter("Góc");
					if (angleParam == null)
					{
						foreach (Parameter parameter in e.Parameters)
						{
							Parameter p = parameter;
							if (p.Definition != null && p.Definition.Name.IndexOf("Angle", StringComparison.OrdinalIgnoreCase) >= 0 && (int)p.StorageType == 2)
							{
								angleParam = p;
								break;
							}
						}
					}
					if (angleParam != null && angleParam.HasValue)
					{
						theta = angleParam.AsDouble() * 180.0 / Math.PI;
					}
					double wVal = (((int)conn3.Shape == 1) ? (conn3.Width * 304.8) : (conn3.Radius * 2.0 * 304.8));
					double R = wVal * 0.5;
					Parameter radParam = e.LookupParameter("Bend Radius") ?? e.LookupParameter("Bán kính");
					if (radParam != null && (int)radParam.StorageType == 2)
					{
						R = radParam.AsDouble() * 304.8;
					}
					else
					{
						Parameter lenParam = e.LookupParameter("Center to End") ?? e.LookupParameter("Length");
						if (lenParam != null && (int)lenParam.StorageType == 2)
						{
							R = lenParam.AsDouble() * 304.8;
						}
					}
					if ((int)conn3.Shape == 1)
					{
						double w2 = conn3.Width * 304.8;
						double h2 = conn3.Height * 304.8;
						return 2.0 * (w2 + h2) * R * theta * (Math.PI / 180.0) / 1000000.0;
					}
					double d2 = conn3.Radius * 2.0 * 304.8;
					return Math.PI * d2 * R * theta * (Math.PI / 180.0) / 1000000.0;
				}
				double L = 300.0;
				Parameter lenParam2 = e.LookupParameter("Length") ?? e.LookupParameter("Chiều dài") ?? e.LookupParameter("Duct Length");
				if (lenParam2 != null && (int)lenParam2.StorageType == 2)
				{
					L = lenParam2.AsDouble() * 304.8;
				}
				if ((int)conn3.Shape == 1 && (int)conn4.Shape == 1)
				{
					double w3 = conn3.Width * 304.8;
					double h3 = conn3.Height * 304.8;
					double w4 = conn4.Width * 304.8;
					double h4 = conn4.Height * 304.8;
					double Lw = Math.Sqrt(L * L + Math.Pow((h3 - h4) / 2.0, 2.0));
					double Lh = Math.Sqrt(L * L + Math.Pow((w3 - w4) / 2.0, 2.0));
					return ((w3 + w4) * Lw + (h3 + h4) * Lh) / 1000000.0;
				}
				if ((int)conn3.Shape == 0 && (int)conn4.Shape == 0)
				{
					double d3 = conn3.Radius * 2.0 * 304.8;
					double d4 = conn4.Radius * 2.0 * 304.8;
					double s = Math.Sqrt(L * L + Math.Pow((d3 - d4) / 2.0, 2.0));
					return Math.PI * (d3 + d4) * s / 2000000.0;
				}
				double w5 = 0.0;
				double h5 = 0.0;
				double d5 = 0.0;
				if ((int)conn3.Shape == 1)
				{
					w5 = conn3.Width * 304.8;
					h5 = conn3.Height * 304.8;
					d5 = conn4.Radius * 2.0 * 304.8;
				}
				else
				{
					w5 = conn4.Width * 304.8;
					h5 = conn4.Height * 304.8;
					d5 = conn3.Radius * 2.0 * 304.8;
				}
				double p2 = 2.0 * (w5 + h5);
				double p3 = Math.PI * d5;
				double maxDim = Math.Max(w5, h5);
				double s2 = Math.Sqrt(L * L + Math.Pow((maxDim - d5) / 2.0, 2.0));
				return (p2 + p3) * s2 / 2000000.0;
			}
			if (connectors.Count == 3)
			{
				Connector connM1 = connectors[0];
				Connector connM2 = connectors[1];
				Connector connB = connectors[2];
				double minDot = 1.0;
				int bestI = 0;
				int bestJ = 1;
				for (int i = 0; i < 3; i++)
				{
					for (int j = i + 1; j < 3; j++)
					{
						try
						{
							double dot = connectors[i].CoordinateSystem.BasisZ.DotProduct(connectors[j].CoordinateSystem.BasisZ);
							if (dot < minDot)
							{
								minDot = dot;
								bestI = i;
								bestJ = j;
							}
						}
						catch
						{
						}
					}
				}
				connM1 = connectors[bestI];
				connM2 = connectors[bestJ];
				for (int k = 0; k < 3; k++)
				{
					if (k != bestI && k != bestJ)
					{
						connB = connectors[k];
						break;
					}
				}
				double Lm = 400.0;
				Parameter lenM = e.LookupParameter("Main Length") ?? e.LookupParameter("Length") ?? e.LookupParameter("Chiều dài");
				if (lenM != null && (int)lenM.StorageType == 2)
				{
					Lm = lenM.AsDouble() * 304.8;
				}
				double Lb = 200.0;
				Parameter lenB = e.LookupParameter("Branch Length") ?? e.LookupParameter("Extension") ?? e.LookupParameter("Chiều dài nhánh");
				if (lenB != null && (int)lenB.StorageType == 2)
				{
					Lb = lenB.AsDouble() * 304.8;
				}
				if ((int)connM1.Shape == 1)
				{
					double wm = connM1.Width * 304.8;
					double hm = connM1.Height * 304.8;
					if ((int)connB.Shape == 1)
					{
						double wb = connB.Width * 304.8;
						double hb = connB.Height * 304.8;
						return (2.0 * (wm + hm) * Lm + 2.0 * (wb + hb) * Lb - wb * hb) / 1000000.0;
					}
					double db = connB.Radius * 2.0 * 304.8;
					return (2.0 * (wm + hm) * Lm + Math.PI * db * Lb - Math.PI * db * db / 4.0) / 1000000.0;
				}
				double dm = connM1.Radius * 2.0 * 304.8;
				if ((int)connB.Shape == 1)
				{
					double wb2 = connB.Width * 304.8;
					double hb2 = connB.Height * 304.8;
					return (Math.PI * dm * Lm + 2.0 * (wb2 + hb2) * Lb - wb2 * hb2) / 1000000.0;
				}
				double db2 = connB.Radius * 2.0 * 304.8;
				return (Math.PI * dm * Lm + Math.PI * db2 * Lb - Math.PI * db2 * db2 / 4.0) / 1000000.0;
			}
			if (connectors.Count >= 4)
			{
				Connector connM3 = connectors[0];
				Connector connM4 = connectors[1];
				Connector connB2 = connectors[2];
				Connector connB3 = connectors[3];
				double minDot2 = 1.0;
				int bestI2 = 0;
				int bestJ2 = 1;
				for (int l = 0; l < connectors.Count; l++)
				{
					for (int m = l + 1; m < connectors.Count; m++)
					{
						try
						{
							double dot2 = connectors[l].CoordinateSystem.BasisZ.DotProduct(connectors[m].CoordinateSystem.BasisZ);
							if (dot2 < minDot2)
							{
								minDot2 = dot2;
								bestI2 = l;
								bestJ2 = m;
							}
						}
						catch
						{
						}
					}
				}
				connM3 = connectors[bestI2];
				connM4 = connectors[bestJ2];
				List<Connector> branches = new List<Connector>();
				for (int n = 0; n < connectors.Count; n++)
				{
					if (n != bestI2 && n != bestJ2)
					{
						branches.Add(connectors[n]);
					}
				}
				if (branches.Count >= 2)
				{
					connB2 = branches[0];
					connB3 = branches[1];
				}
				double Lm2 = 500.0;
				Parameter lenM2 = e.LookupParameter("Main Length") ?? e.LookupParameter("Length") ?? e.LookupParameter("Chiều dài");
				if (lenM2 != null && (int)lenM2.StorageType == 2)
				{
					Lm2 = lenM2.AsDouble() * 304.8;
				}
				double Lb2 = 250.0;
				Parameter lenB2 = e.LookupParameter("Branch 1 Length") ?? e.LookupParameter("Extension 1") ?? e.LookupParameter("Branch Length") ?? e.LookupParameter("Chiều dài nhánh 1");
				if (lenB2 != null && (int)lenB2.StorageType == 2)
				{
					Lb2 = lenB2.AsDouble() * 304.8;
				}
				double Lb3 = 250.0;
				Parameter lenB3 = e.LookupParameter("Branch 2 Length") ?? e.LookupParameter("Extension 2") ?? e.LookupParameter("Branch Length") ?? e.LookupParameter("Chiều dài nhánh 2");
				if (lenB3 != null && (int)lenB3.StorageType == 2)
				{
					Lb3 = lenB3.AsDouble() * 304.8;
				}
				if ((int)connM3.Shape == 1)
				{
					double wm2 = connM3.Width * 304.8;
					double hm2 = connM3.Height * 304.8;
					double wb3 = (((int)connB2.Shape == 1) ? (connB2.Width * 304.8) : (connB2.Radius * 2.0 * 304.8));
					double hb3 = (((int)connB2.Shape == 1) ? (connB2.Height * 304.8) : (connB2.Radius * 2.0 * 304.8));
					double wb4 = (((int)connB3.Shape == 1) ? (connB3.Width * 304.8) : (connB3.Radius * 2.0 * 304.8));
					double hb4 = (((int)connB3.Shape == 1) ? (connB3.Height * 304.8) : (connB3.Radius * 2.0 * 304.8));
					return (2.0 * (wm2 + hm2) * Lm2 + 2.0 * (wb3 + hb3) * Lb2 + 2.0 * (wb4 + hb4) * Lb3 - wb3 * hb3 - wb4 * hb4) / 1000000.0;
				}
				double dm2 = connM3.Radius * 2.0 * 304.8;
				double db3 = connB2.Radius * 2.0 * 304.8;
				double db4 = connB3.Radius * 2.0 * 304.8;
				return (Math.PI * dm2 * Lm2 + Math.PI * db3 * Lb2 + Math.PI * db4 * Lb3 - Math.PI * db3 * db3 / 4.0 - Math.PI * db4 * db4 / 4.0) / 1000000.0;
			}
		}
		catch
		{
		}
		return GetDuctFittingAreaFallback(e);
	}

	private static double GetDuctFittingAreaFallback(Element e)
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Invalid comparison between Unknown and I4
		try
		{
			double L = 300.0;
			Parameter lenParam = e.LookupParameter("Length") ?? e.LookupParameter("Chiều dài") ?? e.LookupParameter("Duct Length");
			if (lenParam != null && (int)lenParam.StorageType == 2)
			{
				L = lenParam.AsDouble() * 304.8;
			}
			Parameter wParam = e.get_Parameter((BuiltInParameter)(-1114101)) ?? e.LookupParameter("Width") ?? e.LookupParameter("Duct Width");
			Parameter hParam = e.get_Parameter((BuiltInParameter)(-1114102)) ?? e.LookupParameter("Height") ?? e.LookupParameter("Duct Height");
			Parameter dParam = e.get_Parameter((BuiltInParameter)(-1114103)) ?? e.LookupParameter("Diameter") ?? e.LookupParameter("Duct Diameter");
			if (wParam != null && hParam != null && wParam.HasValue && hParam.HasValue)
			{
				double w = wParam.AsDouble() * 304.8;
				double h = hParam.AsDouble() * 304.8;
				return 2.0 * (w + h) * L / 1000000.0;
			}
			if (dParam != null && dParam.HasValue)
			{
				double d = dParam.AsDouble() * 304.8;
				return Math.PI * d * L / 1000000.0;
			}
		}
		catch
		{
		}
		return 0.2;
	}

	private static double GetMaxDimensionFromSizeString(string sizeStr)
	{
		if (string.IsNullOrEmpty(sizeStr))
		{
			return 0.0;
		}
		MatchCollection matches = Regex.Matches(sizeStr, "\\d+(\\.\\d+)?");
		double maxVal = 0.0;
		foreach (Match match in matches)
		{
			if (double.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) && val > maxVal)
			{
				maxVal = val;
			}
		}
		return maxVal;
	}

	private static double GetDuctThickness(Element e, string sizeStr, BOQSettings settings)
	{
		double maxDim = GetMaxDimensionFromSizeString(sizeStr);
		if (maxDim <= 0.0)
		{
			return 0.0;
		}
		if (string.IsNullOrEmpty(sizeStr) || (sizeStr.IndexOf("ø", StringComparison.OrdinalIgnoreCase) < 0 && sizeStr.IndexOf("d", StringComparison.OrdinalIgnoreCase) < 0 && sizeStr.Contains("x")))
		{
			if (settings.DuctThicknessRect != null && settings.DuctThicknessRect.Any())
			{
				return settings.DuctThicknessRect.OrderBy((DuctThicknessRule r) => r.Limit).FirstOrDefault((DuctThicknessRule r) => r.Limit >= maxDim)?.Thickness ?? settings.DuctThicknessRect.Max((DuctThicknessRule r) => r.Thickness);
			}
			if (maxDim <= 200.0)
			{
				return 0.58;
			}
			if (maxDim <= 350.0)
			{
				return 0.58;
			}
			if (maxDim <= 500.0)
			{
				return 0.75;
			}
			if (maxDim <= 750.0)
			{
				return 0.75;
			}
			if (maxDim <= 1000.0)
			{
				return 0.75;
			}
			if (maxDim <= 1250.0)
			{
				return 0.95;
			}
			if (maxDim <= 1600.0)
			{
				return 0.95;
			}
			if (maxDim <= 2100.0)
			{
				return 1.15;
			}
			if (maxDim <= 2500.0)
			{
				return 1.15;
			}
			return 1.15;
		}
		if (settings.DuctThicknessRound != null && settings.DuctThicknessRound.Any())
		{
			return settings.DuctThicknessRound.OrderBy((DuctThicknessRule r) => r.Limit).FirstOrDefault((DuctThicknessRule r) => r.Limit >= maxDim)?.Thickness ?? settings.DuctThicknessRound.Max((DuctThicknessRule r) => r.Thickness);
		}
		if (maxDim <= 200.0)
		{
			return 0.58;
		}
		if (maxDim <= 350.0)
		{
			return 0.75;
		}
		if (maxDim <= 500.0)
		{
			return 0.75;
		}
		if (maxDim <= 750.0)
		{
			return 0.95;
		}
		if (maxDim <= 1000.0)
		{
			return 0.95;
		}
		if (maxDim <= 1250.0)
		{
			return 1.15;
		}
		if (maxDim <= 1600.0)
		{
			return 1.15;
		}
		if (maxDim <= 2100.0)
		{
			return 1.15;
		}
		if (maxDim <= 2500.0)
		{
			return 1.15;
		}
		return 1.15;
	}

	private static string GetDuctEiType(Element e)
	{
		if (e == null)
		{
			return "EI0";
		}
		string[] possibleParams = new string[7] { "EI", "Chủng loại EI", "chủng loại EI", "Fire Rating", "Chủng loại", "EI Class", "EI_Class" };
		string[] array = possibleParams;
		foreach (string name in array)
		{
			Parameter p = e.LookupParameter(name);
			if (p == null)
			{
				ElementId typeId = e.GetTypeId();
				if (typeId != ElementId.InvalidElementId)
				{
					Element typeElem = e.Document.GetElement(typeId);
					if (typeElem != null)
					{
						p = typeElem.LookupParameter(name);
					}
				}
			}
			if (p != null && p.HasValue)
			{
				string val = p.AsString()?.Trim() ?? p.AsValueString()?.Trim() ?? "";
				if (!string.IsNullOrEmpty(val))
				{
					return val;
				}
			}
		}
		string typeName = e.Name ?? "";
		Match match = Regex.Match(typeName, "EI\\s*\\d+", RegexOptions.IgnoreCase);
		if (match.Success)
		{
			return match.Value.ToUpper();
		}
		return "EI0";
	}

	private static bool MatchesFilter(string value, string filterType, string filterValue)
	{
		if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(filterValue))
		{
			return false;
		}
		return filterType switch
		{
			"contain" => value.IndexOf(filterValue, StringComparison.OrdinalIgnoreCase) >= 0, 
			"doesn't contain" => value.IndexOf(filterValue, StringComparison.OrdinalIgnoreCase) < 0, 
			"equal" => value.Equals(filterValue, StringComparison.OrdinalIgnoreCase), 
			"not equal" => !value.Equals(filterValue, StringComparison.OrdinalIgnoreCase), 
			_ => false, 
		};
	}

	private static void AddMergedRegionSafe(ISheet sheet, int firstRow, int lastRow, int firstCol, int lastCol)
	{
		if ((firstRow != lastRow || firstCol != lastCol) && firstRow <= lastRow && firstCol <= lastCol)
		{
			sheet.AddMergedRegion(new CellRangeAddress(firstRow, lastRow, firstCol, lastCol));
		}
	}

	private static bool IsLinearCategory(Category cat)
	{
		int id = cat.Id.GetIdInt();
		return id == -2008044 || id == -2008130 || id == -2008132;
	}

	private static int GetPcccCategoryOrder(int catId)
	{
		switch (catId)
		{
		case -2008044:
			return 1;
		default:
			if (catId != -2008055)
			{
				if (catId == -2008099)
				{
					return 3;
				}
				return 99;
			}
			goto case -2008049;
		case -2008049:
			return 2;
		}
	}

	private static int GetPipeCategoryOrder(int catId)
	{
		if (catId == -2008044 || catId == -2008132 || catId == -2008000)
		{
			return 0;
		}
		if (catId == -2008049 || catId == -2008128 || catId == -2008010)
		{
			return 1;
		}
		return catId switch
		{
			-2008055 => 2, 
			-2008130 => 0, 
			-2008126 => 1, 
			_ => 0, 
		};
	}

	private static bool IsAreaCategory(Category cat)
	{
		int id = cat.Id.GetIdInt();
		return id == -2000011 || id == -2000032 || id == -2000035 || id == -2000038 || id == -2008000;
	}

	private static bool IsVolumeCategory(Category cat)
	{
		int id = cat.Id.GetIdInt();
		return id == -2001330 || id == -2001320 || id == -2001300;
	}

	private static string GetFamilyName(Element e)
	{
		Parameter obj = e.get_Parameter((BuiltInParameter)(-1002051));
		object obj2 = ((obj != null) ? obj.AsValueString() : null);
		if (obj2 == null)
		{
			Parameter obj3 = e.get_Parameter((BuiltInParameter)(-1002002));
			obj2 = ((obj3 != null) ? obj3.AsValueString() : null) ?? e.Name;
		}
		return (string)obj2;
	}

	private static string GetServiceType(Element e)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Invalid comparison between Unknown and I4
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bf: Invalid comparison between Unknown and I4
		Parameter serviceTypeParam = e.get_Parameter((BuiltInParameter)(-1140128)) ?? e.LookupParameter("Service Type") ?? e.LookupParameter("ServiceType");
		if (serviceTypeParam != null && serviceTypeParam.HasValue)
		{
			string val = (((int)serviceTypeParam.StorageType == 3) ? serviceTypeParam.AsString() : serviceTypeParam.AsValueString());
			if (!string.IsNullOrEmpty(val))
			{
				return val;
			}
		}
		Parameter sysTypeParam = e.get_Parameter((BuiltInParameter)(-1140333)) ?? e.get_Parameter((BuiltInParameter)(-1140334)) ?? e.LookupParameter("System Type") ?? e.LookupParameter("System Type Name");
		if (sysTypeParam != null && sysTypeParam.HasValue)
		{
			string val2 = (((int)sysTypeParam.StorageType == 3) ? sysTypeParam.AsString() : sysTypeParam.AsValueString());
			if (!string.IsNullOrEmpty(val2))
			{
				return val2;
			}
		}
		return "";
	}

	private static string GetElementSize(Element e)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Invalid comparison between Unknown and I4
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Invalid comparison between Unknown and I4
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Invalid comparison between Unknown and I4
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Invalid comparison between Unknown and I4
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fd: Invalid comparison between Unknown and I4
		Parameter sizeParam = e.get_Parameter((BuiltInParameter)(-1114240)) ?? e.LookupParameter("Size");
		if (sizeParam != null && sizeParam.HasValue)
		{
			string val = (((int)sizeParam.StorageType == 3) ? sizeParam.AsString() : sizeParam.AsValueString());
			if (!string.IsNullOrEmpty(val))
			{
				return val;
			}
		}
		Parameter diameterParam = e.get_Parameter((BuiltInParameter)(-1140238)) ?? e.get_Parameter((BuiltInParameter)(-1140225)) ?? e.get_Parameter((BuiltInParameter)(-1140123)) ?? e.LookupParameter("Diameter") ?? e.LookupParameter("Nominal Diameter");
		if (diameterParam != null && diameterParam.HasValue)
		{
			string val2 = (((int)diameterParam.StorageType == 3) ? diameterParam.AsString() : diameterParam.AsValueString());
			if (!string.IsNullOrEmpty(val2))
			{
				return val2;
			}
		}
		Parameter widthParam = e.get_Parameter((BuiltInParameter)(-1140122)) ?? e.get_Parameter((BuiltInParameter)(-1114101)) ?? e.LookupParameter("Width");
		Parameter heightParam = e.get_Parameter((BuiltInParameter)(-1140121)) ?? e.get_Parameter((BuiltInParameter)(-1114102)) ?? e.LookupParameter("Height");
		if (widthParam != null && widthParam.HasValue && heightParam != null && heightParam.HasValue)
		{
			string w = (((int)widthParam.StorageType == 3) ? widthParam.AsString() : widthParam.AsValueString());
			string h = (((int)heightParam.StorageType == 3) ? heightParam.AsString() : heightParam.AsValueString());
			if (!string.IsNullOrEmpty(w) && !string.IsNullOrEmpty(h))
			{
				return w + "x" + h;
			}
		}
		Parameter ktParam = e.LookupParameter("Size") ?? e.LookupParameter("Kích thước");
		if (ktParam != null && ktParam.HasValue)
		{
			string val3 = (((int)ktParam.StorageType == 3) ? ktParam.AsString() : ktParam.AsValueString());
			if (!string.IsNullOrEmpty(val3))
			{
				return val3;
			}
		}
		return "";
	}

	private static string GetPlumbingPipeSize(Element e, string typeName, int subSystemOrder, int catId)
	{
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0213: Invalid comparison between Unknown and I4
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Invalid comparison between Unknown and I4
		bool isPlumbing = subSystemOrder == 4 || subSystemOrder == 6 || subSystemOrder == 7;
		bool isPipeCurve = catId == -2008044;
		bool isPipeFittingOrAccessory = catId == -2008049 || catId == -2008055;
		if (!isPlumbing)
		{
			return GetElementSize(e);
		}
		if (!isPipeCurve && !isPipeFittingOrAccessory)
		{
			return GetElementSize(e);
		}
		string familyName = GetFamilyName(e) ?? "";
		bool isPprOrUpvc = typeName.IndexOf("PPR", StringComparison.OrdinalIgnoreCase) >= 0 || familyName.IndexOf("PPR", StringComparison.OrdinalIgnoreCase) >= 0 || typeName.IndexOf("uPVC", StringComparison.OrdinalIgnoreCase) >= 0 || familyName.IndexOf("uPVC", StringComparison.OrdinalIgnoreCase) >= 0;
		if (isPipeCurve)
		{
			if (isPprOrUpvc)
			{
				Parameter odParam = e.get_Parameter((BuiltInParameter)(-1140238)) ?? e.LookupParameter("Outside Diameter") ?? e.LookupParameter("Đường kính ngoài");
				if (odParam != null && odParam.HasValue && (int)odParam.StorageType == 2)
				{
					double odVal = odParam.AsDouble();
					if (odVal > 0.0)
					{
						double odMm = UnitUtils.ConvertFromInternalUnits(odVal, UnitTypeId.Millimeters);
						double odRounded = Math.Round(odMm, 1);
						string odStr = ((odRounded % 1.0 == 0.0) ? ((int)odRounded).ToString() : odRounded.ToString());
						return "D" + odStr;
					}
				}
				string szFallback = GetElementSize(e);
				if (!string.IsNullOrEmpty(szFallback))
				{
					return Regex.Replace(szFallback, "\\bDN(\\d+(\\.\\d+)?)", "D$1");
				}
				return szFallback ?? "";
			}
			Parameter nomParam = e.get_Parameter((BuiltInParameter)(-1140225)) ?? e.LookupParameter("Diameter") ?? e.LookupParameter("Đường kính");
			if (nomParam != null && nomParam.HasValue && (int)nomParam.StorageType == 2)
			{
				double nomVal = nomParam.AsDouble();
				if (nomVal > 0.0)
				{
					double nomMm = UnitUtils.ConvertFromInternalUnits(nomVal, UnitTypeId.Millimeters);
					double nomRounded = Math.Round(nomMm, 1);
					string nomStr = ((nomRounded % 1.0 == 0.0) ? ((int)nomRounded).ToString() : nomRounded.ToString());
					return "DN" + nomStr;
				}
			}
			return GetElementSize(e);
		}
		string origSize = GetElementSize(e);
		if (!string.IsNullOrEmpty(origSize) && isPprOrUpvc)
		{
			return Regex.Replace(origSize, "\\bDN(\\d+(\\.\\d+)?)", "D$1");
		}
		return origSize ?? GetElementSize(e);
	}

	private static string GetElementDiameterSize(Element e, bool isPprOrUpvc)
	{
		string originalSize = GetElementSize(e);
		if (string.IsNullOrEmpty(originalSize))
		{
			return originalSize;
		}
		if (isPprOrUpvc)
		{
			return Regex.Replace(originalSize, "\\bDN(\\d+(\\.\\d+)?)", "D$1");
		}
		return originalSize;
	}

	private static string GetLevel(Element e)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Invalid comparison between Unknown and I4
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Invalid comparison between Unknown and I4
		Parameter tangParam = e.LookupParameter("TANG");
		if (tangParam != null && tangParam.HasValue && (int)tangParam.StorageType == 3)
		{
			string val = tangParam.AsString();
			if (!string.IsNullOrEmpty(val))
			{
				return val;
			}
		}
		ElementId typeId = e.GetTypeId();
		if (typeId != ElementId.InvalidElementId)
		{
			Element typeElement = e.Document.GetElement(typeId);
			Parameter typeTangParam = ((typeElement != null) ? typeElement.LookupParameter("TANG") : null);
			if (typeTangParam != null && typeTangParam.HasValue && (int)typeTangParam.StorageType == 3)
			{
				string val2 = typeTangParam.AsString();
				if (!string.IsNullOrEmpty(val2))
				{
					return val2;
				}
			}
		}
		return "N/A";
	}

	private static double? GetParameterValue(Element e, BuiltInParameter bip)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		Parameter p = e.get_Parameter(bip);
		return (p != null && p.HasValue) ? new double?(p.AsDouble()) : ((double?)null);
	}

	private static ICellStyle CreateTitleStyle(IWorkbook workbook)
	{
		ICellStyle style = workbook.CreateCellStyle();
		IFont font = workbook.CreateFont();
		font.FontHeightInPoints = 16.0;
		font.IsBold = true;
		font.Color = IndexedColors.RoyalBlue.Index;
		style.SetFont(font);
		style.Alignment = HorizontalAlignment.Center;
		return style;
	}

	private static ICellStyle CreateHeaderStyle(IWorkbook workbook)
	{
		ICellStyle style = workbook.CreateCellStyle();
		IFont font = workbook.CreateFont();
		font.Color = IndexedColors.White.Index;
		font.IsBold = true;
		style.SetFont(font);
		style.FillForegroundColor = IndexedColors.RoyalBlue.Index;
		style.FillPattern = FillPattern.SolidForeground;
		style.BorderBottom = BorderStyle.Thin;
		style.BorderTop = BorderStyle.Thin;
		style.BorderLeft = BorderStyle.Thin;
		style.BorderRight = BorderStyle.Thin;
		style.Alignment = HorizontalAlignment.Center;
		return style;
	}

	private static ICellStyle CreateBodyStyle(IWorkbook workbook)
	{
		ICellStyle style = workbook.CreateCellStyle();
		style.BorderBottom = BorderStyle.Thin;
		style.BorderTop = BorderStyle.Thin;
		style.BorderLeft = BorderStyle.Thin;
		style.BorderRight = BorderStyle.Thin;
		return style;
	}

	private static ICellStyle CreateNumericStyle(IWorkbook workbook)
	{
		ICellStyle style = workbook.CreateCellStyle();
		style.BorderBottom = BorderStyle.Thin;
		style.BorderTop = BorderStyle.Thin;
		style.BorderLeft = BorderStyle.Thin;
		style.BorderRight = BorderStyle.Thin;
		style.DataFormat = workbook.CreateDataFormat().GetFormat("#,##0.00");
		style.Alignment = HorizontalAlignment.Right;
		return style;
	}

	private static string GetSystemTypeName(Element e, Document doc)
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Expected O, but got Unknown
		Parameter sysTypeParam = e.get_Parameter((BuiltInParameter)(-1140333)) ?? e.get_Parameter((BuiltInParameter)(-1140334));
		if (sysTypeParam != null)
		{
			string val = sysTypeParam.AsValueString();
			if (!string.IsNullOrEmpty(val))
			{
				return val;
			}
		}
		try
		{
			Document elemDoc = e.Document ?? doc;
			ConnectorSet connectors = null;
			FamilyInstance fi = (FamilyInstance)(object)((e is FamilyInstance) ? e : null);
			if (fi != null && fi.MEPModel != null)
			{
				ConnectorManager connectorManager = fi.MEPModel.ConnectorManager;
				connectors = ((connectorManager != null) ? connectorManager.Connectors : null);
			}
			else
			{
				MEPCurve curve = (MEPCurve)(object)((e is MEPCurve) ? e : null);
				if (curve != null)
				{
					ConnectorManager connectorManager2 = curve.ConnectorManager;
					connectors = ((connectorManager2 != null) ? connectorManager2.Connectors : null);
				}
			}
			if (connectors != null)
			{
				foreach (Connector item in connectors)
				{
					Connector conn = item;
					if (conn.MEPSystem != null)
					{
						Element sysType = elemDoc.GetElement(((Element)conn.MEPSystem).GetTypeId());
						if (sysType != null)
						{
							return sysType.Name;
						}
						return ((Element)conn.MEPSystem).Name;
					}
				}
			}
		}
		catch
		{
		}
		return "";
	}

	public static List<string> ExtractGasSizes(string typeName)
	{
		List<string> result = new List<string>();
		if (string.IsNullOrEmpty(typeName))
		{
			return result;
		}
		int øIdx = typeName.IndexOf("ø", StringComparison.OrdinalIgnoreCase);
		if (øIdx < 0)
		{
			øIdx = typeName.IndexOf("Ø", StringComparison.OrdinalIgnoreCase);
		}
		if (øIdx < 0)
		{
			return result;
		}
		string sub = typeName.Substring(øIdx + 1);
		int slashIdx = sub.IndexOf('/');
		if (slashIdx > 0)
		{
			string partA = sub.Substring(0, slashIdx).Trim();
			string partB = sub.Substring(slashIdx + 1);
			int endIdx = partB.IndexOfAny(new char[3] { ' ', '-', '_' });
			partB = ((endIdx <= 0) ? partB.Trim() : partB.Substring(0, endIdx).Trim());
			partA = CleanSizeNumber(partA);
			partB = CleanSizeNumber(partB);
			if (!string.IsNullOrEmpty(partA))
			{
				result.Add(partA);
			}
			if (!string.IsNullOrEmpty(partB))
			{
				result.Add(partB);
			}
		}
		return result;
	}

	private static string CleanSizeNumber(string val)
	{
		StringBuilder sb = new StringBuilder();
		foreach (char c in val)
		{
			if (char.IsDigit(c) || c == '.')
			{
				sb.Append(c);
				continue;
			}
			break;
		}
		return sb.ToString().Trim();
	}

	private static string ComputeHvacPipeNewType(string typeName, string angle)
	{
		string raw = typeName ?? "";
		string newType = raw;
		int dashIdx = newType.IndexOf('-');
		if (dashIdx > 0)
		{
			newType = newType.Substring(0, dashIdx).Trim();
		}
		if (raw.Length > 0 && raw[0] == 'Y' && angle == "90")
		{
			string modified = "Tê" + raw.Substring(1);
			int modDash = modified.IndexOf('-');
			newType = ((modDash > 0) ? modified.Substring(0, modDash).Trim() : modified.Trim());
		}
		int loiIdx = raw.IndexOf("Lơi", StringComparison.OrdinalIgnoreCase);
		if (loiIdx >= 0)
		{
			if (angle == "45")
			{
				newType = raw;
			}
			else if (angle == "90")
			{
				string modified2 = raw.Substring(0, loiIdx) + "Co" + raw.Substring(loiIdx + "Lơi".Length);
				int modDash2 = modified2.IndexOf('-');
				newType = ((modDash2 > 0) ? modified2.Substring(0, modDash2).Trim() : modified2.Trim());
			}
		}
		if (raw.IndexOf("Lơi", StringComparison.OrdinalIgnoreCase) < 0)
		{
			int coIdx = raw.IndexOf("Co", StringComparison.OrdinalIgnoreCase);
			if (coIdx >= 0)
			{
				if (angle == "90")
				{
					newType = raw;
				}
				else if (angle == "45")
				{
					string modified3 = raw.Substring(0, coIdx) + "Lơi" + raw.Substring(coIdx + "Co".Length);
					int modDash3 = modified3.IndexOf('-');
					newType = ((modDash3 > 0) ? modified3.Substring(0, modDash3).Trim() : modified3.Trim());
				}
			}
		}
		return newType;
	}

	private static string ComputeHvacPipeItemCode(string typeName, string angle, string size)
	{
		string raw = typeName ?? "";
		string newType = ComputeHvacPipeNewType(typeName, angle);
		bool startsWithY = raw.Length > 0 && raw[0] == 'Y';
		bool containsLoi = raw.IndexOf("Lơi", StringComparison.OrdinalIgnoreCase) >= 0;
		bool containsCo = !containsLoi && raw.IndexOf("Co", StringComparison.OrdinalIgnoreCase) >= 0;
		string dauMuc;
		if (startsWithY || containsLoi || containsCo)
		{
			dauMuc = newType;
			if (!string.IsNullOrEmpty(size))
			{
				dauMuc = dauMuc + " " + size;
			}
		}
		else
		{
			dauMuc = newType;
			if (angle == "45")
			{
				dauMuc += " 45";
			}
			if (!string.IsNullOrEmpty(size))
			{
				dauMuc = dauMuc + " " + size;
			}
		}
		return dauMuc.Trim();
	}

	private static void WriteHvacGasSheet(ISheet sheet, List<BOQExportItem> allItems, List<string> levels, BOQSettings settings, ICellStyle bodyStyle, ICellStyle numericStyle, ICellStyle headerStyle, int colIdxCategory, int colIdxType, int colIdxDauMuc, int colIdxUnit, int startLevelColIdx, int totalColIndex, ref int currentRow)
	{
		//IL_042a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0430: Invalid comparison between Unknown and I4
		//IL_03c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Expected O, but got Unknown
		//IL_08ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_08b4: Invalid comparison between Unknown and I4
		//IL_084d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0854: Expected O, but got Unknown
		List<BOQExportItem> hvacGasItems = allItems.Where((BOQExportItem x) => x.SubSystemOrder == 8).ToList();
		if (!hvacGasItems.Any())
		{
			return;
		}
		ICellStyle subHeaderStyle = sheet.Workbook.CreateCellStyle();
		IFont font = sheet.Workbook.CreateFont();
		font.IsBold = true;
		font.FontHeightInPoints = 11.0;
		font.Color = IndexedColors.Black.Index;
		subHeaderStyle.SetFont(font);
		subHeaderStyle.FillForegroundColor = IndexedColors.Grey25Percent.Index;
		subHeaderStyle.FillPattern = FillPattern.SolidForeground;
		IRow subHeaderRow = sheet.CreateRow(currentRow++);
		ICell subHeaderCell = subHeaderRow.CreateCell(0);
		subHeaderCell.SetCellValue("Gộp khối lượng của conduit và conduit fitting lại");
		subHeaderCell.CellStyle = subHeaderStyle;
		for (int c = 1; c <= totalColIndex; c++)
		{
			subHeaderRow.CreateCell(c).CellStyle = subHeaderStyle;
		}
		sheet.AddMergedRegion(new CellRangeAddress(currentRow - 1, currentRow - 1, 0, totalColIndex));
		double result;
		var groupedByType = (from x in hvacGasItems
			group x by new { x.TypeName, x.Size } into g
			orderby double.TryParse(g.Key.Size, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : 9999.0, g.Key.TypeName
			select g).ToList();
		int stt = 1;
		foreach (var group in groupedByType)
		{
			IRow row = sheet.CreateRow(currentRow++);
			row.CreateCell(0).SetCellValue(stt++);
			row.CreateCell(colIdxCategory).SetCellValue("Conduits & Fittings");
			row.CreateCell(colIdxType).SetCellValue(group.Key.TypeName);
			row.CreateCell(colIdxDauMuc).SetCellValue("Gas " + group.Key.TypeName);
			row.CreateCell(colIdxUnit).SetCellValue("m");
			double totalRowQty = 0.0;
			for (int i = 0; i < levels.Count; i++)
			{
				string currentLevelName = levels[i];
				List<BOQExportItem> levelItems = group.Where((BOQExportItem x) => x.Level == currentLevelName).ToList();
				if (levelItems.Any())
				{
					double levelQty = 0.0;
					List<BOQExportItem> conduits = levelItems.Where((BOQExportItem x) => x.Category.Id.GetIdInt() == -2008132).ToList();
					if (conduits.Any())
					{
						double rawLength = conduits.Sum((BOQExportItem x) => GetParameterValue(x.Element, (BuiltInParameter)(-1004005)).GetValueOrDefault());
						levelQty += UnitUtils.ConvertFromInternalUnits(rawLength, UnitTypeId.Meters);
					}
					List<BOQExportItem> fittings = levelItems.Where((BOQExportItem x) => x.Category.Id.GetIdInt() == -2008128).ToList();
					if (fittings.Any())
					{
						double fittingsLength = 0.0;
						foreach (BOQExportItem item in fittings)
						{
							Parameter p = item.Element.LookupParameter("Center to End") ?? item.Element.LookupParameter("Center To End");
							if (p == null)
							{
								foreach (Parameter parameter in item.Element.Parameters)
								{
									Parameter param = parameter;
									if (param.Definition != null && param.Definition.Name.Equals("Center to End", StringComparison.OrdinalIgnoreCase))
									{
										p = param;
										break;
									}
								}
							}
							double feetVal = ((p != null && (int)p.StorageType == 2) ? p.AsDouble() : 0.0);
							double metersVal = UnitUtils.ConvertFromInternalUnits(feetVal, UnitTypeId.Meters);
							fittingsLength += metersVal * 2.0;
						}
						levelQty += fittingsLength;
					}
					ICell qtyCell = row.CreateCell(startLevelColIdx + i);
					qtyCell.SetCellValue(levelQty);
					qtyCell.CellStyle = numericStyle;
					totalRowQty += levelQty;
				}
				else
				{
					ICell qtyCell2 = row.CreateCell(startLevelColIdx + i);
					qtyCell2.CellStyle = bodyStyle;
				}
			}
			ICell totalQtyCell = row.CreateCell(totalColIndex);
			totalQtyCell.SetCellValue(totalRowQty);
			totalQtyCell.CellStyle = numericStyle;
			for (int j = 0; j <= colIdxUnit; j++)
			{
				(row.GetCell(j) ?? row.CreateCell(j)).CellStyle = bodyStyle;
			}
		}
		sheet.CreateRow(currentRow++);
		IRow subHeaderRow2 = sheet.CreateRow(currentRow++);
		ICell subHeaderCell2 = subHeaderRow2.CreateCell(0);
		subHeaderCell2.SetCellValue("Xuất riêng cho conduit và conduit fitting");
		subHeaderCell2.CellStyle = subHeaderStyle;
		for (int k = 1; k <= totalColIndex; k++)
		{
			subHeaderRow2.CreateCell(k).CellStyle = subHeaderStyle;
		}
		sheet.AddMergedRegion(new CellRangeAddress(currentRow - 1, currentRow - 1, 0, totalColIndex));
		double result2;
		var separateGroups = (from x in hvacGasItems
			group x by new
			{
				TypeName = x.TypeName,
				Size = x.Size,
				Angle = x.Angle,
				IsElbow = x.IsElbow,
				CategoryId = x.Category.Id.GetIdInt()
			} into g
			orderby GetPipeCategoryOrder(g.Key.CategoryId), double.TryParse(g.Key.Size, NumberStyles.Any, CultureInfo.InvariantCulture, out result2) ? result2 : 9999.0, g.Key.TypeName
			select g).ToList();
		int stt2 = 1;
		foreach (var group2 in separateGroups)
		{
			IRow row2 = sheet.CreateRow(currentRow++);
			row2.CreateCell(0).SetCellValue(stt2++);
			string categoryName = ((group2.Key.CategoryId == -2008132) ? "Conduits" : "Conduit Fittings");
			row2.CreateCell(colIdxCategory).SetCellValue(categoryName);
			row2.CreateCell(colIdxType).SetCellValue(group2.Key.TypeName);
			row2.CreateCell(colIdxDauMuc).SetCellValue("Gas " + group2.Key.TypeName);
			row2.CreateCell(colIdxUnit).SetCellValue("m");
			double totalRowQty2 = 0.0;
			for (int l = 0; l < levels.Count; l++)
			{
				string currentLevelName2 = levels[l];
				List<BOQExportItem> levelItems2 = group2.Where((BOQExportItem x) => x.Level == currentLevelName2).ToList();
				if (levelItems2.Any())
				{
					double levelQty2 = 0.0;
					if (group2.Key.CategoryId == -2008128)
					{
						double fittingsLength2 = 0.0;
						foreach (BOQExportItem item2 in levelItems2)
						{
							Parameter p2 = item2.Element.LookupParameter("Center to End") ?? item2.Element.LookupParameter("Center To End");
							if (p2 == null)
							{
								foreach (Parameter parameter2 in item2.Element.Parameters)
								{
									Parameter param2 = parameter2;
									if (param2.Definition != null && param2.Definition.Name.Equals("Center to End", StringComparison.OrdinalIgnoreCase))
									{
										p2 = param2;
										break;
									}
								}
							}
							double feetVal2 = ((p2 != null && (int)p2.StorageType == 2) ? p2.AsDouble() : 0.0);
							double metersVal2 = UnitUtils.ConvertFromInternalUnits(feetVal2, UnitTypeId.Meters);
							fittingsLength2 += metersVal2 * 2.0;
						}
						levelQty2 = fittingsLength2;
					}
					else
					{
						double rawLength2 = levelItems2.Sum((BOQExportItem x) => GetParameterValue(x.Element, (BuiltInParameter)(-1004005)).GetValueOrDefault());
						levelQty2 = UnitUtils.ConvertFromInternalUnits(rawLength2, UnitTypeId.Meters);
					}
					ICell qtyCell3 = row2.CreateCell(startLevelColIdx + l);
					qtyCell3.SetCellValue(levelQty2);
					qtyCell3.CellStyle = numericStyle;
					totalRowQty2 += levelQty2;
				}
				else
				{
					ICell qtyCell4 = row2.CreateCell(startLevelColIdx + l);
					qtyCell4.CellStyle = bodyStyle;
				}
			}
			ICell totalQtyCell2 = row2.CreateCell(totalColIndex);
			totalQtyCell2.SetCellValue(totalRowQty2);
			totalQtyCell2.CellStyle = numericStyle;
			for (int m = 0; m <= colIdxUnit; m++)
			{
				(row2.GetCell(m) ?? row2.CreateCell(m)).CellStyle = bodyStyle;
			}
		}
	}
}
