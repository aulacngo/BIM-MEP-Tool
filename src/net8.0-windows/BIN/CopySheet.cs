#define DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class CopySheet : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Expected O, but got Unknown
		//IL_00e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_029f: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		UIApplication uiapp = commandData.Application;
		UIDocument uidoc = uiapp.ActiveUIDocument;
		Document doc = uidoc.Document;
		IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
		List<ViewSheet> sheets = (from ViewSheet s in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(ViewSheet))
			orderby s.SheetNumber
			select s).ToList();
		if (sheets.Count == 0)
		{
			TaskDialog.Show("Copy Sheet", "No sheets found in the project.");
			return Result.Cancelled;
		}
		CopySheetWindow ui = new CopySheetWindow(sheets);
		WindowInteropHelper helper = new WindowInteropHelper(ui);
		helper.Owner = revitHandle;
		if (ui.ShowDialog() == true)
		{
			int mode = ui.SelectedModeIndex;
			int copyCount = ui.CopyCount;
			Transaction t = new Transaction(doc, "Copy Sheets");
			try
			{
				t.Start();
				try
				{
					SheetDuplicateOption option = (SheetDuplicateOption)0;
					switch (mode)
					{
					case 0:
						option = (SheetDuplicateOption)0;
						break;
					case 1:
						option = (SheetDuplicateOption)2;
						break;
					case 2:
						option = (SheetDuplicateOption)3;
						break;
					case 3:
						option = (SheetDuplicateOption)4;
						break;
					}
					HashSet<BuiltInParameter> excludeSheetParams = new HashSet<BuiltInParameter> { (BuiltInParameter)(-1007401) };
					int totalCopied = 0;
					foreach (ViewSheet sheet in ui.ResultSheets)
					{
						for (int i = 0; i < copyCount; i++)
						{
							try
							{
								ElementId newSheetId = sheet.Duplicate(option);
								Element element = doc.GetElement(newSheetId);
								ViewSheet newSheet = (ViewSheet)(object)((element is ViewSheet) ? element : null);
								if (newSheet != null)
								{
									CopyParameters((Element)(object)sheet, (Element)(object)newSheet, excludeSheetParams);
									FamilyInstance sourceTitleBlock = ((IEnumerable)new FilteredElementCollector(doc, ((Element)sheet).Id).OfCategory((BuiltInCategory)(-2000280)).OfClass(typeof(FamilyInstance))).Cast<FamilyInstance>().FirstOrDefault();
									FamilyInstance newTitleBlock = ((IEnumerable)new FilteredElementCollector(doc, ((Element)newSheet).Id).OfCategory((BuiltInCategory)(-2000280)).OfClass(typeof(FamilyInstance))).Cast<FamilyInstance>().FirstOrDefault();
									if (sourceTitleBlock != null && newTitleBlock != null)
									{
										CopyParameters((Element)(object)sourceTitleBlock, (Element)(object)newTitleBlock, new HashSet<BuiltInParameter>());
									}
									CopyLegendsAndSchedules(doc, sheet, newSheet);
									totalCopied++;
								}
							}
							catch (Exception ex)
							{
								Debug.WriteLine("Failed to duplicate sheet " + sheet.SheetNumber + ": " + ex.Message);
							}
						}
					}
					t.Commit();
					TaskDialog.Show("Copy Sheet", $"Successfully copied {totalCopied} sheets.");
					return Result.Succeeded;
				}
				catch (Exception ex2)
				{
					t.RollBack();
					message = ex2.Message;
					return Result.Failed;
				}
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
		}
		return Result.Cancelled;
	}

	private void CopyParameters(Element source, Element target, HashSet<BuiltInParameter> excludeParams)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Expected I4, but got Unknown
		foreach (Parameter parameter in source.Parameters)
		{
			Parameter sourceParam = parameter;
			if (((APIObject)sourceParam).IsReadOnly || !sourceParam.HasValue)
			{
				continue;
			}
			Definition definition = sourceParam.Definition;
			InternalDefinition internalDef = (InternalDefinition)(object)((definition is InternalDefinition) ? definition : null);
			if (internalDef != null && excludeParams.Contains(internalDef.BuiltInParameter))
			{
				continue;
			}
			Parameter targetParam = null;
			try
			{
				if (sourceParam.IsShared && sourceParam.GUID != Guid.Empty)
				{
					targetParam = target.get_Parameter(sourceParam.GUID);
				}
			}
			catch
			{
			}
			if (targetParam == null)
			{
				targetParam = target.get_Parameter(sourceParam.Definition);
			}
			if (targetParam == null && sourceParam.Definition != null)
			{
				targetParam = target.LookupParameter(sourceParam.Definition.Name);
			}
			if (targetParam == null || ((APIObject)targetParam).IsReadOnly)
			{
				continue;
			}
			try
			{
				StorageType storageType = sourceParam.StorageType;
				StorageType val = storageType;
				switch (val - 1)
				{
				case (StorageType)1:
					targetParam.Set(sourceParam.AsDouble());
					break;
				case 0:
					targetParam.Set(sourceParam.AsInteger());
					break;
				case (StorageType)2:
					targetParam.Set(sourceParam.AsString());
					break;
				case (StorageType)3:
					targetParam.Set(sourceParam.AsElementId());
					break;
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Failed to copy parameter " + sourceParam.Definition.Name + ": " + ex.Message);
			}
		}
	}

	private void CopyLegendsAndSchedules(Document doc, ViewSheet sourceSheet, ViewSheet targetSheet)
	{
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Invalid comparison between Unknown and I4
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		HashSet<ElementId> targetViewIds = (from id in targetSheet.GetAllViewports().Select(delegate(ElementId vpId)
			{
				Element element = doc.GetElement(vpId);
				Element obj = ((element is Viewport) ? element : null);
				return (obj != null) ? ((Viewport)obj).ViewId : null;
			})
			where id != (ElementId)null
			select id).ToHashSet();
		foreach (ElementId vpId2 in sourceSheet.GetAllViewports())
		{
			Element element2 = doc.GetElement(vpId2);
			Viewport vp = (Viewport)(object)((element2 is Viewport) ? element2 : null);
			if (vp == null)
			{
				continue;
			}
			Element element3 = doc.GetElement(vp.ViewId);
			View view = (View)(object)((element3 is View) ? element3 : null);
			if (view == null || (int)view.ViewType != 11 || targetViewIds.Contains(((Element)view).Id))
			{
				continue;
			}
			try
			{
				Viewport newVp = Viewport.Create(doc, ((Element)targetSheet).Id, ((Element)view).Id, vp.GetBoxCenter());
				if (newVp != null)
				{
					((Element)newVp).ChangeTypeId(((Element)vp).GetTypeId());
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Failed to copy legend " + ((Element)view).Name + ": " + ex.Message);
			}
		}
		HashSet<ElementId> targetScheduleIds = (from ScheduleSheetInstance s in (IEnumerable)new FilteredElementCollector(doc, ((Element)targetSheet).Id).OfClass(typeof(ScheduleSheetInstance))
			select s.ScheduleId).ToHashSet();
		List<ScheduleSheetInstance> sourceSchedules = ((IEnumerable)new FilteredElementCollector(doc, ((Element)sourceSheet).Id).OfClass(typeof(ScheduleSheetInstance))).Cast<ScheduleSheetInstance>().ToList();
		foreach (ScheduleSheetInstance schedInstance in sourceSchedules)
		{
			if (!(schedInstance.ScheduleId == ElementId.InvalidElementId) && !targetScheduleIds.Contains(schedInstance.ScheduleId))
			{
				try
				{
					ScheduleSheetInstance.Create(doc, ((Element)targetSheet).Id, schedInstance.ScheduleId, schedInstance.Point);
				}
				catch (Exception ex2)
				{
					Debug.WriteLine("Failed to copy schedule instance: " + ex2.Message);
				}
			}
		}
	}
}
