using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class PipeInsulationCmd : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;

        try
        {
            List<InsulationTypeItem> insulationTypes = GetPipeInsulationTypes(doc);
            if (insulationTypes.Count == 0)
            {
                TaskDialog.Show("BIM | Pipe Insulation", "Không tìm thấy Pipe Insulation Type nào trong dự án.\nVui lòng load Pipe Insulation Type trước khi sử dụng tool.");
                return Result.Cancelled;
            }

            PipeInsulationWindow ui = new PipeInsulationWindow(insulationTypes, GetSystemOptionsInActiveView(doc));
            if (ui.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            ElementId insulationTypeId = ui.SelectedInsulationType.Tag as ElementId;
            if (insulationTypeId == null)
            {
                TaskDialog.Show("BIM | Pipe Insulation", "Insulation Type được chọn không hợp lệ.");
                return Result.Cancelled;
            }

            List<Element> pipeElements;
            List<Element> fittingElements;
            Result selectionResult = GetTargetElements(uidoc, doc, ui, out pipeElements, out fittingElements);
            if (selectionResult != Result.Succeeded)
            {
                return selectionResult;
            }

            List<PipeInsulationRule> systemRules = ui.Rules
                .Where(rule => rule != null && rule.MinDN > 0 && rule.MaxDN >= rule.MinDN && rule.ThicknessMM > 0)
                .ToList();
            int insulatedCount = 0;
            int skippedCount = 0;
            int removedCount = 0;

            using (Transaction transaction = new Transaction(doc, "BIN_PipeInsulation"))
            {
                bool transactionStarted = false;
                try
                {
                    transaction.Start();
                    transactionStarted = true;

                    FailureHandlingOptions failureOptions = transaction.GetFailureHandlingOptions();
                    failureOptions.SetClearAfterRollback(true);
                    transaction.SetFailureHandlingOptions(failureOptions);

                    if (ui.RemoveExisting)
                    {
                        removedCount = RemoveExistingInsulation(doc, pipeElements, fittingElements);
                        if (removedCount > 0)
                        {
                            doc.Regenerate();
                        }
                    }

                    foreach (Element element in pipeElements)
                    {
                        Pipe pipe = element as Pipe;
                        double thicknessMm = pipe == null ? 0.0 : PipeInsulationRules.GetThicknessMm(pipe, systemRules);
                        if (thicknessMm <= 0.0)
                        {
                            skippedCount++;
                            continue;
                        }

                        try
                        {
                            PipeInsulation.Create(doc, element.Id, insulationTypeId, thicknessMm / 304.8);
                            insulatedCount++;
                        }
                        catch
                        {
                            skippedCount++;
                        }
                    }

                    foreach (Element fitting in fittingElements)
                    {
                        double fittingSize = GetFittingSize(fitting);
                        double thicknessMm = PipeInsulationRules.GetFittingThicknessMm(fitting, fittingSize, systemRules);
                        if (thicknessMm <= 0.0)
                        {
                            skippedCount++;
                            continue;
                        }

                        try
                        {
                            PipeInsulation.Create(doc, fitting.Id, insulationTypeId, thicknessMm / 304.8);
                            insulatedCount++;
                        }
                        catch
                        {
                            skippedCount++;
                        }
                    }

                    transaction.Commit();
                    transactionStarted = false;
                }
                catch
                {
                    if (transactionStarted)
                    {
                        transaction.RollBack();
                    }
                    throw;
                }
            }

            ShowCompletionDialog(ui, pipeElements.Count, fittingElements.Count, insulatedCount, skippedCount, removedCount);
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }
        catch (Exception exception)
        {
            message = exception.Message;
            TaskDialog.Show("BIM TOOL | Error", exception.ToString());
            return Result.Failed;
        }
    }

    private static List<PipeInsulationSystemOption> GetSystemOptionsInActiveView(Document doc)
    {
        return new FilteredElementCollector(doc, doc.ActiveView.Id)
            .OfCategory(BuiltInCategory.OST_PipeCurves)
            .WhereElementIsNotElementType()
            .Cast<Pipe>()
            .Select(pipe => PipeInsulationRules.SystemName(pipe))
            .Where(systemName => !string.IsNullOrWhiteSpace(systemName))
            .GroupBy(systemName => systemName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PipeInsulationSystemOption
            {
                SystemName = group.First(),
                PipeCount = group.Count()
            })
            .OrderBy(option => option.SystemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ShowCompletionDialog(
        PipeInsulationWindow ui,
        int pipeCount,
        int fittingCount,
        int insulatedCount,
        int skippedCount,
        int removedCount)
    {
        TaskDialog dialog = new TaskDialog("BIM | Pipe Insulation")
        {
            MainInstruction = "Đã hoàn thành áp dụng insulation",
            MainContent = "Đã bọc insulation: " + insulatedCount + " phần tử\n" +
                          "Bỏ qua: " + skippedCount + " phần tử",
            ExpandedContent = "Phạm vi: " + ScopeDescription(ui) + "\n" +
                              "Preset: " + ui.SelectedPresetName + "\n" +
                              "Đối tượng trong phạm vi: " + pipeCount + " ống, " + fittingCount + " fitting" +
                              (ui.RemoveExisting ? "\nĐã xóa insulation cũ: " + removedCount : string.Empty),
            MainIcon = TaskDialogIcon.TaskDialogIconInformation,
            CommonButtons = TaskDialogCommonButtons.Ok
        };
        dialog.Show();
    }

    private static Result GetTargetElements(
        UIDocument uidoc,
        Document doc,
        PipeInsulationWindow ui,
        out List<Element> pipeElements,
        out List<Element> fittingElements)
    {
        pipeElements = new List<Element>();
        fittingElements = new List<Element>();

        if (ui.SelectedScope == PipeInsulationScope.AllInView)
        {
            pipeElements = CollectElementsInActiveView(doc, BuiltInCategory.OST_PipeCurves);
            fittingElements = CollectElementsInActiveView(doc, BuiltInCategory.OST_PipeFitting);
        }
        else if (ui.SelectedScope == PipeInsulationScope.SelectedPipes)
        {
            AddSelectedTargets(doc, uidoc.Selection.GetElementIds(), pipeElements, fittingElements);
            if (pipeElements.Count == 0 && fittingElements.Count == 0)
            {
                IList<Reference> pickedReferences = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new PipeOrFittingSelectionFilter(),
                    "Chọn Pipe hoặc Pipe Fitting để bọc insulation");
                AddSelectedTargets(doc, pickedReferences.Select(reference => reference.ElementId), pipeElements, fittingElements);
            }
        }
        else
        {
            foreach (Element pipe in CollectElementsInActiveView(doc, BuiltInCategory.OST_PipeCurves))
            {
                Pipe typedPipe = pipe as Pipe;
                if (typedPipe != null && string.Equals(
                    PipeInsulationRules.SystemName(typedPipe),
                    ui.SelectedSystemType,
                    StringComparison.OrdinalIgnoreCase))
                {
                    pipeElements.Add(pipe);
                }
            }

            foreach (Element fitting in CollectElementsInActiveView(doc, BuiltInCategory.OST_PipeFitting))
            {
                if (string.Equals(
                    PipeInsulationRules.FittingSystemName(fitting),
                    ui.SelectedSystemType,
                    StringComparison.OrdinalIgnoreCase))
                {
                    fittingElements.Add(fitting);
                }
            }

            if (pipeElements.Count == 0 && fittingElements.Count == 0)
            {
                TaskDialog.Show(
                    "BIM | Pipe Insulation",
                    "Không có Pipe hoặc Pipe Fitting thuộc System Type '" + ui.SelectedSystemType + "' trong view hiện tại.");
                return Result.Cancelled;
            }
        }

        if (pipeElements.Count == 0 && fittingElements.Count == 0)
        {
            TaskDialog.Show("BIM | Pipe Insulation", "Không có Pipe hoặc Pipe Fitting nào trong phạm vi đã chọn.");
            return Result.Cancelled;
        }

        return Result.Succeeded;
    }

    private static List<Element> CollectElementsInActiveView(Document doc, BuiltInCategory category)
    {
        return new FilteredElementCollector(doc, doc.ActiveView.Id)
            .OfCategory(category)
            .WhereElementIsNotElementType()
            .ToElements()
            .ToList();
    }

    private static void AddSelectedTargets(
        Document doc,
        IEnumerable<ElementId> selectedIds,
        ICollection<Element> pipeElements,
        ICollection<Element> fittingElements)
    {
        foreach (ElementId selectedId in selectedIds)
        {
            Element element = doc.GetElement(selectedId);
            if (element is Pipe)
            {
                pipeElements.Add(element);
            }
            else if (IsPipeFitting(element))
            {
                fittingElements.Add(element);
            }
        }
    }

    private static bool IsPipeFitting(Element element)
    {
        return element != null && element.Category != null &&
               element.Category.Id.Equals(new ElementId((int)BuiltInCategory.OST_PipeFitting));
    }

    private static string ScopeDescription(PipeInsulationWindow ui)
    {
        switch (ui.SelectedScope)
        {
            case PipeInsulationScope.AllInView:
                return "Tất cả đường ống trong View";
            case PipeInsulationScope.SelectedPipes:
                return "Đường ống đang chọn";
            default:
                return "Theo System Type: " + ui.SelectedSystemType;
        }
    }

    private static List<InsulationTypeItem> GetPipeInsulationTypes(Document doc)
    {
        List<InsulationTypeItem> result = new List<InsulationTypeItem>();
        foreach (Element element in new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_PipeInsulations)
            .WhereElementIsElementType())
        {
            result.Add(new InsulationTypeItem { Name = element.Name, Tag = element.Id });
        }
        return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static double GetFittingSize(Element fitting)
    {
        FamilyInstance familyInstance = fitting as FamilyInstance;
        ConnectorManager connectorManager = familyInstance == null || familyInstance.MEPModel == null
            ? null
            : familyInstance.MEPModel.ConnectorManager;
        if (connectorManager != null)
        {
            double maxSize = 0.0;
            foreach (Connector connector in connectorManager.Connectors)
            {
                if ((int)connector.Shape == 0)
                {
                    double diameter = connector.Radius * 2.0;
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

        Parameter nominalDiameter = fitting.LookupParameter("Nominal Diameter");
        if (nominalDiameter != null && nominalDiameter.StorageType == StorageType.Double)
        {
            return nominalDiameter.AsDouble();
        }

        Parameter size = fitting.LookupParameter("Size");
        string sizeText = size == null ? null : size.AsString();
        if (!string.IsNullOrEmpty(sizeText))
        {
            string number = Regex.Replace(sizeText, "[^\\d.]", string.Empty);
            double sizeMm;
            if (double.TryParse(number, out sizeMm))
            {
                return sizeMm / 304.8;
            }
        }

        return 0.0;
    }

    private static int RemoveExistingInsulation(Document doc, IEnumerable<Element> pipes, IEnumerable<Element> fittings)
    {
        HashSet<ElementId> targetIds = new HashSet<ElementId>(pipes.Select(pipe => pipe.Id));
        targetIds.UnionWith(fittings.Select(fitting => fitting.Id));
        List<ElementId> insulationIds = new List<ElementId>();

        foreach (Element element in new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_PipeInsulations)
            .WhereElementIsNotElementType())
        {
            PipeInsulation insulation = element as PipeInsulation;
            if (insulation != null && targetIds.Contains(insulation.HostElementId))
            {
                insulationIds.Add(insulation.Id);
            }
        }

        int removedCount = 0;
        foreach (ElementId insulationId in insulationIds)
        {
            try
            {
                doc.Delete(insulationId);
                removedCount++;
            }
            catch
            {
                // Continue applying insulation to the remaining independent hosts.
            }
        }
        return removedCount;
    }

    private sealed class PipeOrFittingSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            return element is Pipe || IsPipeFitting(element);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
