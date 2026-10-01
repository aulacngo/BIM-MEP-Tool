using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace BIN;

/// <summary>The eight Ribbon commands' duct path. Never falls back to ConnectTo.</summary>
internal static class DuctElbowBuilder
{
    private const double PositionTolerance = 1e-5;
    // Revit can make a small geometric adjustment while placing a routing-preference fitting.
    // 0.5 degrees still rejects a wrong elbow direction, without rejecting valid geometry
    // because of floating-point/trigonometric noise.
    private const double AxisDotTolerance = 0.9999619230641713; // cos(0.5 degrees)

    internal static Connector PickEnd(Duct duct, XYZ pickedPoint)
    {
        Connector connector = Ends(duct).OrderBy(c => c.Origin.DistanceTo(pickedPoint)).FirstOrDefault();
        if (connector == null) throw new InvalidOperationException("No physical HVAC end connector was found.");
        // Respect the clicked end, including Down45; never silently switch to the other end.
        if (connector.IsConnected)
            throw new InvalidOperationException("The selected duct end is connected. Pick an open end.");
        return connector;
    }

    internal static void Execute(Document doc, Duct source, XYZ pickedPoint, ElbowDirection direction)
    {
        Connector connector = PickEnd(source, pickedPoint);
        Transform frame = connector.CoordinateSystem;
        XYZ axis = ElbowGeometry.Direction(frame.BasisZ, frame.BasisX, frame.BasisY, direction);
        double length = NaviateHelper.GetExtensionLength(connector);
        XYZ start = connector.Origin;
        XYZ end = start + axis * length;
        var failures = new RollbackOnError();
        string name = "BIN Duct Elbow " + direction;
        // The group also rolls back if post-commit validation fails, and produces one Undo item.
        using (var group = new TransactionGroup(doc, name))
        {
            if (group.Start() != TransactionStatus.Started)
                throw new InvalidOperationException("Cannot start the duct elbow transaction group.");
            try
            {
                BuildResult result;
                using (var transaction = new Transaction(doc, name))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("Cannot start the duct elbow transaction.");
                    transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions()
                        .SetFailuresPreprocessor(failures).SetClearAfterRollback(true).SetForcedModalHandling(true));
                    try
                    {
                        result = Build(doc, source, connector, start, end);
                        TransactionStatus status = transaction.Commit();
                        SimpleLogger.Log($"{name}: commit={status}; {failures.Description}");
                        if (status != TransactionStatus.Committed)
                            throw new InvalidOperationException("Duct elbow commit failed: " + status + ". " + failures.Description);
                    }
                    catch
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                        throw;
                    }
                }
                result.Verify(doc);
                if (group.Assimilate() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Cannot finalize the duct elbow transaction group.");
                SimpleLogger.Log($"{name}: verified and committed; duct={result.NewId}; elbow={result.FittingId}");
            }
            catch
            {
                if (group.GetStatus() == TransactionStatus.Started)
                    SimpleLogger.Log($"{name}: group rollback={group.RollBack()}");
                throw;
            }
        }
    }

    // Requires an active transaction. A subtransaction protects other callers of NaviateHelper.DrawDuct.
    internal static BuildResult Build(Document doc, Duct source, Connector sourceConnector, XYZ start, XYZ end)
    {
        string stage = "preflight";
        string context = "source=" + source.Id;
        using (var scope = new SubTransaction(doc))
        {
            if (scope.Start() != TransactionStatus.Started)
                throw new InvalidOperationException("Cannot start the duct elbow operation.");
            try
            {
                if (!(source.Location is LocationCurve location) || !(location.Curve is Line))
                    throw new InvalidOperationException("Only straight rigid ducts are supported.");
                if (!sourceConnector.Owner.Id.Equals(source.Id) || sourceConnector.IsConnected ||
                    sourceConnector.Domain != Domain.DomainHvac || sourceConnector.ConnectorType != ConnectorType.End ||
                    sourceConnector.Origin.DistanceTo(start) > PositionTolerance)
                    throw new InvalidOperationException("The source must be the selected open HVAC end.");
                if (end.DistanceTo(start) < Math.Max(doc.Application.ShortCurveTolerance * 2, 0.1))
                    throw new InvalidOperationException("The new duct would be too short.");

                Transform frame = sourceConnector.CoordinateSystem;
                XYZ n = frame.BasisZ.Normalize();
                XYZ d = (end - start).Normalize();
                double angle = n.AngleTo(d);
                if (Math.Abs(angle - Math.PI / 2) > 1e-6 && Math.Abs(angle - Math.PI / 4) > 1e-6)
                    throw new InvalidOperationException("The duct elbow must turn exactly 45 or 90 degrees.");
                var profile = DuctProfileData.Read(source, sourceConnector);
                ElementId systemId = DuctProfileData.SystemTypeId(doc, source, sourceConnector);
                ElementId levelId = DuctProfileData.LevelId(doc, source, start.Z);
                Connector[] ends = Ends(source);
                if (ends.Length != 2) throw new InvalidOperationException("The source duct must have exactly two physical ends.");
                Connector far = ends.Single(c => c.Id != sourceConnector.Id);
                var result = new BuildResult
                {
                    SourceId = source.Id, SourceEndId = sourceConnector.Id, FarEndId = far.Id,
                    FarOrigin = far.Origin, FarLinks = Links(far), SourceAxis = n,
                    SourceX = frame.BasisX, SourceY = frame.BasisY,
                    NewEnd = end, TargetAxis = d,
                    TargetX = ElbowGeometry.Transport(frame.BasisX, n, d),
                    TargetY = ElbowGeometry.Transport(frame.BasisY, n, d),
                    Profile = profile, TypeId = source.GetTypeId(), SystemId = systemId, LevelId = levelId,
                    SourceReferenceLevelId = source.ReferenceLevel?.Id
                };
                context += $"; {profile}; type={result.TypeId}; system={systemId}; level={levelId}; " +
                    $"start={start}; end={end}; axis={n}; sourceX={frame.BasisX}; sourceY={frame.BasisY}; targetX={result.TargetX}";
                SimpleLogger.Log("DuctElbow preflight: " + context);

                stage = "create duct and set size";
                Duct created = Duct.Create(doc, systemId, result.TypeId, levelId, start, end);
                result.NewId = created.Id;
                profile.Apply(created);
                doc.Regenerate();
                Connector newConnector = EndAt(created, start);
                profile.Verify(created, newConnector);

                stage = "align cross-section roll";
                if (profile.Shape != ConnectorProfileType.Round)
                {
                    double roll = ElbowGeometry.RollAngle(newConnector.CoordinateSystem.BasisX, result.TargetX, d);
                    if (Math.Abs(roll) > 1e-9)
                        ElementTransformUtils.RotateElement(doc, created.Id, Line.CreateBound(start, end), roll);
                    doc.Regenerate();
                    newConnector = EndAt(created, start);
                    VerifyFrame(newConnector, result.TargetX, result.TargetY);
                }

                stage = "create elbow (check routing preferences, angle, size and available straight length)";
                Connector refreshedSource = EndById(source, result.SourceEndId);
                FamilyInstance elbow = doc.Create.NewElbowFitting(refreshedSource, newConnector);
                if (elbow == null) throw new InvalidOperationException("Revit did not return an elbow fitting.");
                result.FittingId = elbow.Id;
                CopyComments(source, created, elbow);
                doc.Regenerate();

                stage = "verify duct-elbow-duct topology, profile and remote end";
                result.Verify(doc);
                if (scope.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Duct elbow subtransaction was not committed.");
                return result;
            }
            catch (Exception exception)
            {
                // Do not inspect model data here: Regenerate may have failed.
                if (scope.GetStatus() == TransactionStatus.Started) scope.RollBack();
                SimpleLogger.Log($"DuctElbow failed at {stage}; {context}; {exception}");
                throw new InvalidOperationException($"Duct elbow failed at {stage}: {exception.Message}", exception);
            }
        }
    }

    internal sealed class BuildResult
    {
        internal ElementId SourceId, NewId, FittingId, TypeId, SystemId, LevelId, SourceReferenceLevelId;
        internal int SourceEndId, FarEndId;
        internal XYZ FarOrigin, SourceAxis, SourceX, SourceY, NewEnd, TargetAxis, TargetX, TargetY;
        internal string[] FarLinks;
        internal DuctProfileData Profile;

        internal void Verify(Document doc)
        {
            Duct source = doc.GetElement(SourceId) as Duct;
            Duct created = doc.GetElement(NewId) as Duct;
            FamilyInstance fitting = doc.GetElement(FittingId) as FamilyInstance;
            if (source == null || created == null || fitting?.MEPModel == null)
                throw new InvalidOperationException("An expected duct or elbow no longer exists.");
            Connector sourceEnd = EndById(source, SourceEndId);
            Connector far = EndById(source, FarEndId);
            Connector[] createdEnds = Ends(created);
            if (createdEnds.Length != 2)
                throw new InvalidOperationException("The new duct no longer has exactly two physical ends.");

            // After NewElbowFitting, topology is more reliable than the original start point:
            // Revit may trim the near end to accommodate the fitting.  The remote end must be
            // the only remaining open end, and it must remain at the requested endpoint.
            Connector newFar = createdEnds.SingleOrDefault(c => !c.IsConnected);
            if (newFar == null || newFar.Origin.DistanceTo(NewEnd) > PositionTolerance)
                throw new InvalidOperationException("Cannot identify the expected open remote end of the new duct.");
            Connector newNear = createdEnds.Single(c => c.Id != newFar.Id);
            Connector[] fittingEnds = fitting.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
                .Where(c => c.ConnectorType == ConnectorType.End && c.Domain == Domain.DomainHvac).ToArray();
            if (fittingEnds.Length != 2 ||
                !fittingEnds.Any(c => c.IsConnectedTo(sourceEnd)) ||
                !fittingEnds.Any(c => c.IsConnectedTo(newNear)) || newFar.IsConnected)
                throw new InvalidOperationException("Expected source duct <-> elbow <-> new duct connections were not established.");
            if (far.Origin.DistanceTo(FarOrigin) > PositionTolerance || !Links(far).SequenceEqual(FarLinks))
                throw new InvalidOperationException("The source remote end or its external connections changed.");
            if (!MatchesAxis(sourceEnd.CoordinateSystem.BasisZ, SourceAxis) ||
                !MatchesAxis(newFar.CoordinateSystem.BasisZ, TargetAxis))
                throw new InvalidOperationException("Revit changed the planned duct direction or elbow angle.");
            Profile.Verify(source, sourceEnd);
            Profile.Verify(created, newNear);
            if (!created.GetTypeId().Equals(TypeId) || !source.GetTypeId().Equals(TypeId) ||
                !DuctProfileData.SystemTypeId(doc, created, newNear).Equals(SystemId) ||
                !DuctProfileData.SystemTypeId(doc, source, sourceEnd).Equals(SystemId) ||
                created.ReferenceLevel == null || !created.ReferenceLevel.Id.Equals(LevelId) ||
                (SourceReferenceLevelId != null && !SourceReferenceLevelId.Equals(source.ReferenceLevel?.Id)))
                throw new InvalidOperationException("Duct type, system type or reference level changed.");
            if (Profile.Shape != ConnectorProfileType.Round)
            {
                VerifyFrame(sourceEnd, SourceX, SourceY);
                VerifyFrame(newNear, TargetX, TargetY);
            }
        }
    }

    private static Connector[] Ends(Duct duct) => duct.ConnectorManager.Connectors.Cast<Connector>()
        .Where(c => c.ConnectorType == ConnectorType.End && c.Domain == Domain.DomainHvac).ToArray();

    private static Connector EndById(Duct duct, int id) => Ends(duct).Single(c => c.Id == id);

    private static Connector EndAt(Duct duct, XYZ point)
    {
        Connector connector = Ends(duct).OrderBy(c => c.Origin.DistanceTo(point)).FirstOrDefault();
        if (connector == null || connector.Origin.DistanceTo(point) > PositionTolerance)
            throw new InvalidOperationException("Cannot locate the expected duct end after regeneration.");
        return connector;
    }

    private static bool MatchesAxis(XYZ actual, XYZ expected) =>
        actual != null && expected != null &&
        actual.Normalize().DotProduct(expected.Normalize()) >= AxisDotTolerance;

    private static string[] Links(Connector connector) => connector.AllRefs.Cast<Connector>()
        .Where(c => c.ConnectorType == ConnectorType.End && connector.IsConnectedTo(c))
        .Select(c => c.Owner.Id + ":" + c.Id).OrderBy(s => s, StringComparer.Ordinal).ToArray();

    private static void VerifyFrame(Connector connector, XYZ x, XYZ y)
    {
        Transform frame = connector.CoordinateSystem;
        if (Math.Abs(frame.BasisX.Normalize().DotProduct(x.Normalize())) < 1 - 1e-8 ||
            Math.Abs(frame.BasisY.Normalize().DotProduct(y.Normalize())) < 1 - 1e-8)
            throw new InvalidOperationException("Duct cross-section roll does not match the transported source frame.");
    }

    private static void CopyComments(Element source, params Element[] targets)
    {
        string value = source.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString();
        if (string.IsNullOrEmpty(value)) return;
        foreach (Element target in targets)
        {
            Parameter parameter = target.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
            if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.String &&
                parameter.AsString() != value && !parameter.Set(value))
                throw new InvalidOperationException("Cannot copy duct comments.");
        }
    }

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
}
