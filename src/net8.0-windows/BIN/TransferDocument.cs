using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class TransferDocument : IExternalCommand
{
	private StringBuilder _log = new StringBuilder();

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_02e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Expected O, but got Unknown
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0140: Expected O, but got Unknown
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b8: Unknown result type (might be due to invalid IL or missing references)
		UIApplication uiApp = commandData.Application;
		Document sourceDoc = uiApp.ActiveUIDocument.Document;
		try
		{
			List<Document> openDocuments = (from Document doc in (IEnumerable)uiApp.Application.Documents
				where !doc.IsLinked && doc.IsValidObject && doc.PathName != sourceDoc.PathName
				select doc).ToList();
			if (!openDocuments.Any())
			{
				MessageBox.Show("Vui l\ufffdng m? file d\ufffdch tru?c khi s? d?ng tool n\ufffdy!", "Th\ufffdng b\ufffdo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return Result.Cancelled;
			}
			TransferDocumentWindow window = new TransferDocumentWindow(uiApp, sourceDoc, openDocuments);
			if (window.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			Document destDoc = window.SelectedDestinationDocument;
			TransferDocumentType selectedType = window.SelectedElementType;
			List<ElementId> selectedIds = window.SelectedElementIds;
			if (destDoc == null)
			{
				MessageBox.Show("Vui l\ufffdng ch?n file d\ufffdch!", "L?i", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return Result.Cancelled;
			}
			if (selectedIds.Count == 0)
			{
				MessageBox.Show("Vui l\ufffdng ch?n \ufffdt nh?t m?t element d? copy!", "L?i", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return Result.Cancelled;
			}
			_log.Clear();
			int successCount = 0;
			List<string> failedItems = new List<string>();
			TransactionGroup transGroup = new TransactionGroup(destDoc, "Copy 2D Elements");
			try
			{
				transGroup.Start();
				CopyPasteOptions options = new CopyPasteOptions();
				options.SetDuplicateTypeNamesHandler((IDuplicateTypeNamesHandler)(object)new TransferDocumentDuplicateTypesHandler());
				switch (selectedType)
				{
				case TransferDocumentType.Legend:
					CopyLegends(sourceDoc, destDoc, selectedIds, options, ref successCount, failedItems);
					break;
				case TransferDocumentType.ViewTemplate:
					CopyViewTemplates(sourceDoc, destDoc, selectedIds, options, ref successCount, failedItems);
					break;
				case TransferDocumentType.DraftingView:
					CopyDraftingViews(sourceDoc, destDoc, selectedIds, options, ref successCount, failedItems);
					break;
				case TransferDocumentType.Schedule:
					CopySchedules(sourceDoc, destDoc, selectedIds, options, ref successCount, failedItems);
					break;
				case TransferDocumentType.Sheet:
					CopySheets(sourceDoc, destDoc, selectedIds, options, ref successCount, failedItems);
					break;
				case TransferDocumentType.Filter:
					CopyFilters(sourceDoc, destDoc, selectedIds, options, ref successCount, failedItems);
					break;
				case TransferDocumentType.View:
					CopyViews(sourceDoc, destDoc, selectedIds, options, ref successCount, failedItems);
					break;
				}
				transGroup.Assimilate();
			}
			finally
			{
				((IDisposable)transGroup)?.Dispose();
			}
			string resultMessage = $"\ufffd\ufffd copy th\ufffdnh c\ufffdng {successCount}/{selectedIds.Count} {selectedType}(s) sang file d\ufffdch.";
			if (failedItems.Count > 0)
			{
				resultMessage = resultMessage + "\n\nC\ufffdc items kh\ufffdng th? copy:\n- " + string.Join("\n- ", failedItems);
			}
			MessageBox.Show(resultMessage, "Ho\ufffdn th\ufffdnh", MessageBoxButton.OK, (failedItems.Count > 0) ? MessageBoxImage.Exclamation : MessageBoxImage.Asterisk);
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			MessageBox.Show("L?i: " + ex.Message, "L?i", MessageBoxButton.OK, MessageBoxImage.Hand);
			return Result.Failed;
		}
	}

	private void CopyLegends(Document sourceDoc, Document destDoc, List<ElementId> legendIds, CopyPasteOptions options, ref int successCount, List<string> failedItems)
	{
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Invalid comparison between Unknown and I4
		foreach (ElementId legendId in legendIds)
		{
			try
			{
				Element element = sourceDoc.GetElement(legendId);
				View sourceLegendView = (View)(object)((element is View) ? element : null);
				string legendName = ((sourceLegendView != null) ? ((Element)sourceLegendView).Name : null) ?? "Unknown";
				Transaction trans = new Transaction(destDoc, "Copy Legend: " + legendName);
				try
				{
					trans.Start();
					List<ElementId> elementsInView = GetAllElementsInView(sourceDoc, sourceLegendView);
					ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { legendId }, destDoc, Transform.Identity, options);
					if (copiedIds != null && copiedIds.Count > 0)
					{
						View newLegendView = null;
						foreach (ElementId copiedId in copiedIds)
						{
							Element elem = destDoc.GetElement(copiedId);
							View v = (View)(object)((elem is View) ? elem : null);
							if (v != null && (int)v.ViewType == 11)
							{
								newLegendView = v;
								break;
							}
						}
						if (newLegendView != null && elementsInView.Count > 0)
						{
							List<ElementId> elementsInNewView = GetAllElementsInView(destDoc, newLegendView);
							if (elementsInNewView.Count < elementsInView.Count)
							{
								try
								{
									ElementTransformUtils.CopyElements(sourceLegendView, (ICollection<ElementId>)elementsInView, newLegendView, Transform.Identity, options);
								}
								catch
								{
								}
							}
						}
						successCount++;
					}
					else
					{
						failedItems.Add(legendName);
					}
					trans.Commit();
				}
				finally
				{
					((IDisposable)trans)?.Dispose();
				}
			}
			catch (Exception)
			{
				Element element2 = sourceDoc.GetElement(legendId);
				View legendView = (View)(object)((element2 is View) ? element2 : null);
				failedItems.Add(((legendView != null) ? ((Element)legendView).Name : null) ?? "Unknown");
			}
		}
	}

	private void CopyViewTemplates(Document sourceDoc, Document destDoc, List<ElementId> templateIds, CopyPasteOptions options, ref int successCount, List<string> failedItems)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Expected O, but got Unknown
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0152: Unknown result type (might be due to invalid IL or missing references)
		List<string> existingNames = (from View v in (IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(View))
			where v.IsTemplate
			select ((Element)v).Name).ToList();
		foreach (ElementId templateId in templateIds)
		{
			Element element = sourceDoc.GetElement(templateId);
			View sourceTemplate = (View)(object)((element is View) ? element : null);
			string originalName = ((Element)sourceTemplate).Name;
			try
			{
				Transaction trans = new Transaction(destDoc, "Copy ViewTemplate: " + originalName);
				try
				{
					trans.Start();
					ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { templateId }, destDoc, Transform.Identity, options);
					if (copiedIds != null && copiedIds.Any())
					{
						Element element2 = destDoc.GetElement(copiedIds.First());
						View destTemplate = (View)(object)((element2 is View) ? element2 : null);
						if (existingNames.Contains(originalName))
						{
							((Element)destTemplate).Name = originalName + "_Copy_" + DateTime.Now.ToString("ss");
						}
						CopyViewParameters(sourceTemplate, destTemplate);
						successCount++;
					}
					else
					{
						failedItems.Add(originalName);
					}
					trans.Commit();
				}
				finally
				{
					((IDisposable)trans)?.Dispose();
				}
			}
			catch (Exception)
			{
				failedItems.Add(originalName);
			}
		}
	}

	private void CopyViewParameters(View source, View dest)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Expected I4, but got Unknown
		foreach (Parameter parameter in ((Element)source).Parameters)
		{
			Parameter sourceParam = parameter;
			if (((APIObject)sourceParam).IsReadOnly || !sourceParam.HasValue)
			{
				continue;
			}
			Parameter destParam = ((Element)dest).get_Parameter(sourceParam.Definition);
			if (destParam == null || ((APIObject)destParam).IsReadOnly)
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
					destParam.Set(sourceParam.AsDouble());
					break;
				case (StorageType)0:
					destParam.Set(sourceParam.AsInteger());
					break;
				case (StorageType)2:
					destParam.Set(sourceParam.AsString());
					break;
				case (StorageType)3:
					destParam.Set(sourceParam.AsElementId());
					break;
				}
			}
			catch
			{
			}
		}
	}

	private void CopyDraftingViews(Document sourceDoc, Document destDoc, List<ElementId> viewIds, CopyPasteOptions options, ref int successCount, List<string> failedItems)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		foreach (ElementId viewId in viewIds)
		{
			try
			{
				Element element = sourceDoc.GetElement(viewId);
				View sourceDraftingView = (View)(object)((element is View) ? element : null);
				string viewName = ((sourceDraftingView != null) ? ((Element)sourceDraftingView).Name : null) ?? "Unknown";
				Transaction trans = new Transaction(destDoc, "Copy Drafting View: " + viewName);
				try
				{
					trans.Start();
					ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { viewId }, destDoc, Transform.Identity, options);
					if (copiedIds != null && copiedIds.Count > 0)
					{
						successCount++;
					}
					else
					{
						failedItems.Add(viewName);
					}
					trans.Commit();
				}
				finally
				{
					((IDisposable)trans)?.Dispose();
				}
			}
			catch (Exception)
			{
				Element element2 = sourceDoc.GetElement(viewId);
				View draftingView = (View)(object)((element2 is View) ? element2 : null);
				failedItems.Add(((draftingView != null) ? ((Element)draftingView).Name : null) ?? "Unknown");
			}
		}
	}

	private void CopySchedules(Document sourceDoc, Document destDoc, List<ElementId> scheduleIds, CopyPasteOptions options, ref int successCount, List<string> failedItems)
	{
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		foreach (ElementId scheduleId in scheduleIds)
		{
			try
			{
				Element element = sourceDoc.GetElement(scheduleId);
				ViewSchedule schedule = (ViewSchedule)(object)((element is ViewSchedule) ? element : null);
				string scheduleName = ((schedule != null) ? ((Element)schedule).Name : null) ?? "Unknown";
				Transaction trans = new Transaction(destDoc, "Copy Schedule: " + scheduleName);
				try
				{
					trans.Start();
					ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { scheduleId }, destDoc, Transform.Identity, options);
					if (copiedIds != null && copiedIds.Count > 0)
					{
						successCount++;
					}
					else
					{
						failedItems.Add(scheduleName);
					}
					trans.Commit();
				}
				finally
				{
					((IDisposable)trans)?.Dispose();
				}
			}
			catch (Exception)
			{
				Element element2 = sourceDoc.GetElement(scheduleId);
				ViewSchedule schedule2 = (ViewSchedule)(object)((element2 is ViewSchedule) ? element2 : null);
				failedItems.Add(((schedule2 != null) ? ((Element)schedule2).Name : null) ?? "Unknown");
			}
		}
	}

	private void CopySheets(Document sourceDoc, Document destDoc, List<ElementId> sheetIds, CopyPasteOptions options, ref int successCount, List<string> failedItems)
	{
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0420: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0267: Unknown result type (might be due to invalid IL or missing references)
		ElementType sourceVpType = default(ElementType);
		foreach (ElementId sheetId in sheetIds)
		{
			Element element = sourceDoc.GetElement(sheetId);
			ViewSheet sourceSheet = (ViewSheet)(object)((element is ViewSheet) ? element : null);
			if (sourceSheet == null)
			{
				continue;
			}
			try
			{
				Transaction t = new Transaction(destDoc, "Copy Sheet: " + sourceSheet.SheetNumber);
				try
				{
					t.Start();
					ElementId destTbId = GetValidTitleBlockId(sourceDoc, destDoc, sourceSheet, options);
					if (destTbId == ElementId.InvalidElementId)
					{
						failedItems.Add(sourceSheet.SheetNumber + " (No TitleBlock)");
						t.RollBack();
						continue;
					}
					ViewSheet newSheet = ViewSheet.Create(destDoc, destTbId);
					try
					{
						((Element)newSheet).Name = ((Element)sourceSheet).Name;
					}
					catch
					{
					}
					string sNum = sourceSheet.SheetNumber;
					int i = 1;
					HashSet<string> existingNumbers = (from ViewSheet x in (IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(ViewSheet))
						select x.SheetNumber).ToHashSet();
					while (existingNumbers.Contains(sNum))
					{
						sNum = sourceSheet.SheetNumber + "-" + i++;
					}
					try
					{
						newSheet.SheetNumber = sNum;
					}
					catch
					{
					}
					CopyElementParameters((Element)(object)sourceSheet, (Element)(object)newSheet);
					CopyTitleBlockParameters(sourceDoc, sourceSheet, destDoc, newSheet);
					foreach (ElementId vpId in sourceSheet.GetAllViewports())
					{
						try
						{
							Element element2 = sourceDoc.GetElement(vpId);
							Viewport vp = (Viewport)(object)((element2 is Viewport) ? element2 : null);
							if (vp == null)
							{
								continue;
							}
							Element element3 = sourceDoc.GetElement(vp.ViewId);
							View sourceView = (View)(object)((element3 is View) ? element3 : null);
							if (sourceView == null)
							{
								continue;
							}
							ElementId newViewId = CopyViewToDestination(sourceDoc, destDoc, sourceView, options);
							if (!(newViewId != ElementId.InvalidElementId) || !Viewport.CanAddViewToSheet(destDoc, ((Element)newSheet).Id, newViewId))
							{
								continue;
							}
							Viewport newVp = Viewport.Create(destDoc, ((Element)newSheet).Id, newViewId, vp.GetBoxCenter());
							try
							{
								ElementId sourceVpTypeId = ((Element)vp).GetTypeId();
								if (sourceVpTypeId != ElementId.InvalidElementId)
								{
									ref ElementType reference = ref sourceVpType;
									Element element4 = sourceDoc.GetElement(sourceVpTypeId);
									reference = (ElementType)(object)((element4 is ElementType) ? element4 : null);
									if (sourceVpType != null)
									{
										ElementType destVpType = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(ElementType))).Cast<ElementType>().FirstOrDefault((ElementType et) => ((Element)et).Name == ((Element)sourceVpType).Name && et.FamilyName == sourceVpType.FamilyName);
										if (destVpType != null)
										{
											((Element)newVp).ChangeTypeId(((Element)destVpType).Id);
										}
									}
								}
							}
							catch
							{
							}
							try
							{
								Parameter srcDetail = ((Element)vp).get_Parameter((BuiltInParameter)(-1005201));
								Parameter destDetail = ((Element)newVp).get_Parameter((BuiltInParameter)(-1005201));
								if (srcDetail != null && destDetail != null && srcDetail.HasValue)
								{
									destDetail.Set(srcDetail.AsString());
								}
							}
							catch
							{
							}
							CopyElementParameters((Element)(object)vp, (Element)(object)newVp);
						}
						catch
						{
						}
					}
					foreach (ScheduleSheetInstance ssi in ((IEnumerable)new FilteredElementCollector(sourceDoc, ((Element)sourceSheet).Id).OfClass(typeof(ScheduleSheetInstance))).Cast<ScheduleSheetInstance>())
					{
						try
						{
							Element element5 = sourceDoc.GetElement(ssi.ScheduleId);
							ViewSchedule sourceSchedule = (ViewSchedule)(object)((element5 is ViewSchedule) ? element5 : null);
							if (sourceSchedule != null)
							{
								ICollection<ElementId> copiedSchedules = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { ((Element)sourceSchedule).Id }, destDoc, Transform.Identity, options);
								if (copiedSchedules != null && copiedSchedules.Any())
								{
									ScheduleSheetInstance.Create(destDoc, ((Element)newSheet).Id, copiedSchedules.First(), ssi.Point);
								}
							}
						}
						catch
						{
						}
					}
					CopySheetAnnotations(sourceDoc, sourceSheet, destDoc, newSheet, options);
					successCount++;
					t.Commit();
				}
				finally
				{
					((IDisposable)t)?.Dispose();
				}
			}
			catch (Exception)
			{
				failedItems.Add(sourceSheet.SheetNumber + " - " + ((Element)sourceSheet).Name);
			}
		}
	}

	private ElementId GetValidTitleBlockId(Document sourceDoc, Document destDoc, ViewSheet sourceSheet, CopyPasteOptions opt)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		FamilyInstance sourceTbInstance = ((IEnumerable)new FilteredElementCollector(sourceDoc, ((Element)sourceSheet).Id).OfCategory((BuiltInCategory)(-2000280)).OfClass(typeof(FamilyInstance))).Cast<FamilyInstance>().FirstOrDefault();
		if (sourceTbInstance != null)
		{
			FamilySymbol sourceTbType = sourceTbInstance.Symbol;
			if (sourceTbType != null)
			{
				FamilySymbol destTbType = ((IEnumerable)new FilteredElementCollector(destDoc).OfCategory((BuiltInCategory)(-2000280)).OfClass(typeof(FamilySymbol))).Cast<FamilySymbol>().FirstOrDefault((FamilySymbol x) => ((Element)x).Name == ((Element)sourceTbType).Name && ((ElementType)x).FamilyName == ((ElementType)sourceTbType).FamilyName);
				if (destTbType != null)
				{
					if (!destTbType.IsActive)
					{
						destTbType.Activate();
						destDoc.Regenerate();
					}
					return ((Element)destTbType).Id;
				}
				try
				{
					ICollection<ElementId> copied = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { ((Element)sourceTbType).Id }, destDoc, Transform.Identity, opt);
					if (copied != null && copied.Any())
					{
						foreach (ElementId cId in copied)
						{
							Element element = destDoc.GetElement(cId);
							FamilySymbol fs = (FamilySymbol)(object)((element is FamilySymbol) ? element : null);
							if (fs != null)
							{
								if (!fs.IsActive)
								{
									fs.Activate();
									destDoc.Regenerate();
								}
								return ((Element)fs).Id;
							}
						}
						return copied.First();
					}
				}
				catch
				{
				}
			}
		}
		FamilySymbol anyTb = ((IEnumerable)new FilteredElementCollector(destDoc).OfCategory((BuiltInCategory)(-2000280)).OfClass(typeof(FamilySymbol))).Cast<FamilySymbol>().FirstOrDefault();
		if (anyTb != null)
		{
			if (!anyTb.IsActive)
			{
				anyTb.Activate();
				destDoc.Regenerate();
			}
			return ((Element)anyTb).Id;
		}
		return ElementId.InvalidElementId;
	}

	private ElementId CopyViewToDestination(Document sourceDoc, Document destDoc, View sourceView, CopyPasteOptions opt)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Invalid comparison between Unknown and I4
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Invalid comparison between Unknown and I4
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_025d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Invalid comparison between Unknown and I4
		try
		{
			ViewType vType = sourceView.ViewType;
			if ((int)vType == 11)
			{
				View existing = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(View))).Cast<View>().FirstOrDefault((View v) => (int)v.ViewType == 11 && ((Element)v).Name == ((Element)sourceView).Name);
				if (existing != null)
				{
					return ((Element)existing).Id;
				}
				ICollection<ElementId> copied = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { ((Element)sourceView).Id }, destDoc, Transform.Identity, opt);
				if (copied != null && copied.Any())
				{
					Element element = destDoc.GetElement(copied.First());
					View newLegend = (View)(object)((element is View) ? element : null);
					if (newLegend != null)
					{
						List<ElementId> elementsInView = GetAllElementsInView(sourceDoc, sourceView);
						if (elementsInView.Count > 0)
						{
							try
							{
								ElementTransformUtils.CopyElements(sourceView, (ICollection<ElementId>)elementsInView, newLegend, Transform.Identity, opt);
							}
							catch
							{
							}
						}
					}
					return copied.First();
				}
			}
			else if ((int)vType == 10)
			{
				View existingDrafting = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(View))).Cast<View>().FirstOrDefault((View v) => (int)v.ViewType == 10 && ((Element)v).Name == ((Element)sourceView).Name);
				if (existingDrafting != null)
				{
					return ((Element)existingDrafting).Id;
				}
				ICollection<ElementId> copiedDrafting = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { ((Element)sourceView).Id }, destDoc, Transform.Identity, opt);
				if (copiedDrafting != null && copiedDrafting.Any())
				{
					foreach (ElementId cId in copiedDrafting)
					{
						Element cElem = destDoc.GetElement(cId);
						View cv = (View)(object)((cElem is View) ? cElem : null);
						if (cv != null && (int)cv.ViewType == 10)
						{
							return cId;
						}
					}
					return copiedDrafting.First();
				}
			}
			else
			{
				View existingView = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(View))).Cast<View>().FirstOrDefault((View v) => v.ViewType == vType && ((Element)v).Name == ((Element)sourceView).Name);
				if (existingView != null)
				{
					return ((Element)existingView).Id;
				}
			}
		}
		catch
		{
		}
		return ElementId.InvalidElementId;
	}

	private void CopyFilters(Document sourceDoc, Document destDoc, List<ElementId> filterIds, CopyPasteOptions options, ref int successCount, List<string> failedItems)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		List<string> existingNames = ((IEnumerable<Element>)new FilteredElementCollector(destDoc).OfClass(typeof(ParameterFilterElement))).Select((Element f) => f.Name).ToList();
		foreach (ElementId filterId in filterIds)
		{
			Element element = sourceDoc.GetElement(filterId);
			ParameterFilterElement sourceFilter = (ParameterFilterElement)(object)((element is ParameterFilterElement) ? element : null);
			string filterName = ((sourceFilter != null) ? ((Element)sourceFilter).Name : null) ?? "Unknown";
			try
			{
				Transaction t = new Transaction(destDoc, "Copy Filter");
				try
				{
					t.Start();
					ICollection<ElementId> copied = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { filterId }, destDoc, Transform.Identity, options);
					if (copied.Any())
					{
						Element newFilter = destDoc.GetElement(copied.First());
						if (existingNames.Contains(filterName))
						{
							newFilter.Name = filterName + "_Copy_" + DateTime.Now.ToString("ss");
						}
						successCount++;
					}
					else
					{
						failedItems.Add(filterName);
					}
					t.Commit();
				}
				finally
				{
					((IDisposable)t)?.Dispose();
				}
			}
			catch
			{
				failedItems.Add(filterName);
			}
		}
	}

	private List<ElementId> GetAllElementsInView(Document doc, View view)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_0246: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0354: Unknown result type (might be due to invalid IL or missing references)
		//IL_03db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0462: Unknown result type (might be due to invalid IL or missing references)
		//IL_04e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_056b: Unknown result type (might be due to invalid IL or missing references)
		HashSet<ElementId> elementIds = new HashSet<ElementId>();
		FilteredElementCollector collector = new FilteredElementCollector(doc, ((Element)view).Id);
		foreach (Element elem in collector)
		{
			if (!(elem.Id == ((Element)view).Id) && !(elem is View) && !(elem is Viewport) && !(elem is ElementType))
			{
				elementIds.Add(elem.Id);
			}
		}
		IEnumerable<ElementId> importInstances = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id in importInstances)
		{
			elementIds.Add(id);
		}
		IEnumerable<ElementId> imageElements = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2000560)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id2 in imageElements)
		{
			elementIds.Add(id2);
		}
		IEnumerable<ElementId> filledRegions = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(FilledRegion)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id3 in filledRegions)
		{
			elementIds.Add(id3);
		}
		IEnumerable<ElementId> textNotes = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(TextNote)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id4 in textNotes)
		{
			elementIds.Add(id4);
		}
		IEnumerable<ElementId> dimensions = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(Dimension)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id5 in dimensions)
		{
			elementIds.Add(id5);
		}
		IEnumerable<ElementId> detailCurves = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(CurveElement)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id6 in detailCurves)
		{
			elementIds.Add(id6);
		}
		IEnumerable<ElementId> groups = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(Group)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id7 in groups)
		{
			elementIds.Add(id7);
		}
		IEnumerable<ElementId> familyInstances = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id8 in familyInstances)
		{
			elementIds.Add(id8);
		}
		IEnumerable<ElementId> genericAnnotations = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2000150)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id9 in genericAnnotations)
		{
			elementIds.Add(id9);
		}
		IEnumerable<ElementId> spotDimensions = from e in (IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(SpotDimension)).WhereElementIsNotElementType()
			where e.OwnerViewId == ((Element)view).Id
			select e.Id;
		foreach (ElementId id10 in spotDimensions)
		{
			elementIds.Add(id10);
		}
		return elementIds.ToList();
	}

	private string GetUniqueViewName(Document doc, string baseName)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		HashSet<string> existingNames = new HashSet<string>(from View v in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(View))
			select ((Element)v).Name, StringComparer.OrdinalIgnoreCase);
		if (!existingNames.Contains(baseName))
		{
			return baseName;
		}
		int counter = 1;
		string newName;
		do
		{
			newName = $"{baseName} ({counter})";
			counter++;
		}
		while (existingNames.Contains(newName));
		return newName;
	}

	private void CopyTitleBlockParameters(Document sourceDoc, ViewSheet sourceSheet, Document destDoc, ViewSheet destSheet)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Expected O, but got Unknown
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Expected I4, but got Unknown
		try
		{
			FamilyInstance sourceTb = ((IEnumerable)new FilteredElementCollector(sourceDoc, ((Element)sourceSheet).Id).OfCategory((BuiltInCategory)(-2000280)).OfClass(typeof(FamilyInstance))).Cast<FamilyInstance>().FirstOrDefault();
			FamilyInstance destTb = ((IEnumerable)new FilteredElementCollector(destDoc, ((Element)destSheet).Id).OfCategory((BuiltInCategory)(-2000280)).OfClass(typeof(FamilyInstance))).Cast<FamilyInstance>().FirstOrDefault();
			if (sourceTb == null || destTb == null)
			{
				return;
			}
			foreach (Parameter parameter in ((Element)sourceTb).Parameters)
			{
				Parameter sourceParam = parameter;
				if (((APIObject)sourceParam).IsReadOnly || !sourceParam.HasValue)
				{
					continue;
				}
				try
				{
					Parameter destParam = ((Element)destTb).LookupParameter(sourceParam.Definition.Name);
					if (destParam == null || ((APIObject)destParam).IsReadOnly)
					{
						continue;
					}
					StorageType storageType = sourceParam.StorageType;
					StorageType val = storageType;
					switch (val - 1)
					{
					case (StorageType)1:
						destParam.Set(sourceParam.AsDouble());
						break;
					case (StorageType)0:
						destParam.Set(sourceParam.AsInteger());
						break;
					case (StorageType)2:
						if (!string.IsNullOrEmpty(sourceParam.AsString()))
						{
							destParam.Set(sourceParam.AsString());
						}
						break;
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private void CopyElementParameters(Element source, Element dest)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected I4, but got Unknown
		foreach (Parameter parameter in source.Parameters)
		{
			Parameter sourceParam = parameter;
			if (((APIObject)sourceParam).IsReadOnly || !sourceParam.HasValue)
			{
				continue;
			}
			try
			{
				Parameter destParam = dest.LookupParameter(sourceParam.Definition.Name);
				if (destParam == null || ((APIObject)destParam).IsReadOnly)
				{
					continue;
				}
				StorageType storageType = sourceParam.StorageType;
				StorageType val = storageType;
				switch (val - 1)
				{
				case (StorageType)1:
					destParam.Set(sourceParam.AsDouble());
					break;
				case (StorageType)0:
					destParam.Set(sourceParam.AsInteger());
					break;
				case (StorageType)2:
					if (!string.IsNullOrEmpty(sourceParam.AsString()))
					{
						destParam.Set(sourceParam.AsString());
					}
					break;
				}
			}
			catch
			{
			}
		}
	}

	private void CopySheetAnnotations(Document sourceDoc, ViewSheet sourceSheet, Document destDoc, ViewSheet destSheet, CopyPasteOptions options)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		try
		{
			List<ElementId> annotationIds = new List<ElementId>();
			FilteredElementCollector collector = new FilteredElementCollector(sourceDoc, ((Element)sourceSheet).Id);
			foreach (Element elem in collector)
			{
				if (!(elem is View) && !(elem is Viewport) && !(elem is ScheduleSheetInstance) && !(elem is ElementType) && (elem.Category == null || elem.Category.GetIdInt() != -2000280))
				{
					annotationIds.Add(elem.Id);
				}
			}
			if (annotationIds.Count > 0)
			{
				ElementTransformUtils.CopyElements((View)(object)sourceSheet, (ICollection<ElementId>)annotationIds, (View)(object)destSheet, Transform.Identity, options);
			}
		}
		catch
		{
		}
	}

	private void CopyViews(Document sourceDoc, Document destDoc, List<ElementId> viewIds, CopyPasteOptions options, ref int successCount, List<string> failedItems)
	{
		//IL_0457: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Invalid comparison between Unknown and I4
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Invalid comparison between Unknown and I4
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Invalid comparison between Unknown and I4
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Invalid comparison between Unknown and I4
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Invalid comparison between Unknown and I4
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Invalid comparison between Unknown and I4
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Invalid comparison between Unknown and I4
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Invalid comparison between Unknown and I4
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Invalid comparison between Unknown and I4
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Invalid comparison between Unknown and I4
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Invalid comparison between Unknown and I4
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_021c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Invalid comparison between Unknown and I4
		foreach (ElementId viewId in viewIds)
		{
			Element element = sourceDoc.GetElement(viewId);
			View sourceView = (View)(object)((element is View) ? element : null);
			if (sourceView == null)
			{
				continue;
			}
			string viewName = ((Element)sourceView).Name;
			try
			{
				Transaction t = new Transaction(destDoc, "Copy View: " + viewName);
				try
				{
					t.Start();
					View newView = null;
					if ((int)sourceView.ViewType == 1 || (int)sourceView.ViewType == 116 || (int)sourceView.ViewType == 2 || (int)sourceView.ViewType == 115)
					{
						ViewFamily vf = (ViewFamily)(((int)sourceView.ViewType == 1) ? 109 : (((int)sourceView.ViewType == 116) ? 110 : (((int)sourceView.ViewType == 2) ? 111 : 120)));
						ViewFamilyType vft = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(ViewFamilyType))).Cast<ViewFamilyType>().FirstOrDefault((ViewFamilyType x) => x.ViewFamily == vf);
						Level lvl = GetTargetLevel(sourceDoc, destDoc, sourceView.GenLevel);
						if (vft != null && lvl != null)
						{
							newView = (View)(object)ViewPlan.Create(destDoc, ((Element)vft).Id, ((Element)lvl).Id);
						}
					}
					else if ((int)sourceView.ViewType == 4)
					{
						ViewFamilyType vft2 = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(ViewFamilyType))).Cast<ViewFamilyType>().FirstOrDefault((ViewFamilyType x) => (int)x.ViewFamily == 102);
						if (vft2 != null)
						{
							newView = (View)(object)View3D.CreateIsometric(destDoc, ((Element)vft2).Id);
						}
					}
					else if ((int)sourceView.ViewType == 117 || (int)sourceView.ViewType == 3)
					{
						ViewFamily vf2 = (ViewFamily)(((int)sourceView.ViewType == 117) ? 112 : 114);
						ViewFamilyType vft3 = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(ViewFamilyType))).Cast<ViewFamilyType>().FirstOrDefault((ViewFamilyType x) => x.ViewFamily == vf2);
						if (vft3 != null && (int)sourceView.ViewType == 117)
						{
							newView = (View)(object)ViewSection.CreateSection(destDoc, ((Element)vft3).Id, sourceView.CropBox);
						}
					}
					if (newView == null)
					{
						failedItems.Add(viewName + " (Cannot create view type or find level)");
						t.RollBack();
						continue;
					}
					((Element)newView).Name = GetUniqueViewName(destDoc, viewName);
					if (sourceView.ViewTemplateId != ElementId.InvalidElementId)
					{
						ElementId destTemplateId = CopySingleTemplate(sourceDoc, destDoc, sourceView.ViewTemplateId, options);
						if (destTemplateId != ElementId.InvalidElementId)
						{
							newView.ViewTemplateId = destTemplateId;
						}
					}
					CopyElementParameters((Element)(object)sourceView, (Element)(object)newView);
					try
					{
						newView.CropBox = sourceView.CropBox;
						newView.CropBoxActive = sourceView.CropBoxActive;
						newView.CropBoxVisible = sourceView.CropBoxVisible;
					}
					catch
					{
					}
					try
					{
						View3D v3d = (View3D)(object)((sourceView is View3D) ? sourceView : null);
						if (v3d != null)
						{
							View3D nv3d = (View3D)(object)((newView is View3D) ? newView : null);
							if (nv3d != null)
							{
								nv3d.IsSectionBoxActive = v3d.IsSectionBoxActive;
								if (v3d.IsSectionBoxActive)
								{
									nv3d.SetSectionBox(v3d.GetSectionBox());
								}
							}
						}
					}
					catch
					{
					}
					try
					{
						List<ElementId> sourceFilters = sourceView.GetFilters().ToList();
						foreach (ElementId filterId in sourceFilters)
						{
							ICollection<ElementId> copiedFilters = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { filterId }, destDoc, Transform.Identity, options);
							if (copiedFilters != null && copiedFilters.Count > 0)
							{
								ElementId destFilterId = copiedFilters.First();
								newView.AddFilter(destFilterId);
								newView.SetFilterVisibility(destFilterId, sourceView.GetFilterVisibility(filterId));
								newView.SetFilterOverrides(destFilterId, sourceView.GetFilterOverrides(filterId));
							}
						}
					}
					catch
					{
					}
					List<ElementId> elementsInView = GetAllElementsInView(sourceDoc, sourceView);
					if (elementsInView.Count > 0)
					{
						try
						{
							ElementTransformUtils.CopyElements(sourceView, (ICollection<ElementId>)elementsInView, newView, Transform.Identity, options);
						}
						catch
						{
						}
					}
					successCount++;
					t.Commit();
				}
				finally
				{
					((IDisposable)t)?.Dispose();
				}
			}
			catch (Exception ex)
			{
				failedItems.Add(viewName + " (" + ex.Message + ")");
			}
		}
	}

	private Level GetTargetLevel(Document sourceDoc, Document destDoc, Level sourceLevel)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		if (sourceLevel == null)
		{
			return ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(Level))).Cast<Level>().FirstOrDefault();
		}
		Level targetLvl = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(Level))).Cast<Level>().FirstOrDefault((Level l) => ((Element)l).Name.Equals(((Element)sourceLevel).Name, StringComparison.OrdinalIgnoreCase));
		if (targetLvl != null)
		{
			return targetLvl;
		}
		targetLvl = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(Level))).Cast<Level>().FirstOrDefault((Level l) => Math.Abs(l.Elevation - sourceLevel.Elevation) < 0.001);
		if (targetLvl != null)
		{
			return targetLvl;
		}
		return (from Level l in (IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(Level))
			orderby Math.Abs(l.Elevation - sourceLevel.Elevation)
			select l).FirstOrDefault();
	}

	private ElementId CopySingleTemplate(Document sourceDoc, Document destDoc, ElementId templateId, CopyPasteOptions options)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Element element = sourceDoc.GetElement(templateId);
			View sourceTemplate = (View)(object)((element is View) ? element : null);
			if (sourceTemplate == null)
			{
				return ElementId.InvalidElementId;
			}
			string originalName = ((Element)sourceTemplate).Name;
			View existing = ((IEnumerable)new FilteredElementCollector(destDoc).OfClass(typeof(View))).Cast<View>().FirstOrDefault((View v) => v.IsTemplate && ((Element)v).Name == originalName);
			if (existing != null)
			{
				return ((Element)existing).Id;
			}
			ICollection<ElementId> copiedIds = ElementTransformUtils.CopyElements(sourceDoc, (ICollection<ElementId>)new List<ElementId> { templateId }, destDoc, Transform.Identity, options);
			if (copiedIds != null && copiedIds.Any())
			{
				Element element2 = destDoc.GetElement(copiedIds.First());
				View destTemplate = (View)(object)((element2 is View) ? element2 : null);
				CopyViewParameters(sourceTemplate, destTemplate);
				return ((Element)destTemplate).Id;
			}
		}
		catch
		{
		}
		return ElementId.InvalidElementId;
	}
}
