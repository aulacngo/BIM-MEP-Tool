using FillPattern = NPOI.SS.UserModel.FillPattern;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using Autodesk.Revit.DB;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace BIN;

public static class ExcelExporter
{
	public static void Export(List<ViewSchedule> schedules, string folderPath, bool isSingleFile)
	{
		try
		{
			if (isSingleFile)
			{
				string fileName = "Project_Schedules.xlsx";
				if (schedules.Count == 1)
				{
					fileName = CleanFileName(((Element)schedules[0]).Name) + ".xlsx";
				}
				string fullPath = Path.Combine(folderPath, fileName);
				IWorkbook workbook = new XSSFWorkbook();
				HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (ViewSchedule s in schedules)
				{
					string baseName = CleanSheetName(((Element)s).Name);
					string uniqueName = baseName;
					int suffix = 1;
					while (usedNames.Contains(uniqueName))
					{
						string suffixStr = $"({suffix})";
						int maxLen = 31 - suffixStr.Length;
						uniqueName = ((baseName.Length > maxLen) ? baseName.Substring(0, maxLen) : baseName) + suffixStr;
						suffix++;
					}
					usedNames.Add(uniqueName);
					ISheet sheet = workbook.CreateSheet(uniqueName);
					FillData(sheet, s);
				}
				using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
				{
					workbook.Write(fs);
				}
				Process.Start(new ProcessStartInfo(fullPath)
				{
					UseShellExecute = true
				});
				return;
			}
			foreach (ViewSchedule s2 in schedules)
			{
				string fullPath2 = Path.Combine(folderPath, CleanFileName(((Element)s2).Name) + ".xlsx");
				IWorkbook workbook2 = new XSSFWorkbook();
				ISheet sheet2 = workbook2.CreateSheet("Data");
				FillData(sheet2, s2);
				using FileStream fs2 = new FileStream(fullPath2, FileMode.Create, FileAccess.Write);
				workbook2.Write(fs2);
			}
			Process.Start(new ProcessStartInfo(folderPath)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("Export failed: " + ex.Message);
		}
	}

	private static void FillData(ISheet sheet, ViewSchedule vs)
	{
		IFont headerFont = sheet.Workbook.CreateFont();
		headerFont.IsBold = true;
		headerFont.Color = IndexedColors.White.Index;
		ICellStyle headerStyle = sheet.Workbook.CreateCellStyle();
		headerStyle.SetFont(headerFont);
		headerStyle.FillForegroundColor = IndexedColors.RoyalBlue.Index;
		headerStyle.FillPattern = FillPattern.SolidForeground;
		ScheduleDefinition definition = vs.Definition;
		IList<ScheduleFieldId> fieldOrder = definition.GetFieldOrder();
		IRow headerRow = sheet.CreateRow(0);
		int excelCol = 0;
		for (int i = 0; i < fieldOrder.Count; i++)
		{
			ScheduleField field = definition.GetField(fieldOrder[i]);
			if (!field.IsHidden)
			{
				ICell cell = headerRow.CreateCell(excelCol);
				cell.SetCellValue(field.ColumnHeading);
				cell.CellStyle = headerStyle;
				excelCol++;
			}
		}
		TableSectionData body = vs.GetTableData().GetSectionData((SectionType)1);
		int rows = body.NumberOfRows;
		int cols = body.NumberOfColumns;
		for (int r = 0; r < rows; r++)
		{
			IRow row = sheet.CreateRow(r + 1);
			for (int c = 0; c < excelCol; c++)
			{
				ICell cell2 = row.CreateCell(c);
				cell2.SetCellValue(((TableView)vs).GetCellText((SectionType)1, r, c));
			}
		}
		for (int j = 0; j < excelCol; j++)
		{
			sheet.AutoSizeColumn(j);
		}
	}

	private static string CleanSheetName(string name)
	{
		string invalidChars = "/\\?*[]:";
		string cleaned = Regex.Replace(name, "[" + Regex.Escape(invalidChars) + "]", "_");
		return (cleaned.Length > 31) ? cleaned.Substring(0, 31) : cleaned;
	}

	private static string CleanFileName(string name)
	{
		string invalidChars = "/\\?*:|\"<>+";
		return Regex.Replace(name, "[" + Regex.Escape(invalidChars) + "]", "_");
	}
}
