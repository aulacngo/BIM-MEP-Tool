using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public partial class ConnectSprinklerFlexPipeCmd
{
    private sealed class BranchOutlet
    {
        public ElementId OwnerId;
        public XYZ Point;
        public FamilyInstance Sprinkler;
        public string Kind;
    }

    private static List<Element> CollectMultiSelection(UIDocument uidoc)
    {
        var ids = new HashSet<ElementId>(uidoc.Selection.GetElementIds()
            .Where(id => IsPhase2SelectableElement(uidoc.Document.GetElement(id))));
        while (true)
        {
            uidoc.Selection.SetElementIds(ids.ToList());
            var dialog = new TaskDialog("Flex Multi — Chọn cộng dồn");
            dialog.MainInstruction = $"Đang chọn {ids.Count} phần tử";
            dialog.MainContent = "Quét hoặc pick thêm nhiều lần. Finish trong bước pick chỉ trở về menu này. Hoàn tất mới xử lý nối. Esc trong bước chọn giữ nguyên tập chọn trước bước đó.";
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Quét thêm hình chữ nhật");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Pick thêm phần tử (Finish để quay lại)");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Pick phần tử cần bỏ (Finish để quay lại)");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Hoàn tất lựa chọn — tiếp tục nối");
            dialog.CommonButtons = TaskDialogCommonButtons.Cancel;
            var choice = dialog.Show();
            if (choice == TaskDialogResult.CommandLink4)
                return ids.Select(uidoc.Document.GetElement).ToList();
            if (choice == TaskDialogResult.Cancel) return null;
            try
            {
                var picked = choice == TaskDialogResult.CommandLink1
                    ? uidoc.Selection.PickElementsByRectangle(new SprinklerAndPipeSelectionFilter(), "Quét thêm sprinkler, Pipe hoặc fitting").Select(e => e.Id).ToList()
                    : uidoc.Selection.PickObjects(ObjectType.Element, new SprinklerAndPipeSelectionFilter(),
                        "Pick các phần tử rồi bấm Finish để quay lại menu").Select(r => r.ElementId).ToList();
                if (choice == TaskDialogResult.CommandLink3) ids.ExceptWith(picked);
                else ids.UnionWith(picked);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
        }
    }

    private static Result ExecuteBranchMulti(UIDocument uidoc, ref string message, bool elbowOnly = false)
    {
        if (uidoc == null) return Result.Failed;
        Document doc = uidoc.Document;
        try
        {
            var selected = CollectMultiSelection(uidoc);
            if (selected == null) return Result.Cancelled;
            var pipes = selected.OfType<Pipe>().OrderBy(p => p.Id.GetIdInt()).ToList();
            var sprinklers = selected.OfType<FamilyInstance>().Where(IsSprinkler).OrderBy(s => s.Id.GetIdInt()).ToList();
            var fittings = selected.OfType<FamilyInstance>().Where(IsPipeFitting).ToList();
            FlexPipeDiagnostics.Write("multi-branch", "selection", "Rectangle/preselection captured",
                details: $"pipes={string.Join(",", pipes.Select(p => p.Id))}; fittings={string.Join(",", fittings.Select(f => f.Id))}; sprinklers={string.Join(",", sprinklers.Select(s => s.Id))}");
            if (sprinklers.Count == 0)
                throw new InvalidOperationException($"Vùng quét nhận {pipes.Count} ống, {fittings.Count} fitting nhưng không có sprinkler Revit. Hãy quét bao cả sprinkler và nhánh ống.");
            var unavailable = sprinklers.Where(s => GetSprinklerConnector(s) == null).ToList();
            if (unavailable.Count > 0)
            {
                string detail = string.Join("\n", unavailable.Select(s =>
                {
                    var connectors = s.MEPModel?.ConnectorManager?.Connectors;
                    string states = connectors == null ? "family không cung cấp connector" : string.Join("; ", connectors.Cast<Connector>()
                        .Where(c => c.Domain == Domain.DomainPiping && !IsLogicalConnector(c))
                        .Select(c => $"type={c.ConnectorType}, connected={c.IsConnected}"));
                    return $"Sprinkler {s.Id}: {states}";
                }));
                FlexPipeDiagnostics.Write("multi-branch", "sprinkler-unavailable", "No free piping connector", details: detail);
                throw new InvalidOperationException("Đã nhận sprinkler nhưng không lấy được đầu nối ống hở:\n" + detail);
            }

            // Validate a single chain of selected pipes, allowing intervening pipe fittings.
            var pipeIds = new HashSet<ElementId>(pipes.Select(p => p.Id));
            var adjacency = pipes.ToDictionary(p => p.Id, p => new HashSet<ElementId>());
            foreach (Pipe pipe in pipes)
            {
                if (!elbowOnly && (!((pipe.Location as LocationCurve)?.Curve is Line line) || Math.Abs(line.Direction.Z) > 0.01))
                    throw new InvalidOperationException($"Ống {pipe.Id}: bản đầu hỗ trợ nhánh thẳng nằm ngang, kể cả nhiều đoạn qua reducer.");
                var visited = new HashSet<ElementId> { pipe.Id };
                var queue = new Queue<Element>(); queue.Enqueue(pipe);
                while (queue.Count > 0)
                {
                    Element element = queue.Dequeue();
                    ConnectorSet connectors = element is Pipe p ? p.ConnectorManager.Connectors : ((FamilyInstance)element).MEPModel?.ConnectorManager?.Connectors;
                    if (connectors == null) continue;
                    foreach (Connector connector in connectors)
                    {
                        if (connector.ConnectorType != ConnectorType.End || connector.Domain != Domain.DomainPiping || !connector.IsConnected) continue;
                        foreach (Connector reference in connector.AllRefs)
                        {
                            if (reference.ConnectorType != ConnectorType.End || !connector.IsConnectedTo(reference)) continue;
                            Element owner = reference.Owner;
                            if (!visited.Add(owner.Id)) continue;
                            if (owner is Pipe other)
                            {
                                if (pipeIds.Contains(other.Id)) adjacency[pipe.Id].Add(other.Id);
                                continue;
                            }
                            if (owner is FamilyInstance fitting && IsPipeFitting(fitting))
                            {
                                if (!fittings.Any(f => f.Id == fitting.Id)) fittings.Add(fitting);
                                queue.Enqueue(fitting);
                            }
                        }
                    }
                }
            }
            if (!elbowOnly && pipes.Count > 0)
            {
                var reached = new HashSet<ElementId>();
                var pending = new Queue<ElementId>(); pending.Enqueue(pipes[0].Id);
                while (pending.Count > 0)
                {
                    ElementId id = pending.Dequeue();
                    if (!reached.Add(id)) continue;
                    foreach (ElementId next in adjacency[id]) pending.Enqueue(next);
                }
                if (reached.Count != pipes.Count || adjacency.Any(a => a.Value.Count > 2))
                    throw new InvalidOperationException("Chọn một tuyến nhánh liên thông; không quét nhiều nhánh hoặc bỏ sót đoạn ống giữa.");
            }

            var outlets = new List<BranchOutlet>();
            foreach (FamilyInstance fitting in fittings.OrderBy(f => f.Id.GetIdInt()))
            {
                var open = GetUnusedConnectors(fitting).Where(c => c.Domain == Domain.DomainPiping).ToList();
                if (open.Count == 0) continue;
                if (open.Count != 1 || !IsNearlyVerticalDown(open[0].CoordinateSystem.BasisZ) || GetConnectedPipe(fitting) == null)
                    throw new InvalidOperationException($"Fitting {fitting.Id}: cần một đầu chờ hở hướng xuống, nối trực tiếp với Pipe.");
                outlets.Add(new BranchOutlet { OwnerId = fitting.Id, Point = open[0].Origin, Kind = "Dùng fitting" });
            }
            var freeEnds = pipes.SelectMany(p => GetPipeEndConnectors(p).Where(c => !c.IsConnected)).ToList();
            if (!elbowOnly && freeEnds.Count > 1)
                throw new InvalidOperationException("Nhánh có nhiều đầu ống hở, chưa xác định được đầu cuối. Hãy chọn nhánh đã nối đầu cấp, chỉ còn một đầu cuối hở.");
            foreach (Connector end in freeEnds)
                outlets.Add(new BranchOutlet { OwnerId = end.Owner.Id, Point = end.Origin, Kind = "Tạo Elbow cuối" });
            if (outlets.Count > sprinklers.Count)
                throw new InvalidOperationException("Số đầu chờ/đầu cuối nhiều hơn sprinkler chọn. Hãy kiểm tra lại phạm vi chọn.");
            if (elbowOnly && outlets.Count != sprinklers.Count)
                throw new InvalidOperationException($"Elbow Multi cần mỗi sprinkler một đầu chờ fitting hoặc đầu ống hở. Nhận {outlets.Count} đầu chờ và {sprinklers.Count} sprinkler. Không tạo Tê trong chế độ này.");

            // Pad mandatory outlets with zero-cost dummy rows for the remaining Tee connections.
            int count = sprinklers.Count;
            double[,] costs = new double[count, count];
            for (int i = 0; i < outlets.Count; i++)
                for (int j = 0; j < count; j++) costs[i, j] = outlets[i].Point.DistanceTo(GetSprinklerConnector(sprinklers[j]).Origin);
            int[] assignment = SolveMinimumCostAssignment(costs);
            var used = new HashSet<ElementId>();
            for (int i = 0; i < outlets.Count; i++)
            {
                if (elbowOnly && costs[i, assignment[i]] > 50.0)
                    throw new InvalidOperationException("Cặp Elbow/Fitting và sprinkler cách nhau quá 15240 mm. Hãy thu hẹp lựa chọn.");
                outlets[i].Sprinkler = sprinklers[assignment[i]];
                used.Add(outlets[i].Sprinkler.Id);
            }
            var tees = new List<BranchOutlet>();
            foreach (FamilyInstance sprinkler in sprinklers.Where(s => !used.Contains(s.Id)))
            {
                XYZ origin = GetSprinklerConnector(sprinkler).Origin;
                var candidates = pipes.Select(p => new { Pipe = p, Line = (Line)((LocationCurve)p.Location).Curve })
                    .Select(p => new { p.Pipe, p.Line, Point = ProjectPointOnLine(p.Line, origin) })
                    .Where(p => Math.Abs(p.Point.DistanceTo(p.Line.GetEndPoint(0)) + p.Point.DistanceTo(p.Line.GetEndPoint(1)) - p.Line.Length) < 0.001)
                    .OrderBy(p => p.Point.DistanceTo(origin)).ToList();
                if (candidates.Count == 0) throw new InvalidOperationException($"Sprinkler {sprinkler.Id}: điểm chiếu nằm ngoài nhánh, không thể đặt Tê.");
                var candidate = candidates[0];
                if (candidates.Count > 1 && Math.Abs(candidates[1].Point.DistanceTo(origin) - candidate.Point.DistanceTo(origin)) < 1 / 304.8)
                    throw new InvalidOperationException($"Sprinkler {sprinkler.Id}: có hai đoạn ống gần bằng nhau, cần thu hẹp lựa chọn.");
                if (candidate.Point.DistanceTo(candidate.Line.GetEndPoint(0)) < 100 / 304.8 || candidate.Point.DistanceTo(candidate.Line.GetEndPoint(1)) < 100 / 304.8)
                    throw new InvalidOperationException($"Sprinkler {sprinkler.Id}: Tê dự kiến cách đầu đoạn ống/fitting dưới 100 mm.");
                if (tees.Any(t => t.Point.DistanceTo(candidate.Point) < 100 / 304.8))
                    throw new InvalidOperationException("Hai điểm Tê dự kiến cách nhau dưới 100 mm; cần chỉnh phạm vi hoặc vị trí.");
                tees.Add(new BranchOutlet { OwnerId = candidate.Pipe.Id, Point = candidate.Point, Sprinkler = sprinkler, Kind = "Tạo Tê" });
            }
            string preview = string.Join("\n", outlets.Concat(tees).Select(o =>
                $"{o.Kind} {o.OwnerId} → SP {o.Sprinkler.Id}; dài thẳng {o.Point.DistanceTo(GetSprinklerConnector(o.Sprinkler).Origin) * 304.8:F0} mm"));
            FlexPipeDiagnostics.Write("multi-branch", "planned", "Geometry branch plan", details: preview);
            if (TaskDialog.Show(elbowOnly ? "Elbow Multi" : "Nhánh Tê + Elbow", preview + "\n\nKiểm tra cặp ghép rồi chọn Yes để tạo. Fitting có sẵn được giữ nguyên.",
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No) != TaskDialogResult.Yes) return Result.Cancelled;
            var before = new HashSet<ElementId>(new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElementIds());
            using (TransactionGroup group = new TransactionGroup(doc, "Flex Multi - Tee and Elbow branch"))
            {
                group.Start();
                try
                {
                    foreach (BranchOutlet outlet in outlets)
                    {
                        Element owner = doc.GetElement(outlet.OwnerId);
                        Connector connector = FindUnusedConnectorAtPoint(owner, outlet.Point);
                        Pipe pipe = owner as Pipe ?? GetConnectedPipe(owner as FamilyInstance);
                        if (connector == null || pipe == null) throw new InvalidOperationException("Đầu chờ đã thay đổi; hãy chạy lại.");
                        if (!ExecuteConnectFlexPipeDirect(doc, new List<FamilyInstance> { outlet.Sprinkler }, pipe, out string log, true, connector))
                            throw new InvalidOperationException(log);
                    }
                    // Batch all Tee points on each original pipe so splitting never invalidates the next source ID.
                    foreach (var batch in tees.GroupBy(t => t.OwnerId))
                        if (!ExecuteConnectFlexPipeDirect(doc, batch.Select(t => t.Sprinkler).ToList(), (Pipe)doc.GetElement(batch.Key), out string log, false))
                            throw new InvalidOperationException(log);
                    group.Assimilate();
                }
                catch { group.RollBack(); throw; }
            }
            var created = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElementIds().Where(id => !before.Contains(id)).ToList();
            uidoc.Selection.SetElementIds(created);
            FlexPipeDiagnostics.Write("multi-branch", "succeeded", $"Connected {count} sprinklers", details: preview + "; created=" + string.Join(",", created));
            TaskDialog.Show("Nhánh Tê + Elbow", $"Đã nối {count} sprinkler. Đang chọn các phần tử vừa tạo.");
            return Result.Succeeded;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
        catch (Exception ex)
        {
            message = ex.Message;
            FlexPipeDiagnostics.Write("multi-branch", "failed", ex.Message, details: ex.ToString());
            TaskDialog.Show("Nhánh Tê + Elbow", ex.Message + "\nCụm chưa tạo thành công được hoàn tác.");
            return Result.Failed;
        }
    }
}
