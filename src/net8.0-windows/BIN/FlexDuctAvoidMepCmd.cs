using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class FlexDuctAvoidMepCmd : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        if (uidoc == null) { message = "Cần mở một mô hình Revit."; return Result.Failed; }
        Document doc = uidoc.Document;
        try
        {
            Reference flexReference = uidoc.Selection.PickObject(ObjectType.Element, new AvoidMep_FlexDuctFilter(),
                "1. Chọn Flex Duct hoặc Flex Pipe trong Host Model (ESC: đóng).");
            var flex = doc.GetElement(flexReference) as MEPCurve;
            Reference obstacleReference = uidoc.Selection.PickObject(ObjectType.PointOnElement, new AvoidMep_ObstacleFilter(doc),
                "2. Chọn dầm, sàn hoặc cấu kiện 3D trong Host / Revit Link (TAB để chọn trong Link). ESC: đóng.");
            Element obstacle;
            Transform transform = Transform.Identity;
            bool linked = obstacleReference.LinkedElementId != null && !ElementId.InvalidElementId.Equals(obstacleReference.LinkedElementId);
            if (linked)
            {
                var link = doc.GetElement(obstacleReference.ElementId) as RevitLinkInstance;
                Document linkDocument = link?.GetLinkDocument();
                if (linkDocument == null) throw new InvalidOperationException("Revit Link chưa được tải.");
                obstacle = linkDocument.GetElement(obstacleReference.LinkedElementId);
                transform = link.GetTotalTransform();
            }
            else obstacle = doc.GetElement(obstacleReference.ElementId);
            if (flex == null || !AvoidMep_ObstacleFilter.IsObstacle(obstacle)
                || (!linked && obstacle.Id.Equals(flex.Id)))
                throw new InvalidOperationException("Cần chọn Flex trong Host và một vật cản 3D khác.");

            double diameter = NominalDiameter(flex);
            double insulation = Insulation(flex, out string insulationSource);
            using (var geometry = new FlexAvoidGeometry(obstacle, transform))
            {
                var window = new AvoidMepWindow(flex is FlexDuct ? "FLEX DUCT" : "FLEX PIPE",
                    (linked ? "LINK · " : "HOST · ") + obstacle.Category.Name + " · " + obstacle.Name,
                    diameter / FlexAvoidMath.Mm, insulation / FlexAvoidMath.Mm, insulationSource,
                    (settings, autoAvoid) =>
                    {
                        double outerRadius = diameter / 2 + settings.InsulationMm * FlexAvoidMath.Mm;
                        double reach = outerRadius + settings.ClearanceMm * FlexAvoidMath.Mm;
                        FlexAvoidMath.Check check;
                        bool changed = false;
                        if (autoAvoid)
                        {
                            check = geometry.Check(flex);
                            if (check.LowerBound < reach)
                            {
                                check = Apply(doc, flex, geometry, reach, settings.Up);
                                changed = true;
                            }
                        }
                        else check = geometry.Check(flex);
                        string highlightNote = Highlight(uidoc, flexReference, obstacleReference, check.CurvePoint, outerRadius);
                        return Report(check, outerRadius, reach, settings.ClearanceMm, autoAvoid, changed, highlightNote);
                    });
                IntPtr handle = commandData.Application.MainWindowHandle;
                if (handle != IntPtr.Zero) new WindowInteropHelper(window).Owner = handle;
                window.ShowDialog();
                if (!string.IsNullOrEmpty(window.LastError)) { message = window.LastError; return Result.Failed; }
                if (window.HasExecuted) return Result.Succeeded;
                return Result.Cancelled;
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
        catch (Exception ex)
        {
            message = ex.Message;
            TaskDialog.Show("BIM TOOL · Flex tránh dầm", ex.Message);
            return Result.Failed;
        }
    }

    private static double NominalDiameter(MEPCurve flex)
    {
        BuiltInParameter parameter = flex is FlexPipe ? BuiltInParameter.RBS_PIPE_DIAMETER_PARAM : BuiltInParameter.RBS_CURVE_DIAMETER_PARAM;
        double diameter = ParameterLength(flex, parameter);
        if (diameter <= 0) diameter = ParameterLength(flex.Document.GetElement(flex.GetTypeId()), parameter);
        if (!FlexAvoidMath.Finite(diameter) || diameter <= 0)
            throw new InvalidOperationException("Không đọc được đường kính danh nghĩa của Flex tròn.");
        return diameter;
    }

    private static double Insulation(MEPCurve flex, out string source)
    {
        double thickness = 0;
        source = "Không phát hiện bảo ôn · kiểm tra hoặc nhập giá trị thực tế";
        foreach (Element owner in new[] { (Element)flex, flex.Document.GetElement(flex.GetTypeId()) })
        {
            // The requested flex-specific enum is absent in some Revit versions.
            // Resolve by name, then use the supported reference/pipe parameters.
            foreach (string name in new[] { "RBS_FLEX_DUCT_INSULATION_THICKNESS", "RBS_REFERENCE_INSULATION_THICKNESS",
                "RBS_PIPE_INSULATION_THICKNESS", "RBS_INSULATION_THICKNESS_FOR_DUCT", "RBS_INSULATION_THICKNESS_FOR_PIPE", "RBS_INSULATION_THICKNESS" })
            {
                if (!Enum.TryParse(name, out BuiltInParameter parameter)) continue;
                double value = ParameterLength(owner, parameter);
                if (value > thickness) { thickness = value; source = (owner is ElementType ? "Type" : "Instance") + " · " + name; }
            }
        }
        try
        {
            foreach (ElementId id in InsulationLiningBase.GetInsulationIds(flex.Document, flex.Id))
                if (flex.Document.GetElement(id) is InsulationLiningBase layer && layer.Thickness > thickness)
                { thickness = layer.Thickness; source = "Insulation gắn với đối tượng"; }
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // Flex is not a valid insulation host in all versions; its own
            // instance/type parameters above remain authoritative for detection.
        }
        return thickness;
    }

    private static double ParameterLength(Element element, BuiltInParameter parameter)
    {
        Parameter p = element?.get_Parameter(parameter);
        if (p == null || !p.HasValue || p.StorageType != StorageType.Double) return 0;
        double value = p.AsDouble();
        return FlexAvoidMath.Finite(value) && value >= 0 ? value : 0;
    }

    private static FlexAvoidMath.Check Apply(Document doc, MEPCurve flex, FlexAvoidGeometry obstacle, double reach, bool up)
    {
        if (flex.Pinned) throw new InvalidOperationException("Flex đang được ghim (Pinned). Hãy bỏ ghim trước khi né dầm.");
        var original = new EndState(flex);
        if (obstacle.Distance(original.Start).SignedDistance < reach || obstacle.Distance(original.End).SignedDistance < reach)
            throw new InvalidOperationException("Đầu nối nằm trong vùng cần tránh. Không thể giữ nguyên hai đầu và đạt Clearance.");
        FlexAvoidMath.Route route = FlexAvoidMath.Plan(original.Start, original.End, obstacle.Triangles, reach);
        var watch = Stopwatch.StartNew();
        using (var group = new TransactionGroup(doc, "Flex Duct Avoid Beam"))
        {
            group.Start();
            try
            {
                using (var transaction = new Transaction(doc, "Flex Duct Avoid Beam"))
                {
                    transaction.Start();
                    var failures = new RollbackOnError();
                    transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions()
                        .SetFailuresPreprocessor(failures).SetClearAfterRollback(true).SetForcedModalHandling(true));
                    FlexAvoidMath.Check Probe(double extra, bool keep)
                    {
                        if (watch.Elapsed.TotalSeconds > 30)
                            throw new InvalidOperationException("Chưa tìm được tuyến đạt Clearance trong giới hạn xử lý. Đã giữ nguyên Flex.");
                        using (var attempt = new SubTransaction(doc))
                        {
                            attempt.Start();
                            try
                            {
                                FlexAvoidGeometry.SetPoints(flex, route.Points(up, extra));
                                original.RestoreTangents(flex);
                                doc.Regenerate();
                                original.Verify(flex);
                                FlexAvoidMath.Check measured = obstacle.Check(flex);
                                if (keep)
                                {
                                    if (measured.LowerBound < reach) throw new InvalidOperationException("Spline sau tái tạo chưa đạt Clearance.");
                                    if (attempt.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Không chấp nhận được tuyến Flex mới.");
                                }
                                return measured;
                            }
                            finally
                            {
                                if (attempt.GetStatus() == TransactionStatus.Started) attempt.RollBack();
                            }
                        }
                    }
                    double unsafeExtra = 0, safeExtra = FlexAvoidMath.DistanceTolerance;
                    FlexAvoidMath.Check candidate = Probe(safeExtra, false);
                    for (int i = 0; candidate.LowerBound < reach && i < 7; i++)
                    {
                        unsafeExtra = safeExtra;
                        safeExtra += Math.Max(25 * FlexAvoidMath.Mm, reach - candidate.LowerBound + 5 * FlexAvoidMath.Mm);
                        if (safeExtra > 5000 * FlexAvoidMath.Mm) break;
                        candidate = Probe(safeExtra, false);
                    }
                    if (candidate.LowerBound < reach) throw new InvalidOperationException("Không tìm được tuyến spline né dầm an toàn với hai đầu hiện tại.");
                    // Reduce unnecessary sag/raise while retaining the certified
                    // clearance bound. Initial lowest/highest-face route is kept
                    // if it already passes without extra displacement.
                    for (int i = 0; safeExtra - unsafeExtra > FlexAvoidMath.Mm && i < 7; i++)
                    {
                        double middle = (safeExtra + unsafeExtra) / 2;
                        if (Probe(middle, false).LowerBound >= reach) safeExtra = middle; else unsafeExtra = middle;
                    }
                    Probe(safeExtra, true);
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit không commit tuyến Flex. " + failures.Description);
                }
                // A transaction group can still roll back after Revit failure
                // processing/commit changes the resulting curve or connection.
                original.Verify(flex);
                FlexAvoidMath.Check final = obstacle.Check(flex);
                if (final.LowerBound < reach) throw new InvalidOperationException("Spline sau commit không đạt Clearance; đã rollback.");
                VerifySpace(doc, flex, reach, watch);
                if (group.Assimilate() != TransactionStatus.Committed) throw new InvalidOperationException("Không hoàn tất được nhóm transaction.");
                return final;
            }
            catch
            {
                if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                throw;
            }
        }
    }

    // Spatially filtered read-only check of nearby Host and loaded Link solids.
    // In particular an upward route cannot be committed through a floor/roof.
    private static void VerifySpace(Document doc, MEPCurve flex, double reach, Stopwatch watch)
    {
        using (BoundingBoxXYZ box = flex.get_BoundingBox(null))
        {
            if (box == null) throw new InvalidOperationException("Không đọc được vùng kiểm tra không gian của Flex sau né dầm.");
            var worldCorners = new List<XYZ>();
            for (int i = 0; i < 8; i++) worldCorners.Add(box.Transform.OfPoint(new XYZ(
                (i & 4) == 0 ? box.Min.X : box.Max.X,
                (i & 2) == 0 ? box.Min.Y : box.Max.Y,
                (i & 1) == 0 ? box.Min.Z : box.Max.Z)));
            XYZ min = new XYZ(worldCorners.Min(p => p.X) - reach, worldCorners.Min(p => p.Y) - reach, worldCorners.Min(p => p.Z) - reach);
            XYZ max = new XYZ(worldCorners.Max(p => p.X) + reach, worldCorners.Max(p => p.Y) + reach, worldCorners.Max(p => p.Z) + reach);
            var ignored = new HashSet<ElementId> { flex.Id };
            // The intended endpoint joints already share space with the Flex.
            foreach (Connector end in Ends(flex))
                foreach (Connector peer in end.AllRefs.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End && end.IsConnectedTo(c)))
                    ignored.Add(peer.Owner.Id);
            try { foreach (ElementId id in InsulationLiningBase.GetInsulationIds(doc, flex.Id)) ignored.Add(id); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }
            int bodies = 0, candidates = 0;
            IEnumerable<Element> Nearby(Document document, Transform toHost)
            {
                Transform inverse = toHost.Inverse;
                var local = new List<XYZ>();
                for (int i = 0; i < 8; i++) local.Add(inverse.OfPoint(new XYZ(
                    (i & 4) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 1) == 0 ? min.Z : max.Z)));
                using (var outline = new Outline(new XYZ(local.Min(p => p.X), local.Min(p => p.Y), local.Min(p => p.Z)),
                    new XYZ(local.Max(p => p.X), local.Max(p => p.Y), local.Max(p => p.Z))))
                using (var filter = new BoundingBoxIntersectsFilter(outline))
                using (var collector = new FilteredElementCollector(document))
                    return collector.WhereElementIsNotElementType().WherePasses(filter).ToElements();
            }
            void Validate(Element candidate, Transform toHost)
            {
                if (!AvoidMep_ObstacleFilter.IsObstacle(candidate)) return;
                if (++candidates > 128 || watch.Elapsed.TotalSeconds > 30)
                    throw new InvalidOperationException("Vùng né có quá nhiều cấu kiện; chưa xác minh được không gian trống. Đã rollback.");
                using (var geometry = new FlexAvoidGeometry(candidate, toHost, false))
                {
                    if (!geometry.HasBodies) return;
                    if (++bodies > 32) throw new InvalidOperationException("Vùng né có quá nhiều Solid lân cận để xác minh. Đã rollback.");
                    if (geometry.Check(flex).LowerBound < reach)
                        throw new InvalidOperationException("Tuyến né không đủ khoảng trống với " + candidate.Category.Name + " · " + candidate.Name + ". Đã rollback.");
                }
            }
            foreach (Element candidate in Nearby(doc, Transform.Identity))
            {
                if (ignored.Contains(candidate.Id)) continue;
                if (candidate is RevitLinkInstance link)
                {
                    Document linked = link.GetLinkDocument();
                    if (linked == null) throw new InvalidOperationException("Có Revit Link chưa tải trong vùng né; chưa xác minh được không gian trống.");
                    Transform total = link.GetTotalTransform();
                    foreach (Element child in Nearby(linked, total))
                    {
                        if (child is RevitLinkInstance) throw new InvalidOperationException("Vùng né chứa Link lồng nhau; cần kiểm tra thủ công trước khi áp dụng.");
                        Validate(child, total);
                    }
                }
                else Validate(candidate, Transform.Identity);
            }
        }
    }

    private sealed class EndState
    {
        internal readonly XYZ Start, End;
        private readonly XYZ _startTangent, _endTangent;
        private readonly List<ConnectorState> _connectors;
        internal EndState(MEPCurve flex)
        {
            IList<XYZ> points = FlexAvoidGeometry.Points(flex);
            if (points.Count < 2) throw new InvalidOperationException("Flex không có hai đầu hợp lệ.");
            Start = points[0]; End = points[points.Count - 1];
            _startTangent = flex is FlexDuct duct ? duct.StartTangent : ((FlexPipe)flex).StartTangent;
            _endTangent = flex is FlexDuct duct2 ? duct2.EndTangent : ((FlexPipe)flex).EndTangent;
            _connectors = Ends(flex).Select(c => new ConnectorState
                { Id = c.Id, Origin = c.Origin, Direction = c.CoordinateSystem.BasisZ, Links = Links(c), Connected = c.IsConnected }).ToList();
            if (_connectors.Count != 2) throw new InvalidOperationException("Cần xác minh đúng hai connector đầu của Flex.");
        }
        internal void RestoreTangents(MEPCurve flex)
        {
            if (flex is FlexDuct duct) { duct.StartTangent = _startTangent; duct.EndTangent = _endTangent; }
            else { var pipe = (FlexPipe)flex; pipe.StartTangent = _startTangent; pipe.EndTangent = _endTangent; }
        }
        internal void Verify(MEPCurve flex)
        {
            IList<XYZ> points = FlexAvoidGeometry.Points(flex);
            if (points[0].DistanceTo(Start) > 1e-5 || points[points.Count - 1].DistanceTo(End) > 1e-5)
                throw new InvalidOperationException("Hai đầu Flex đã thay đổi; rollback để bảo vệ kết nối.");
            List<Connector> ends = Ends(flex).ToList();
            foreach (ConnectorState original in _connectors)
            {
                Connector current = ends.SingleOrDefault(c => c.Id == original.Id);
                if (current == null || current.Origin.DistanceTo(original.Origin) > 1e-5 || current.IsConnected != original.Connected
                    || current.CoordinateSystem.BasisZ.Normalize().DotProduct(original.Direction.Normalize()) < 0.9999619230641713
                    || !Links(current).SequenceEqual(original.Links))
                    throw new InvalidOperationException("Vị trí, hướng hoặc liên kết connector đã thay đổi; rollback.");
            }
        }
    }
    private sealed class ConnectorState
    {
        internal int Id;
        internal XYZ Origin, Direction;
        internal bool Connected;
        internal string[] Links;
    }
    private static IEnumerable<Connector> Ends(MEPCurve flex)
        => flex.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End);
    private static string[] Links(Connector connector)
        => connector.AllRefs.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End
            && !c.Owner.Id.Equals(connector.Owner.Id) && connector.IsConnectedTo(c))
            .Select(c => c.Owner.Id + ":" + c.Id).OrderBy(s => s, StringComparer.Ordinal).ToArray();

    private sealed class RollbackOnError : IFailuresPreprocessor
    {
        internal string Description = "";
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            IList<FailureMessageAccessor> failures = accessor.GetFailureMessages();
            Description = string.Join("; ", failures.Select(f => f.GetDescriptionText()));
            return failures.Any(f => f.GetSeverity() == FailureSeverity.Error || f.GetSeverity() == FailureSeverity.DocumentCorruption)
                ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }

    private static FlexAvoidReport Report(FlexAvoidMath.Check check, double outerRadius, double reach,
        double clearanceMm, bool auto, bool changed, string highlightNote)
    {
        double gap = (check.Minimum - outerRadius) / FlexAvoidMath.Mm;
        double guaranteedGap = (check.LowerBound - outerRadius) / FlexAvoidMath.Mm;
        bool warning = check.LowerBound < reach;
        string status = changed ? "ĐÃ NÉ DẦM · đã kiểm tra spline sau commit"
            : warning ? (gap < 0 ? "VA CHẠM · bao ngoài bảo ôn ăn vào vật cản" : "THIẾU KHOẢNG HỞ AN TOÀN")
            : auto ? "ĐÃ ĐỦ KHOẢNG HỞ · không cần thay đổi Flex" : "ĐẠT KHOẢNG HỞ";
        XYZ p = check.CurvePoint;
        string N(double n) => n.ToString("0.0", CultureInfo.CurrentCulture);
        return new FlexAvoidReport
        {
            Warning = warning,
            Text = status + "\nKhoảng cách tâm → mặt Solid (có dấu): " + N(check.Minimum / FlexAvoidMath.Mm) + " mm"
                + "\nKhoảng hở vỏ ngoài: " + N(gap) + " mm · cận dưới: " + N(guaranteedGap) + " mm"
                + "\nĐộ ăn sâu: " + N(Math.Max(0, -gap)) + " mm · thiếu Clearance: " + N(Math.Max(0, clearanceMm - guaranteedGap)) + " mm"
                + "\nSai số khoảng cách ≤ " + N(check.Uncertainty / FlexAvoidMath.Mm) + " mm · " + check.Evaluations + " phép đo"
                + "\nVị trí tâm gần nhất XYZ: " + N(p.X / FlexAvoidMath.Mm) + ", " + N(p.Y / FlexAvoidMath.Mm) + ", " + N(p.Z / FlexAvoidMath.Mm) + " mm"
                + "\n" + highlightNote
        };
    }

    private static string Highlight(UIDocument uidoc, Reference flex, Reference obstacle, XYZ point, double radius)
    {
        try
        {
            uidoc.Selection.SetReferences(new List<Reference> { flex, obstacle });
            UIView view = uidoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId.Equals(uidoc.ActiveView.Id));
            double size = Math.Max(300 * FlexAvoidMath.Mm, 4 * radius);
            XYZ diagonal = (uidoc.ActiveView.RightDirection + uidoc.ActiveView.UpDirection) * size;
            view?.ZoomAndCenterRectangle(point - diagonal, point + diagonal);
            uidoc.RefreshActiveView();
            return "Đã highlight Flex / vật cản và focus vùng gần nhất trong view.";
        }
        catch (Exception ex) { return "Kết quả đo đã có; view không highlight được: " + ex.Message; }
    }
}
