using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class AvoidClashCmd : IExternalCommand
{
	private const double ConnectorToleranceFeet = 1e-4;
	private const double GeometryToleranceFeet = 1e-6;
	private const double AxisDotTolerance = 0.9999619230641713; // cos(0.5 degrees)
	private const double MinimumOffsetFeet = 50.0 / 304.8;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			Reference refPipe = uidoc.Selection.PickObject(
				ObjectType.Element,
				new AvoidClashMepSelectionFilter(),
				"1. Click chon ONG (Pipe) hoac ONG GIO (Duct) can uon de ne va cham");
			if (refPipe == null) return Result.Cancelled;

			Element runningPipeElem = doc.GetElement(refPipe);
			if (runningPipeElem == null) return Result.Cancelled;

			Reference refObstacle = null;
			try
			{
				refObstacle = uidoc.Selection.PickObject(
					ObjectType.PointOnElement,
					new AvoidClashObstacleSelectionFilter(doc),
					"2. Click chon VAT CAN (Dam ket cau trong file Link, hoac Ong, Ong gio, Tuong, San...)");
			}
			catch (Autodesk.Revit.Exceptions.OperationCanceledException)
			{
				return Result.Cancelled;
			}
			if (refObstacle == null) return Result.Cancelled;

			Element obstacleElem = null;
			Transform obstacleTransform = Transform.Identity;
			if (refObstacle.LinkedElementId != null && refObstacle.LinkedElementId != ElementId.InvalidElementId)
			{
				RevitLinkInstance linkInstance = doc.GetElement(refObstacle.ElementId) as RevitLinkInstance;
				Document linkDoc = linkInstance?.GetLinkDocument();
				if (linkDoc == null)
				{
					TaskDialog.Show("Loi", "Khong the doc du lieu file Link Revit. Vui long kiem tra file link da duoc load day du.");
					return Result.Cancelled;
				}

				obstacleElem = linkDoc.GetElement(refObstacle.LinkedElementId);
				obstacleTransform = linkInstance.GetTotalTransform();
			}
			else
			{
				obstacleElem = doc.GetElement(refObstacle);
			}

			if (obstacleElem == null || !AvoidClashObstacleSelectionFilter.IsValidObstacleCategory(obstacleElem.Category))
			{
				TaskDialog.Show("Thong bao", "Vui long chon dam ket cau, ong, ong gio, mang cap, tuong hoac san.");
				return Result.Cancelled;
			}

			AvoidClashWindow window = new AvoidClashWindow(uidoc, runningPipeElem, obstacleElem, obstacleTransform);
			IntPtr h = commandData.Application.MainWindowHandle;
			if (h != IntPtr.Zero) new WindowInteropHelper(window).Owner = h;
			return window.ShowDialog() == true ? Result.Succeeded : Result.Cancelled;
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

    public static bool ExecuteBypass(Document doc, Element runningMep, Element obstacle,
        Transform obstacleTransform, BypassShapeMode mode, BypassDirection direction, double clearanceMm,
        out string error, bool disconnectZEnd = false)
    {
        error = string.Empty;
        try
        {
            MEPCurve source = runningMep as MEPCurve;
            if (!(source is Pipe) && !(source is Duct))
                throw new InvalidOperationException("Chi ho tro Pipe va Duct thang (Round/Rectangular).");
            if (source.Document != doc || obstacle == null || (obstacle.Document == doc && obstacle.Id.Equals(source.Id)))
                throw new InvalidOperationException("Tuyen va vat can phai la hai phan tu khac nhau.");
            if (!AvoidClashGeometry.Finite(clearanceMm) || clearanceMm < 10)
                throw new InvalidOperationException("Khoang ho phai la so huu han, toi thieu 10 mm.");
            LocationCurve location = source.Location as LocationCurve;
            Line line = location?.Curve as Line;
            if (line == null) throw new InvalidOperationException("Chi ho tro tuyen co truc tim thang.");
            if (source.Pinned || !source.GroupId.Equals(ElementId.InvalidElementId))
                throw new InvalidOperationException("Hay bo Pin/Group cua tuyen truoc khi ne va cham.");
            XYZ start = line.GetEndPoint(0), end = line.GetEndPoint(1);
            XYZ axis = (end - start).Normalize();
            XYZ offsetDirection = AvoidClashGeometry.OffsetDirection(axis, direction);
            Connector startConnector = RequireEndAt(source, start), endConnector = RequireEndAt(source, end);
            if (Ends(source).Count() != 2 || source.ConnectorManager.Connectors.Cast<Connector>().Any(c =>
                c.ConnectorType != ConnectorType.End && c.ConnectorType != ConnectorType.Logical && c.IsConnected))
                throw new InvalidOperationException("Tuyen co nhanh/tap dang noi. Hay tach mot doan thang khong co nhanh de ne.");
            Profile profile = Profile.Read(doc, source, endConnector, axis);
            string[] originalStartLinks = LinkKeys(startConnector);
            Connector[] endLinks = PhysicalLinks(endConnector).ToArray();
            if (endLinks.Length > 1) throw new InvalidOperationException("Dau cuoi co nhieu ket noi; khong the thay the an toan.");
            EndLink remote = endLinks.Length == 0 ? null : new EndLink(endLinks[0]);
            if (mode == BypassShapeMode.Z45 && remote != null && !disconnectZEnd)
                throw new InvalidOperationException("Z45 doi vi tri dau cuoi. Chon 'Cho phep ngat dau cuoi' hoac dung U45/U90 de giu ket noi.");

            double clearance = clearanceMm / 304.8;
            XYZ transverse = axis.CrossProduct(offsetDirection).Normalize();
            double offsetReach = profile.Reach(offsetDirection, profile.X, profile.Y);
            double transverseReach = profile.Reach(transverse, profile.X, profile.Y);
            List<XYZ[]> obstacleBoxes = ObstacleBoxes(obstacle, obstacleTransform ?? Transform.Identity);
            double entry = double.MaxValue, exit = double.MinValue, high = double.MinValue;
            foreach (XYZ[] box in obstacleBoxes)
            {
                if (!AvoidClashGeometry.BoxInCorridor(box, start, axis, offsetDirection, transverseReach + clearance,
                    out double a, out double b, out double low, out double top)) continue;
                if (b < 0 || a > line.Length || low > offsetReach + clearance || top < -offsetReach - clearance) continue;
                entry = Math.Min(entry, a); exit = Math.Max(exit, b); high = Math.Max(high, top);
            }
            if (entry == double.MaxValue || exit - entry <= GeometryToleranceFeet)
                throw new InvalidOperationException("Khong tim thay vat can trong bao tiet dien cua tuyen duoc chon.");
            // Also reserve section reach before/after the obstacle, including the sloped segments.
            double margin = Math.Max(MinimumOffsetFeet, clearance + profile.BendReach);
            double offset = Math.Max(100.0 / 304.8, high + offsetReach + clearance);
            double minimumLength = Math.Max(0.05, doc.Application.ShortCurveTolerance * 1.01);
            XYZ[] points = AvoidClashGeometry.Build(start, end, offsetDirection, entry - margin, exit + margin,
                offset, mode, minimumLength);
            var segments = new List<MEPCurve>();
            var joints = new List<Joint>();
            var failures = new RollbackOnError();
            using (var group = new TransactionGroup(doc, "BIM - Avoid Clash " + mode))
            {
                if (group.Start() != TransactionStatus.Started) throw new InvalidOperationException("Khong the bat dau TransactionGroup.");
                try
                {
                    using (var transaction = new Transaction(doc, "BIM - Avoid Clash " + mode))
                    {
                        if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("Khong the bat dau Transaction.");
                        transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions()
                            .SetFailuresPreprocessor(failures).SetClearAfterRollback(true).SetForcedModalHandling(true));
                        try
                        {
                            if (remote != null) endConnector.DisconnectFrom(remote.Resolve(doc));
                            location.Curve = Line.CreateBound(points[0], points[1]);
                            segments.Add(source);
                            for (int i = 1; i < points.Length - 1; i++)
                                segments.Add(profile.Create(doc, points[i], points[i + 1], axis));
                            doc.Regenerate();
                            for (int i = 0; i < segments.Count - 1; i++)
                                joints.Add(ConnectWithElbow(doc, segments[i], segments[i + 1], points[i + 1]));
                            foreach (Joint joint in joints) profile.ApplyLayers(doc, doc.GetElement(joint.FittingId));
                            if (remote != null && mode != BypassShapeMode.Z45)
                            {
                                Connector replacement = RequireEndAt(segments.Last(), end);
                                Connector peer = remote.Resolve(doc);
                                if (replacement.IsConnected || peer.IsConnected)
                                    throw new InvalidOperationException("Dau cuoi da noi voi phan tu khac trong khi tao bypass.");
                                replacement.ConnectTo(peer);
                            }
                            doc.Regenerate();
                            Verify(doc, segments, joints, points, profile, axis, offsetDirection, obstacleBoxes, clearance,
                                originalStartLinks, remote, mode);
                            TransactionStatus status = transaction.Commit();
                            if (status != TransactionStatus.Committed)
                                throw new InvalidOperationException("Commit bypass that bai: " + status + ". " + failures.Description);
                        }
                        catch
                        {
                            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                            throw;
                        }
                    }
                    // End-of-transaction Revit checks can adjust geometry: validate again and roll back the group on failure.
                    Verify(doc, segments, joints, points, profile, axis, offsetDirection, obstacleBoxes, clearance,
                        originalStartLinks, remote, mode);
                    if (group.Assimilate() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Khong the hoan tat TransactionGroup bypass.");
                    return true;
                }
                catch
                {
                    if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool ExecuteBypass(Document doc, Element runningMep, Element obstacle, BypassShapeMode mode,
        BypassDirection direction, double clearanceMm, out string error)
        => ExecuteBypass(doc, runningMep, obstacle, Transform.Identity, mode, direction, clearanceMm, out error);

    // Retain the existing public entry points for callers using an angle.
    public static bool ExecuteBypass(Document doc, Element runningMep, Element obstacle, Transform transform,
        double angleDeg, BypassDirection direction, double clearanceMm, out string error)
    {
        if (angleDeg != 45 && angleDeg != 90) { error = "Chi ho tro goc 45/90 do."; return false; }
        return ExecuteBypass(doc, runningMep, obstacle, transform, angleDeg == 45 ? BypassShapeMode.U45 : BypassShapeMode.U90,
            direction, clearanceMm, out error);
    }

    public static bool ExecuteBypass(Document doc, Element runningMep, Element obstacle, double angleDeg,
        BypassDirection direction, double clearanceMm, out string error)
        => ExecuteBypass(doc, runningMep, obstacle, Transform.Identity, angleDeg, direction, clearanceMm, out error);

    private sealed class Profile
    {
        internal ElementId SystemId, TypeId, LevelId;
        internal double Diameter, OuterDiameter, Width, Height;
        internal XYZ X, Y;
        internal DuctProfileData DuctData;
        internal bool IsPipe;
        internal readonly List<Layer> Layers = new List<Layer>();
        internal double InsulationPadding => Layers.Where(layer => !layer.Lining).Sum(layer => layer.Thickness);
        internal double BendReach => OuterDiameter > 0 ? OuterDiameter * 0.5 + InsulationPadding
            : Math.Sqrt(Math.Pow(Width + 2 * InsulationPadding, 2) + Math.Pow(Height + 2 * InsulationPadding, 2)) * 0.5;
        internal double Reach(XYZ direction, XYZ x, XYZ y)
            => AvoidClashGeometry.SectionReach(direction, x, y, OuterDiameter > 0 ? OuterDiameter + 2 * InsulationPadding : 0,
                Width + 2 * InsulationPadding, Height + 2 * InsulationPadding);

        internal static Profile Read(Document doc, MEPCurve source, Connector forward, XYZ axis)
        {
            var p = new Profile { TypeId = source.GetTypeId(), LevelId = source.ReferenceLevel?.Id ?? source.LevelId,
                X = forward.CoordinateSystem.BasisX.Normalize(), Y = forward.CoordinateSystem.BasisY.Normalize(), IsPipe = source is Pipe };
            if (!(doc.GetElement(p.LevelId) is Level)) throw new InvalidOperationException("Khong xac dinh duoc Reference Level cua tuyen.");
            if (Math.Abs(axis.DotProduct(p.X)) > 1e-6 || Math.Abs(axis.DotProduct(p.Y)) > 1e-6)
                throw new InvalidOperationException("He truc tiet dien khong vuong goc voi tim tuyen.");
            if (source is Pipe pipe)
            {
                p.Diameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? 0;
                p.OuterDiameter = Math.Max(p.Diameter, pipe.get_Parameter(BuiltInParameter.RBS_PIPE_OUTER_DIAMETER)?.AsDouble() ?? 0);
                p.SystemId = pipe.MEPSystem?.GetTypeId() ?? pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId();
                if (p.SystemId == null || !(doc.GetElement(p.SystemId) is PipingSystemType)
                    || !AvoidClashGeometry.Finite(p.Diameter) || p.Diameter <= 0 || !AvoidClashGeometry.Finite(p.OuterDiameter))
                    throw new InvalidOperationException("Piping System Type/duong kinh ong khong hop le.");
            }
            else
            {
                Duct duct = (Duct)source;
                p.DuctData = DuctProfileData.Read(duct, forward);
                if (p.DuctData.Shape != ConnectorProfileType.Round && p.DuctData.Shape != ConnectorProfileType.Rectangular)
                    throw new InvalidOperationException("Chi ho tro Round Duct va Rectangular Duct.");
                p.SystemId = DuctProfileData.SystemTypeId(doc, duct, forward);
                p.Diameter = p.OuterDiameter = p.DuctData.Diameter;
                p.Width = p.DuctData.Width; p.Height = p.DuctData.Height;
            }
            foreach (ElementId id in InsulationLiningBase.GetInsulationIds(doc, source.Id))
                p.Layers.Add(Layer.Read((InsulationLiningBase)doc.GetElement(id), false));
            if (source is Duct)
                foreach (ElementId id in InsulationLiningBase.GetLiningIds(doc, source.Id))
                    p.Layers.Add(Layer.Read((InsulationLiningBase)doc.GetElement(id), true));
            if (p.Layers.Count(layer => layer.Lining) > 1 || p.Layers.Count(layer => !layer.Lining) > 1)
                throw new InvalidOperationException("Tuyen co nhieu lop bao on/lot cung loai; khong the sao chep an toan.");
            return p;
        }

        internal void ApplyLayers(Document doc, Element host)
        {
            foreach (Layer layer in Layers) layer.Apply(doc, host, IsPipe);
        }

        internal XYZ SectionX(XYZ sourceAxis, XYZ targetAxis)
            => sourceAxis.DotProduct(targetAxis) >= 1 - 1e-10 ? X : ElbowGeometry.Transport(X, sourceAxis, targetAxis);

        internal MEPCurve Create(Document doc, XYZ start, XYZ end, XYZ originalAxis)
        {
            MEPCurve result;
            if (IsPipe)
            {
                result = Pipe.Create(doc, SystemId, TypeId, LevelId, start, end);
                Parameter diameter = result.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                if (diameter == null || diameter.IsReadOnly) throw new InvalidOperationException("Khong the gan duong kinh ong moi.");
                if (Math.Abs(diameter.AsDouble() - Diameter) > GeometryToleranceFeet) diameter.Set(Diameter);
            }
            else
            {
                Duct duct = Duct.Create(doc, SystemId, TypeId, LevelId, start, end);
                DuctData.Apply(duct);
                result = duct;
                doc.Regenerate();
                if (DuctData.Shape == ConnectorProfileType.Rectangular)
                {
                    XYZ segmentAxis = (end - start).Normalize();
                    XYZ x = RequireEndAt(duct, end).CoordinateSystem.BasisX;
                    double roll = ElbowGeometry.RollAngle(x, SectionX(originalAxis, segmentAxis), segmentAxis);
                    if (Math.Abs(roll) > 1e-9) ElementTransformUtils.RotateElement(doc, duct.Id, Line.CreateBound(start, end), roll);
                    doc.Regenerate();
                }
            }
            ApplyLayers(doc, result);
            return result;
        }

        internal void Verify(Document doc, MEPCurve curve, XYZ originalAxis, XYZ segmentAxis)
        {
            Connector connector = Ends(curve).First();
            ElementId systemId;
            if (curve is Duct duct)
            {
                DuctData.Verify(duct, connector);
                systemId = DuctProfileData.SystemTypeId(doc, duct, connector);
                if (DuctData.Shape == ConnectorProfileType.Rectangular
                    && Math.Abs(connector.CoordinateSystem.BasisX.Normalize().DotProduct(SectionX(originalAxis, segmentAxis))) < AxisDotTolerance)
                    throw new InvalidOperationException("Revit da doi huong xoay tiet dien Duct chu nhat.");
            }
            else
            {
                if (Math.Abs(curve.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).AsDouble() - Diameter) > GeometryToleranceFeet)
                    throw new InvalidOperationException("Revit da doi duong kinh ong.");
                systemId = curve.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId();
            }
            if (!TypeId.Equals(curve.GetTypeId()) || !SystemId.Equals(systemId) || !LevelId.Equals(curve.ReferenceLevel?.Id))
                throw new InvalidOperationException("Type, System Type hoac Reference Level cua tuyen da thay doi.");
            foreach (Layer layer in Layers) layer.Verify(doc, curve);
        }
    }

    private sealed class Layer
    {
        internal ElementId TypeId;
        internal double Thickness;
        internal bool Lining;
        internal static Layer Read(InsulationLiningBase element, bool lining)
        {
            if (element == null || !AvoidClashGeometry.Finite(element.Thickness) || element.Thickness <= 0)
                throw new InvalidOperationException("Lop bao on/lot cua tuyen khong hop le.");
            return new Layer { TypeId = element.GetTypeId(), Thickness = element.Thickness, Lining = lining };
        }
        private ICollection<ElementId> Ids(Document doc, Element host) => Lining
            ? InsulationLiningBase.GetLiningIds(doc, host.Id) : InsulationLiningBase.GetInsulationIds(doc, host.Id);
        internal void Apply(Document doc, Element host, bool pipe)
        {
            ICollection<ElementId> ids = Ids(doc, host);
            InsulationLiningBase layer;
            if (ids.Count == 0)
            {
                if (Lining) layer = DuctLining.Create(doc, host.Id, TypeId, Thickness);
                else if (pipe) layer = PipeInsulation.Create(doc, host.Id, TypeId, Thickness);
                else layer = DuctInsulation.Create(doc, host.Id, TypeId, Thickness);
            }
            else
            {
                if (ids.Count != 1) throw new InvalidOperationException("Bao on/lot cua doan moi khong duy nhat.");
                layer = (InsulationLiningBase)doc.GetElement(ids.Single());
                if (!TypeId.Equals(layer.GetTypeId()))
                {
                    ElementId previousId = layer.Id;
                    ElementId changedId = layer.ChangeTypeId(TypeId);
                    layer = (InsulationLiningBase)doc.GetElement(changedId.Equals(ElementId.InvalidElementId) ? previousId : changedId);
                }
                if (Math.Abs(layer.Thickness - Thickness) > GeometryToleranceFeet) layer.Thickness = Thickness;
            }
            Verify(doc, host);
        }
        internal void Verify(Document doc, Element host)
        {
            ICollection<ElementId> ids = Ids(doc, host);
            if (ids.Count != 1) throw new InvalidOperationException("Khong giu duoc lop bao on/lot tren bypass.");
            var actual = doc.GetElement(ids.Single()) as InsulationLiningBase;
            if (actual == null || !TypeId.Equals(actual.GetTypeId()) || Math.Abs(actual.Thickness - Thickness) > GeometryToleranceFeet)
                throw new InvalidOperationException("Type/chieu day bao on hoac lop lot da thay doi.");
        }
    }

    private sealed class EndLink
    {
        internal readonly ElementId OwnerId;
        internal readonly int ConnectorId;
        internal readonly XYZ Origin;
        internal EndLink(Connector connector) { OwnerId = connector.Owner.Id; ConnectorId = connector.Id; Origin = connector.Origin; }
        internal Connector Resolve(Document doc)
        {
            Element owner = doc.GetElement(OwnerId);
            ConnectorManager manager = (owner as MEPCurve)?.ConnectorManager ?? (owner as FamilyInstance)?.MEPModel?.ConnectorManager;
            Connector result = manager?.Connectors.Cast<Connector>().FirstOrDefault(c => c.Id == ConnectorId);
            if (result == null || result.Origin.DistanceTo(Origin) > ConnectorToleranceFeet)
                throw new InvalidOperationException("Dau noi ngoai tuyen bi mat hoac da di chuyen.");
            return result;
        }
    }

    private sealed class Joint
    {
        internal ElementId FirstId, SecondId, FittingId;
        internal int FirstEndId, SecondEndId;
        internal void Verify(Document doc)
        {
            Connector a = EndById((MEPCurve)doc.GetElement(FirstId), FirstEndId);
            Connector b = EndById((MEPCurve)doc.GetElement(SecondId), SecondEndId);
            var fitting = doc.GetElement(FittingId) as FamilyInstance;
            if (fitting?.MEPModel == null) throw new InvalidOperationException("Co bypass khong ton tai.");
            Connector[] ends = fitting.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                .Where(c => c.ConnectorType == ConnectorType.End).ToArray();
            if (ends.Length != 2 || !ends.Any(c => c.IsConnectedTo(a)) || !ends.Any(c => c.IsConnectedTo(b)))
                throw new InvalidOperationException("Khong tao duoc ket noi MEP <-> Elbow <-> MEP. Kiem tra Routing Preferences va family co.");
        }
    }

    private static Joint ConnectWithElbow(Document doc, MEPCurve first, MEPCurve second, XYZ corner)
    {
        Connector a = RequireEndAt(first, corner), b = RequireEndAt(second, corner);
        if (a.IsConnected || b.IsConnected) throw new InvalidOperationException("Dau noi tai goc bypass da bi chiem.");
        var joint = new Joint { FirstId = first.Id, SecondId = second.Id, FirstEndId = a.Id, SecondEndId = b.Id };
        if (first is Duct da && second is Duct db)
        {
            // This legacy helper swallows exceptions; success is decided by connector topology below.
            MEPLibrary.CreateElbowFitting(doc, da, db);
        }
        else
        {
            FamilyInstance fitting = doc.Create.NewElbowFitting(a, b);
            if (fitting == null) throw new InvalidOperationException("Revit khong tra ve Pipe Elbow.");
        }
        doc.Regenerate();
        a = EndById(first, joint.FirstEndId); b = EndById(second, joint.SecondEndId);
        Connector common = PhysicalLinks(a).FirstOrDefault(c => PhysicalLinks(b).Any(d => c.Owner.Id.Equals(d.Owner.Id)));
        if (common == null) throw new InvalidOperationException("Khong tao duoc co tai " + corner + ". Kiem tra family 45/90 do, kich thuoc va chieu dai lap co.");
        joint.FittingId = common.Owner.Id;
        joint.Verify(doc);
        return joint;
    }

    private static void Verify(Document doc, IList<MEPCurve> segments, IList<Joint> joints, XYZ[] points,
        Profile profile, XYZ axis, XYZ offsetDirection, List<XYZ[]> obstacleBoxes, double clearance,
        string[] startLinks, EndLink remote, BypassShapeMode mode)
    {
        XYZ origin = points[0], transverse = axis.CrossProduct(offsetDirection).Normalize();
        for (int i = 0; i < segments.Count; i++)
        {
            MEPCurve curve = (MEPCurve)doc.GetElement(segments[i].Id);
            Line actual = (curve.Location as LocationCurve)?.Curve as Line;
            XYZ expectedAxis = (points[i + 1] - points[i]).Normalize();
            if (actual == null || actual.Direction.DotProduct(expectedAxis) < AxisDotTolerance)
                throw new InvalidOperationException("Revit da thay doi huong doan bypass/goc co.");
            for (int j = 0; j < 2; j++)
            {
                XYZ delta = actual.GetEndPoint(j) - points[i];
                double t = delta.DotProduct(expectedAxis);
                if ((delta - expectedAxis * t).GetLength() > ConnectorToleranceFeet || t < -ConnectorToleranceFeet
                    || t > (points[i + 1] - points[i]).GetLength() + ConnectorToleranceFeet)
                    throw new InvalidOperationException("Revit da day doan bypass ra ngoai truc du kien.");
            }
            profile.Verify(doc, curve, axis, expectedAxis);
            XYZ x = profile.SectionX(axis, expectedAxis), y = expectedAxis.CrossProduct(x).Normalize();
            double reachN = profile.Reach(axis, x, y), reachU = profile.Reach(offsetDirection, x, y), reachV = profile.Reach(transverse, x, y);
            XYZ a = actual.GetEndPoint(0) - origin, b = actual.GetEndPoint(1) - origin;
            VerifyEnvelope(obstacleBoxes, origin, axis, offsetDirection, clearance,
                Math.Min(a.DotProduct(axis), b.DotProduct(axis)) - reachN, Math.Max(a.DotProduct(axis), b.DotProduct(axis)) + reachN,
                Math.Min(a.DotProduct(offsetDirection), b.DotProduct(offsetDirection)) - reachU, Math.Max(a.DotProduct(offsetDirection), b.DotProduct(offsetDirection)) + reachU,
                Math.Min(a.DotProduct(transverse), b.DotProduct(transverse)) - reachV, Math.Max(a.DotProduct(transverse), b.DotProduct(transverse)) + reachV);
        }
        foreach (Joint joint in joints)
        {
            joint.Verify(doc);
            foreach (Layer layer in profile.Layers) layer.Verify(doc, doc.GetElement(joint.FittingId));
            foreach (XYZ[] box in ObstacleBoxes(doc.GetElement(joint.FittingId), Transform.Identity))
            {
                Range(box, origin, axis, out double a, out double b);
                Range(box, origin, offsetDirection, out double c, out double d);
                Range(box, origin, transverse, out double e, out double f);
                VerifyEnvelope(obstacleBoxes, origin, axis, offsetDirection, clearance, a, b, c, d, e, f);
            }
        }
        Connector start = RequireEndAt(segments[0], points[0]), end = RequireEndAt(segments.Last(), points.Last());
        if (!LinkKeys(start).SequenceEqual(startLinks)) throw new InvalidOperationException("Ket noi dau dau cua tuyen da thay doi.");
        string[] expectedEnd = remote != null && mode != BypassShapeMode.Z45
            ? new[] { remote.OwnerId + ":" + remote.ConnectorId } : new string[0];
        if (!LinkKeys(end).SequenceEqual(expectedEnd)) throw new InvalidOperationException("Ket noi dau cuoi cua tuyen khong dung du kien.");
        if (remote != null)
        {
            Connector peer = remote.Resolve(doc);
            if (mode == BypassShapeMode.Z45 && peer.IsConnected)
                throw new InvalidOperationException("Dau noi cu cua Z45 khong duoc ngat an toan.");
        }
    }

    // A conservative separating-plane check includes the fitting envelope, not just its centreline.
    // Rejecting ambiguous envelopes is safer than committing a family that clips the selected beam.
    private static void VerifyEnvelope(List<XYZ[]> boxes, XYZ origin, XYZ axis, XYZ u, double clearance,
        double minN, double maxN, double minU, double maxU, double minV, double maxV)
    {
        XYZ v = axis.CrossProduct(u).Normalize();
        XYZ corridorOrigin = origin + v * ((minV + maxV) * 0.5);
        foreach (XYZ[] box in boxes)
        {
            if (!AvoidClashGeometry.BoxInCorridor(box, corridorOrigin, axis, u, (maxV - minV) * 0.5 + clearance,
                out double entry, out double exit, out double low, out double high)) continue;
            if (minN - exit >= clearance - ConnectorToleranceFeet || entry - maxN >= clearance - ConnectorToleranceFeet
                || minU - high >= clearance - ConnectorToleranceFeet || low - maxU >= clearance - ConnectorToleranceFeet) continue;
            throw new InvalidOperationException("Bao tiet dien/co bypass chua dam bao khoang ho voi vat can. Hay tang Clearance, doi huong hoac chon family co gon hon.");
        }
    }

    private static void Range(XYZ[] corners, XYZ origin, XYZ direction, out double min, out double max)
    {
        min = corners.Min(p => (p - origin).DotProduct(direction));
        max = corners.Max(p => (p - origin).DotProduct(direction));
    }

    private static List<XYZ[]> ObstacleBoxes(Element element, Transform transform)
    {
        var boxes = new List<XYZ[]>();
        Options options = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false, IncludeNonVisibleObjects = false };
        var hosts = new List<Element> { element };
        int category = element.Category?.Id.GetIdInt() ?? 0;
        if (element is Pipe || element is Duct || category == (int)BuiltInCategory.OST_PipeFitting || category == (int)BuiltInCategory.OST_DuctFitting)
            foreach (ElementId id in InsulationLiningBase.GetInsulationIds(element.Document, element.Id))
                hosts.Add(element.Document.GetElement(id));
        foreach (Element host in hosts)
        {
            int previousCount = boxes.Count;
            foreach (Solid solid in GetSolids(host.get_Geometry(options)))
            {
                BoundingBoxXYZ box = solid.GetBoundingBox();
                if (box != null) boxes.Add(BoxCorners(box, transform));
            }
            if (boxes.Count == previousCount)
            {
                BoundingBoxXYZ box = host.get_BoundingBox(null);
                if (box != null) boxes.Add(BoxCorners(box, transform));
                else throw new InvalidOperationException("Khong doc duoc bao hinh hoc cua " + host.Name + ".");
            }
        }
        if (boxes.Count == 0) throw new InvalidOperationException("Khong doc duoc bao hinh hoc cua " + element.Name + ".");
        return boxes;
    }

    private static IEnumerable<Solid> GetSolids(GeometryElement geometry)
    {
        if (geometry == null) yield break;
        foreach (GeometryObject item in geometry)
        {
            if (item is Solid solid && solid.Faces.Size > 0 && solid.Edges.Size > 0) yield return solid;
            if (item is GeometryInstance instance)
                foreach (Solid nested in GetSolids(instance.GetInstanceGeometry())) yield return nested;
        }
    }

    private static XYZ[] BoxCorners(BoundingBoxXYZ box, Transform external)
    {
        Transform transform = external.Multiply(box.Transform ?? Transform.Identity);
        var corners = new XYZ[8];
        for (int i = 0; i < 8; i++) corners[i] = transform.OfPoint(new XYZ(
            (i & 4) == 0 ? box.Min.X : box.Max.X, (i & 2) == 0 ? box.Min.Y : box.Max.Y, (i & 1) == 0 ? box.Min.Z : box.Max.Z));
        return corners;
    }

    private static IEnumerable<Connector> Ends(MEPCurve curve)
        => curve.ConnectorManager.Connectors.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End);
    private static Connector EndById(MEPCurve curve, int id)
        => Ends(curve).Single(c => c.Id == id);
    private static Connector RequireEndAt(MEPCurve curve, XYZ point)
    {
        Connector end = Ends(curve).OrderBy(c => c.Origin.DistanceTo(point)).FirstOrDefault();
        if (end == null || end.Origin.DistanceTo(point) > ConnectorToleranceFeet)
            throw new InvalidOperationException("Khong tim thay dau noi MEP tai vi tri du kien.");
        return end;
    }
    private static IEnumerable<Connector> PhysicalLinks(Connector connector)
        => connector.AllRefs.Cast<Connector>().Where(c => c.ConnectorType == ConnectorType.End
            && !c.Owner.Id.Equals(connector.Owner.Id) && connector.IsConnectedTo(c));
    private static string[] LinkKeys(Connector connector)
        => PhysicalLinks(connector).Select(c => c.Owner.Id + ":" + c.Id).OrderBy(s => s, StringComparer.Ordinal).ToArray();

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

    private sealed class AvoidClashMepSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element) => element is Pipe || element is Duct;
        public bool AllowReference(Reference reference, XYZ position) => true;
    }
	private sealed class AvoidClashObstacleSelectionFilter : ISelectionFilter
	{
		private readonly Document _doc;

		public AvoidClashObstacleSelectionFilter(Document doc)
		{
			_doc = doc;
		}

		public bool AllowElement(Element element)
		{
			if (element == null) return false;
			if (element is RevitLinkInstance || element.Category?.Id.GetIdInt() == (int)BuiltInCategory.OST_RvtLinks) return true;
			return IsValidObstacleCategory(element.Category);
		}

		public bool AllowReference(Reference reference, XYZ position)
		{
			if (reference == null) return false;
			if (reference.LinkedElementId != null && reference.LinkedElementId != ElementId.InvalidElementId)
			{
				Element hostElem = _doc.GetElement(reference.ElementId);
				if (hostElem is RevitLinkInstance linkInstance)
				{
					Document linkDoc = linkInstance.GetLinkDocument();
					if (linkDoc != null)
					{
						Element linkedElem = linkDoc.GetElement(reference.LinkedElementId);
						return linkedElem != null && IsValidObstacleCategory(linkedElem.Category);
					}
				}
				return false;
			}

			Element elem = _doc.GetElement(reference.ElementId);
			if (elem is RevitLinkInstance) return true;
			return elem != null && IsValidObstacleCategory(elem.Category);
		}

		public static bool IsValidObstacleCategory(Category category)
		{
			if (category == null) return false;
			int categoryId = category.Id.GetIdInt();
			return categoryId == (int)BuiltInCategory.OST_StructuralFraming
				|| categoryId == (int)BuiltInCategory.OST_StructuralColumns
				|| categoryId == (int)BuiltInCategory.OST_StructuralFoundation
				|| categoryId == (int)BuiltInCategory.OST_DuctCurves
				|| categoryId == (int)BuiltInCategory.OST_PipeCurves
				|| categoryId == (int)BuiltInCategory.OST_CableTray
				|| categoryId == (int)BuiltInCategory.OST_Walls
				|| categoryId == (int)BuiltInCategory.OST_Floors;
		}
	}
}
