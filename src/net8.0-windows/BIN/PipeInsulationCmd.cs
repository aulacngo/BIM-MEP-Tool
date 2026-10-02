using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
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
            List<InsulationTypeItem> pipeInsulationTypes = GetPipeInsulationTypes(doc);
            List<InsulationTypeItem> ductInsulationTypes = GetDuctInsulationTypes(doc);
            if (pipeInsulationTypes.Count == 0 && ductInsulationTypes.Count == 0)
            {
                TaskDialog.Show(
                    "BIM | Pipe & Duct Insulation",
                    "Không tìm thấy Pipe Insulation Type hoặc Duct Insulation Type nào trong dự án.\n" +
                    "Vui lòng load ít nhất một insulation type trước khi sử dụng tool.");
                return Result.Cancelled;
            }

            PipeInsulationWindow ui = new PipeInsulationWindow(
                pipeInsulationTypes,
                ductInsulationTypes,
                GetPipeSystemOptionsInActiveView(doc),
                GetDuctSystemOptionsInActiveView(doc));
            if (ui.ShowDialog() != true)
            {
                return Result.Cancelled;
            }

            ElementId insulationTypeId = ui.SelectedInsulationType == null
                ? null
                : ui.SelectedInsulationType.Tag as ElementId;
            if (insulationTypeId == null)
            {
                string targetLabel = ui.SelectedMepTarget == MepTargetKind.Duct ? "Duct" : "Pipe";
                TaskDialog.Show(
                    "BIM | Pipe & Duct Insulation",
                    "Chưa chọn " + targetLabel + " Insulation Type hợp lệ. Vui lòng load type tương ứng vào dự án.");
                return Result.Cancelled;
            }

            return ui.SelectedMepTarget == MepTargetKind.Duct
                ? ApplyDuctInsulation(uidoc, doc, ui, insulationTypeId)
                : ApplyPipeInsulation(uidoc, doc, ui, insulationTypeId);
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

    private static Result ApplyPipeInsulation(
        UIDocument uidoc,
        Document doc,
        PipeInsulationWindow ui,
        ElementId insulationTypeId)
    {
        List<Element> pipeElements;
        List<Element> fittingElements;
        Result selectionResult = GetPipeTargetElements(uidoc, doc, ui, out pipeElements, out fittingElements);
        if (selectionResult != Result.Succeeded)
        {
            return selectionResult;
        }

        List<Element> teeFittings = new List<Element>();
        if (ui.SmoothTees)
        {
            teeFittings = fittingElements.Where(IsPipeTeeOrBranchFitting).ToList();
            fittingElements = fittingElements.Where(fitting => !IsPipeTeeOrBranchFitting(fitting)).ToList();
        }

        List<PipeInsulationRule> systemRules = ui.Rules
            .Where(rule => rule != null && rule.MinDN >= 0 && rule.MaxDN >= rule.MinDN && rule.ThicknessMM > 0)
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

                // This is the original smooth-tee behavior: remove tee insulation so
                // intersecting insulation solids do not create a square block at a tee.
                if (teeFittings.Count > 0)
                {
                    removedCount += RemoveExistingPipeInsulation(doc, new List<Element>(), teeFittings);
                }

                if (removedCount > 0)
                {
                    doc.Regenerate();
                }

                foreach (Element element in pipeElements)
                {
                    Pipe pipe = element as Pipe;
                    PipeInsulationRule matchingRule = pipe == null
                        ? null
                        : PipeInsulationRules.FindMatchingPipeRule(pipe, systemRules);
                    if (matchingRule == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    ElementId typeId = ResolveInsulationType(doc, matchingRule.InsulationTypeName, false) ?? insulationTypeId;
                    int removedForHost;
                    if (TryReplacePipeInsulation(
                        doc,
                        element,
                        typeId,
                        matchingRule.ThicknessMM,
                        ui.RemoveExisting,
                        out removedForHost))
                    {
                        insulatedCount++;
                        removedCount += removedForHost;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                foreach (Element fitting in fittingElements)
                {
                    double fittingSize = GetPipeFittingSize(fitting);
                    PipeInsulationRule matchingRule = PipeInsulationRules.FindMatchingPipeFittingRule(fitting, fittingSize, systemRules);
                    if (matchingRule == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    ElementId typeId = ResolveInsulationType(doc, matchingRule.InsulationTypeName, false) ?? insulationTypeId;
                    int removedForHost;
                    if (TryReplacePipeInsulation(
                        doc,
                        fitting,
                        typeId,
                        matchingRule.ThicknessMM,
                        ui.RemoveExisting,
                        out removedForHost))
                    {
                        insulatedCount++;
                        removedCount += removedForHost;
                    }
                    else
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

        ShowCompletionDialog(ui, "ống nước", pipeElements.Count, fittingElements.Count, insulatedCount, skippedCount, removedCount);
        return Result.Succeeded;
    }

    private static Result ApplyDuctInsulation(
        UIDocument uidoc,
        Document doc,
        PipeInsulationWindow ui,
        ElementId insulationTypeId)
    {
        List<Element> ductElements;
        List<Element> fittingElements;
        Result selectionResult = GetDuctTargetElements(uidoc, doc, ui, out ductElements, out fittingElements);
        if (selectionResult != Result.Succeeded)
        {
            return selectionResult;
        }

        List<Element> teeFittings = new List<Element>();
        if (ui.SmoothTees)
        {
            teeFittings = fittingElements.Where(IsDuctTeeOrBranchFitting).ToList();
            fittingElements = fittingElements.Where(fitting => !IsDuctTeeOrBranchFitting(fitting)).ToList();
        }

        List<PipeInsulationRule> systemRules = ui.Rules
            .Where(rule => rule != null && rule.MinDN >= 0 && rule.MaxDN >= rule.MinDN && rule.ThicknessMM > 0)
            .ToList();
        int insulatedCount = 0;
        int skippedCount = 0;
        int removedCount = 0;

        using (Transaction transaction = new Transaction(doc, "BIN_DuctInsulation"))
        {
            bool transactionStarted = false;
            try
            {
                transaction.Start();
                transactionStarted = true;

                FailureHandlingOptions failureOptions = transaction.GetFailureHandlingOptions();
                failureOptions.SetClearAfterRollback(true);
                transaction.SetFailureHandlingOptions(failureOptions);

                if (teeFittings.Count > 0)
                {
                    removedCount += RemoveExistingDuctInsulation(doc, new List<Element>(), teeFittings);
                }

                if (removedCount > 0)
                {
                    doc.Regenerate();
                }

                foreach (Element element in ductElements)
                {
                    Duct duct = element as Duct;
                    double ductSize = GetDuctSize(duct);
                    PipeInsulationRule matchingRule = duct == null
                        ? null
                        : PipeInsulationRules.FindMatchingDuctRule(duct, ductSize, systemRules);
                    if (matchingRule == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    ElementId typeId = ResolveInsulationType(doc, matchingRule.InsulationTypeName, true) ?? insulationTypeId;
                    int removedForHost;
                    if (TryReplaceDuctInsulation(
                        doc,
                        element,
                        typeId,
                        matchingRule.ThicknessMM,
                        ui.RemoveExisting,
                        out removedForHost))
                    {
                        insulatedCount++;
                        removedCount += removedForHost;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                foreach (Element fitting in fittingElements)
                {
                    double fittingSize = GetDuctFittingSize(fitting);
                    PipeInsulationRule matchingRule = PipeInsulationRules.FindMatchingDuctFittingRule(fitting, fittingSize, systemRules);
                    if (matchingRule == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    ElementId typeId = ResolveInsulationType(doc, matchingRule.InsulationTypeName, true) ?? insulationTypeId;
                    int removedForHost;
                    if (TryReplaceDuctInsulation(
                        doc,
                        fitting,
                        typeId,
                        matchingRule.ThicknessMM,
                        ui.RemoveExisting,
                        out removedForHost))
                    {
                        insulatedCount++;
                        removedCount += removedForHost;
                    }
                    else
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

        ShowCompletionDialog(ui, "ống gió", ductElements.Count, fittingElements.Count, insulatedCount, skippedCount, removedCount);
        return Result.Succeeded;
    }

    private static ElementId ResolveInsulationType(Document doc, string typeName, bool isDuct)
    {
        if (!IsSpecificInsulationTypeName(typeName))
        {
            return null;
        }

        IEnumerable<InsulationTypeItem> insulationTypes = isDuct
            ? GetDuctInsulationTypes(doc)
            : GetPipeInsulationTypes(doc);
        InsulationTypeItem matchingType = insulationTypes.FirstOrDefault(item =>
            string.Equals(item.Name, typeName, StringComparison.OrdinalIgnoreCase));
        return matchingType == null ? null : matchingType.Tag as ElementId;
    }

    private static bool IsSpecificInsulationTypeName(string typeName)
    {
        return !string.IsNullOrWhiteSpace(typeName) &&
               !string.Equals(typeName, "(Theo loại chính)", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(typeName, "(Mặc định)", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReplacePipeInsulation(
        Document doc,
        Element element,
        ElementId insulationTypeId,
        double thicknessMm,
        bool removeExisting,
        out int removedCount)
    {
        removedCount = 0;
        using (SubTransaction sub = new SubTransaction(doc))
        {
            try
            {
                sub.Start();
                if (removeExisting)
                {
                    ICollection<ElementId> oldInsulationIds = InsulationLiningBase.GetInsulationIds(doc, element.Id);
                    if (oldInsulationIds != null && oldInsulationIds.Count > 0)
                    {
                        doc.Delete(oldInsulationIds);
                        removedCount = oldInsulationIds.Count;
                    }
                }

                PipeInsulation.Create(doc, element.Id, insulationTypeId, thicknessMm / 304.8);
                sub.Commit();
                return true;
            }
            catch
            {
                try
                {
                    if (sub.GetStatus() == TransactionStatus.Started)
                    {
                        sub.RollBack();
                    }
                }
                catch
                {
                    // Preserve the original per-host failure as a skipped element.
                }

                removedCount = 0;
                return false;
            }
        }
    }

    private static bool TryReplaceDuctInsulation(
        Document doc,
        Element element,
        ElementId insulationTypeId,
        double thicknessMm,
        bool removeExisting,
        out int removedCount)
    {
        removedCount = 0;
        using (SubTransaction sub = new SubTransaction(doc))
        {
            try
            {
                sub.Start();
                if (removeExisting)
                {
                    ICollection<ElementId> oldInsulationIds = InsulationLiningBase.GetInsulationIds(doc, element.Id);
                    if (oldInsulationIds != null && oldInsulationIds.Count > 0)
                    {
                        doc.Delete(oldInsulationIds);
                        removedCount = oldInsulationIds.Count;
                    }
                }

                DuctInsulation.Create(doc, element.Id, insulationTypeId, thicknessMm / 304.8);
                sub.Commit();
                return true;
            }
            catch
            {
                try
                {
                    if (sub.GetStatus() == TransactionStatus.Started)
                    {
                        sub.RollBack();
                    }
                }
                catch
                {
                    // Preserve the original per-host failure as a skipped element.
                }

                removedCount = 0;
                return false;
            }
        }
    }

    private static List<PipeInsulationSystemOption> GetPipeSystemOptionsInActiveView(Document doc)
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

    private static List<PipeInsulationSystemOption> GetDuctSystemOptionsInActiveView(Document doc)
    {
        List<string> systemNames = new List<string>();
        foreach (Element element in CollectElementsInActiveView(doc, BuiltInCategory.OST_DuctCurves))
        {
            Duct duct = element as Duct;
            if (duct != null)
            {
                systemNames.Add(PipeInsulationRules.DuctSystemName(duct));
            }
        }

        foreach (Element fitting in CollectElementsInActiveView(doc, BuiltInCategory.OST_DuctFitting))
        {
            systemNames.Add(PipeInsulationRules.DuctFittingSystemName(fitting));
        }

        return systemNames
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
        string hostLabel,
        int hostCount,
        int fittingCount,
        int insulatedCount,
        int skippedCount,
        int removedCount)
    {
        TaskDialog dialog = new TaskDialog("BIM | Pipe & Duct Insulation")
        {
            MainInstruction = "Đã hoàn thành áp dụng insulation",
            MainContent = "Đã bọc insulation cho " + hostCount + " " + hostLabel + ", " + fittingCount + " fitting.\n" +
                          "Tạo mới: " + insulatedCount + " phần tử; bỏ qua: " + skippedCount + " phần tử.",
            ExpandedContent = "Phạm vi: " + ScopeDescription(ui) + "\n" +
                              "Preset: " + ui.SelectedPresetName +
                              ((ui.RemoveExisting || removedCount > 0) ? "\nĐã xóa insulation cũ: " + removedCount : string.Empty) +
                              (ui.SmoothTees ? "\nChế độ làm mượt ngã ba chữ T: ĐÃ BẬT." : string.Empty),
            MainIcon = TaskDialogIcon.TaskDialogIconInformation,
            CommonButtons = TaskDialogCommonButtons.Ok
        };
        dialog.Show();
    }

    private static Result GetPipeTargetElements(
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
            AddSelectedPipeTargets(doc, uidoc.Selection.GetElementIds(), pipeElements, fittingElements);
            if (pipeElements.Count == 0 && fittingElements.Count == 0)
            {
                IList<Reference> pickedReferences = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new PipeOrFittingSelectionFilter(),
                    "Chọn Pipe hoặc Pipe Fitting để bọc insulation");
                AddSelectedPipeTargets(doc, pickedReferences.Select(reference => reference.ElementId), pipeElements, fittingElements);
            }
        }
        else
        {
            pipeElements = CollectElementsInActiveView(doc, BuiltInCategory.OST_PipeCurves)
                .Where(element =>
                {
                    Pipe pipe = element as Pipe;
                    return pipe != null && string.Equals(PipeInsulationRules.SystemName(pipe), ui.SelectedSystemType, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
            fittingElements = CollectElementsInActiveView(doc, BuiltInCategory.OST_PipeFitting)
                .Where(element => string.Equals(PipeInsulationRules.FittingSystemName(element), ui.SelectedSystemType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return ValidateTargetElements(pipeElements, fittingElements, "Pipe hoặc Pipe Fitting");
    }

    private static Result GetDuctTargetElements(
        UIDocument uidoc,
        Document doc,
        PipeInsulationWindow ui,
        out List<Element> ductElements,
        out List<Element> fittingElements)
    {
        ductElements = new List<Element>();
        fittingElements = new List<Element>();

        if (ui.SelectedScope == PipeInsulationScope.AllInView)
        {
            ductElements = CollectElementsInActiveView(doc, BuiltInCategory.OST_DuctCurves);
            fittingElements = CollectElementsInActiveView(doc, BuiltInCategory.OST_DuctFitting);
        }
        else if (ui.SelectedScope == PipeInsulationScope.SelectedPipes)
        {
            AddSelectedDuctTargets(doc, uidoc.Selection.GetElementIds(), ductElements, fittingElements);
            if (ductElements.Count == 0 && fittingElements.Count == 0)
            {
                IList<Reference> pickedReferences = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new DuctOrFittingSelectionFilter(),
                    "Chọn Duct hoặc Duct Fitting để bọc insulation");
                AddSelectedDuctTargets(doc, pickedReferences.Select(reference => reference.ElementId), ductElements, fittingElements);
            }
        }
        else
        {
            ductElements = CollectElementsInActiveView(doc, BuiltInCategory.OST_DuctCurves)
                .Where(element =>
                {
                    Duct duct = element as Duct;
                    return duct != null && string.Equals(PipeInsulationRules.DuctSystemName(duct), ui.SelectedSystemType, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
            fittingElements = CollectElementsInActiveView(doc, BuiltInCategory.OST_DuctFitting)
                .Where(element => string.Equals(PipeInsulationRules.DuctFittingSystemName(element), ui.SelectedSystemType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return ValidateTargetElements(ductElements, fittingElements, "Duct hoặc Duct Fitting");
    }

    private static Result ValidateTargetElements(List<Element> hostElements, List<Element> fittingElements, string targetLabel)
    {
        if (hostElements.Count == 0 && fittingElements.Count == 0)
        {
            TaskDialog.Show("BIM | Pipe & Duct Insulation", "Không có " + targetLabel + " nào trong phạm vi đã chọn.");
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

    private static void AddSelectedPipeTargets(
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

    private static void AddSelectedDuctTargets(
        Document doc,
        IEnumerable<ElementId> selectedIds,
        ICollection<Element> ductElements,
        ICollection<Element> fittingElements)
    {
        foreach (ElementId selectedId in selectedIds)
        {
            Element element = doc.GetElement(selectedId);
            if (element is Duct)
            {
                ductElements.Add(element);
            }
            else if (IsDuctFitting(element))
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

    private static bool IsDuctFitting(Element element)
    {
        return element != null && element.Category != null &&
               element.Category.Id.Equals(new ElementId((int)BuiltInCategory.OST_DuctFitting));
    }

    private static bool IsPipeTeeOrBranchFitting(Element fitting)
    {
        return IsTeeOrBranchFitting(fitting, Domain.DomainPiping);
    }

    private static bool IsDuctTeeOrBranchFitting(Element fitting)
    {
        return IsTeeOrBranchFitting(fitting, Domain.DomainHvac);
    }

    private static bool IsTeeOrBranchFitting(Element fitting, Domain expectedDomain)
    {
        FamilyInstance familyInstance = fitting as FamilyInstance;
        if (familyInstance == null)
        {
            return false;
        }

        Parameter partTypeParam = familyInstance.Symbol == null || familyInstance.Symbol.Family == null
            ? null
            : familyInstance.Symbol.Family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
        if (partTypeParam != null)
        {
            int partTypeValue = partTypeParam.AsInteger();
            if (partTypeValue == (int)PartType.Tee || partTypeValue == (int)PartType.Cross)
            {
                return true;
            }
        }

        ConnectorManager connectorManager = familyInstance.MEPModel == null ? null : familyInstance.MEPModel.ConnectorManager;
        if (connectorManager != null)
        {
            int connectorCount = 0;
            foreach (Connector connector in connectorManager.Connectors)
            {
                if (connector.Domain == expectedDomain)
                {
                    connectorCount++;
                }
            }

            if (connectorCount >= 3)
            {
                return true;
            }
        }

        string name = ((familyInstance.Name ?? string.Empty) + " " +
                       (familyInstance.Symbol == null || familyInstance.Symbol.Family == null ? string.Empty : familyInstance.Symbol.Family.Name ?? string.Empty))
            .ToLowerInvariant();
        return name.Contains("tee") || name.Contains("tê") || name.Contains("cross") || name.Contains("chạc");
    }

    private static string ScopeDescription(PipeInsulationWindow ui)
    {
        string objectLabel = ui.SelectedMepTarget == MepTargetKind.Duct ? "ống gió" : "đường ống";
        switch (ui.SelectedScope)
        {
            case PipeInsulationScope.AllInView:
                return "Tất cả " + objectLabel + " trong View";
            case PipeInsulationScope.SelectedPipes:
                return objectLabel + " đang chọn";
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
            if (element is PipeInsulationType)
            {
                result.Add(new InsulationTypeItem { Name = element.Name, Tag = element.Id });
            }
        }
        return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<InsulationTypeItem> GetDuctInsulationTypes(Document doc)
    {
        List<InsulationTypeItem> result = new List<InsulationTypeItem>();
        foreach (Element element in new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_DuctInsulations)
            .WhereElementIsElementType())
        {
            if (element is DuctInsulationType)
            {
                result.Add(new InsulationTypeItem { Name = element.Name, Tag = element.Id });
            }
        }
        return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static double GetPipeFittingSize(Element fitting)
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
                if (connector.Domain == Domain.DomainPiping && connector.Shape == ConnectorProfileType.Round)
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

        return ReadLargestSizeFromText(fitting, "Size");
    }

    private static double GetDuctSize(Duct duct)
    {
        if (duct == null)
        {
            return 0.0;
        }

        double diameter = ReadDoubleParameter(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
        if (diameter > 0.0)
        {
            return diameter;
        }

        double width = ReadDoubleParameter(duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
        double height = ReadDoubleParameter(duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
        return Math.Max(width, height);
    }

    private static double GetDuctFittingSize(Element fitting)
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
                if (connector.Domain != Domain.DomainHvac)
                {
                    continue;
                }

                try
                {
                    double connectorSize = connector.Shape == ConnectorProfileType.Round
                        ? connector.Radius * 2.0
                        : Math.Max(connector.Width, connector.Height);
                    if (connectorSize > maxSize)
                    {
                        maxSize = connectorSize;
                    }
                }
                catch
                {
                    // Try the next connector, then fall back to its displayed size.
                }
            }
            if (maxSize > 0.0)
            {
                return maxSize;
            }
        }

        double size = ReadLargestSizeFromText(fitting, "Duct Size");
        return size > 0.0 ? size : ReadLargestSizeFromText(fitting, "Size");
    }

    private static double ReadDoubleParameter(Element element, BuiltInParameter parameterId)
    {
        Parameter parameter = element == null ? null : element.get_Parameter(parameterId);
        return parameter != null && parameter.StorageType == StorageType.Double ? parameter.AsDouble() : 0.0;
    }

    private static double ReadLargestSizeFromText(Element element, string parameterName)
    {
        Parameter parameter = element == null ? null : element.LookupParameter(parameterName);
        if (parameter != null && parameter.StorageType == StorageType.Double)
        {
            return parameter.AsDouble();
        }

        string text = parameter == null ? null : parameter.AsValueString() ?? parameter.AsString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0.0;
        }

        double largestMm = 0.0;
        foreach (Match match in Regex.Matches(text, "[0-9]+(?:[.,][0-9]+)?"))
        {
            double sizeMm;
            if (double.TryParse(
                match.Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out sizeMm))
            {
                largestMm = Math.Max(largestMm, sizeMm);
            }
        }

        return largestMm / 304.8;
    }

    private static int RemoveExistingPipeInsulation(Document doc, IEnumerable<Element> pipes, IEnumerable<Element> fittings)
    {
        return RemoveExistingInsulation(doc, BuiltInCategory.OST_PipeInsulations, pipes, fittings);
    }

    private static int RemoveExistingDuctInsulation(Document doc, IEnumerable<Element> ducts, IEnumerable<Element> fittings)
    {
        return RemoveExistingInsulation(doc, BuiltInCategory.OST_DuctInsulations, ducts, fittings);
    }

    private static int RemoveExistingInsulation(
        Document doc,
        BuiltInCategory insulationCategory,
        IEnumerable<Element> hosts,
        IEnumerable<Element> fittings)
    {
        HashSet<ElementId> targetIds = new HashSet<ElementId>(hosts.Select(host => host.Id));
        targetIds.UnionWith(fittings.Select(fitting => fitting.Id));
        List<ElementId> insulationIds = new List<ElementId>();

        foreach (Element element in new FilteredElementCollector(doc)
            .OfCategory(insulationCategory)
            .WhereElementIsNotElementType())
        {
            ElementId hostId = GetInsulationHostId(element);
            if (hostId != null && targetIds.Contains(hostId))
            {
                insulationIds.Add(element.Id);
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

    private static ElementId GetInsulationHostId(Element insulation)
    {
        PipeInsulation pipeInsulation = insulation as PipeInsulation;
        if (pipeInsulation != null)
        {
            return pipeInsulation.HostElementId;
        }

        DuctInsulation ductInsulation = insulation as DuctInsulation;
        return ductInsulation == null ? null : ductInsulation.HostElementId;
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

    private sealed class DuctOrFittingSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            return element is Duct || IsDuctFitting(element);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
