using System;
using System.Collections.Generic;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class AvoidClashCmd : IExternalCommand
{
	private const double ConnectorToleranceFeet = 1e-4;
	private const double GeometryToleranceFeet = 1e-6;
	private const double MinimumOffsetFeet = 50.0 / 304.8;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			Reference refPipe = uidoc.Selection.PickObject(
				ObjectType.Element,
				new AvoidClashPipeSelectionFilter(),
				"1. Click chon ONG can uon de ne va cham (Running Pipe)");
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
			window.ShowDialog();
			return Result.Succeeded;
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

	public static bool ExecuteBypass(Document doc, Element runningPipeElem, Element obstacleElem,
		Transform obstacleTransform, double angleDeg, BypassDirection dir, double clearanceMm, out string error)
	{
		error = string.Empty;
		obstacleTransform = obstacleTransform ?? Transform.Identity;
		Pipe pipe = runningPipeElem as Pipe;
		if (pipe == null)
		{
			error = "Doi tuong can uon khong phai la Pipe.";
			return false;
		}

		LocationCurve locCurve = pipe.Location as LocationCurve;
		Line pipeLine = locCurve?.Curve as Line;
		if (pipeLine == null)
		{
			error = "Khong lay duoc truc tim dang thang cua ong.";
			return false;
		}

		XYZ pStart = pipeLine.GetEndPoint(0);
		XYZ pEnd = pipeLine.GetEndPoint(1);
		double pipeLength = pipeLine.Length;
		if (pipeLength <= GeometryToleranceFeet)
		{
			error = "Doan ong duoc chon qua ngan.";
			return false;
		}

		XYZ pipeDir = (pEnd - pStart).Normalize();
		double pipeDiameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? (50.0 / 304.8);
		double pipeRadius = pipeDiameter * 0.5;
		ElementId sysTypeId = pipe.MEPSystem?.GetTypeId()
			?? pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId();
		if (sysTypeId == null || sysTypeId == ElementId.InvalidElementId)
		{
			error = "Khong xac dinh duoc Piping System Type cua ong.";
			return false;
		}

		ElementId pipeTypeId = pipe.PipeType.Id;
		ElementId levelId = pipe.ReferenceLevel?.Id ?? pipe.LevelId;
		if (levelId == null || levelId == ElementId.InvalidElementId)
		{
			error = "Khong xac dinh duoc Level cua ong.";
			return false;
		}

		if (!GetCrossingSpan(pipe, obstacleElem, obstacleTransform, out XYZ pEntry, out XYZ pExit, out double obsBottomZ, out double obsTopZ))
		{
			error = "Khong xac dinh duoc vi tri ong cat qua vat can. Hay chon vat can co hinh hoc hop le.";
			return false;
		}

		double clearanceFeet = clearanceMm / 304.8;
		double spanMargin = Math.Max(clearanceFeet, MinimumOffsetFeet);
		XYZ pClearEntry = pEntry - pipeDir * spanMargin;
		XYZ pClearExit = pExit + pipeDir * spanMargin;

		XYZ uDir;
		double vOffset;
		if (dir == BypassDirection.Down)
		{
			double targetZ = obsBottomZ - pipeRadius - clearanceFeet;
			vOffset = pStart.Z - targetZ;
			if (vOffset < MinimumOffsetFeet) vOffset = 100.0 / 304.8;
			uDir = -XYZ.BasisZ;
		}
		else if (dir == BypassDirection.Up)
		{
			double targetZ = obsTopZ + pipeRadius + clearanceFeet;
			vOffset = targetZ - pStart.Z;
			if (vOffset < MinimumOffsetFeet) vOffset = 100.0 / 304.8;
			uDir = XYZ.BasisZ;
		}
		else
		{
			XYZ horizontalDirection = new XYZ(pipeDir.X, pipeDir.Y, 0.0);
			if (horizontalDirection.GetLength() <= GeometryToleranceFeet)
			{
				error = "Khong the ne Trai/Phai cho ong dung. Hay chon Len hoac Xuong.";
				return false;
			}

			XYZ horiz = horizontalDirection.Normalize();
			XYZ perp = new XYZ(-horiz.Y, horiz.X, 0.0);
			uDir = dir == BypassDirection.Left ? perp : -perp;
			double obstacleWidth = GetObstacleWidthAlongDirection(obstacleElem, obstacleTransform, uDir);
			vOffset = obstacleWidth * 0.5 + pipeRadius + clearanceFeet;
		}

		double slopeRun = angleDeg == 45.0
			? vOffset
			: Math.Max(pipeRadius * 2.0, 60.0 / 304.8);
		XYZ c2 = pClearEntry + uDir * vOffset;
		XYZ c3 = pClearExit + uDir * vOffset;
		XYZ c1 = pClearEntry - pipeDir * slopeRun;
		XYZ c4 = pClearExit + pipeDir * slopeRun;

		double tC1 = (c1 - pStart).DotProduct(pipeDir);
		double tC4 = (c4 - pStart).DotProduct(pipeDir);
		if (tC1 <= 0.05 || tC4 >= pipeLength - 0.05)
		{
			error = "Doan ong qua ngan (can toi thieu "
				+ Math.Round((c4 - c1).GetLength() * 304.8)
				+ " mm de ne dam). Vui long chon vi tri hoac khoang ho nho hon.";
			return false;
		}

		// Reconnect the original pEnd relationship after replacing that end segment.
		Connector endConn = GetConnectorAt(pipe, pEnd);
		Connector otherConn = GetConnectedRef(endConn);
		using (Transaction tr = new Transaction(doc, "BIM - Avoid Clash Bypass"))
		{
			FailureHandlingOptions failOpt = tr.GetFailureHandlingOptions();
			failOpt.SetFailuresPreprocessor(new SuppressAllWarnings());
			tr.SetFailureHandlingOptions(failOpt);
			tr.Start();
			try
			{
				if (endConn != null && otherConn != null && IsConnectedTo(endConn, otherConn))
				{
					endConn.DisconnectFrom(otherConn);
				}

				locCurve.Curve = Line.CreateBound(pStart, c1);
				Pipe pipeSlope1 = CreatePipe(doc, sysTypeId, pipeTypeId, levelId, c1, c2, pipeDiameter);
				Pipe pipeBridge = CreatePipe(doc, sysTypeId, pipeTypeId, levelId, c2, c3, pipeDiameter);
				Pipe pipeSlope2 = CreatePipe(doc, sysTypeId, pipeTypeId, levelId, c3, c4, pipeDiameter);
				Pipe pipeEnd = CreatePipe(doc, sysTypeId, pipeTypeId, levelId, c4, pEnd, pipeDiameter);
				doc.Regenerate();

				List<string> elbowDiagnostics = new List<string>();
				if (!ConnectPipesWithElbow(doc, pipe, pipeSlope1, c1, elbowDiagnostics)
					|| !ConnectPipesWithElbow(doc, pipeSlope1, pipeBridge, c2, elbowDiagnostics)
					|| !ConnectPipesWithElbow(doc, pipeBridge, pipeSlope2, c3, elbowDiagnostics)
					|| !ConnectPipesWithElbow(doc, pipeSlope2, pipeEnd, c4, elbowDiagnostics))
				{
					throw new InvalidOperationException("Khong the tao elbow cho bypass: " + string.Join(" | ", elbowDiagnostics));
				}

				if (otherConn != null) ReconnectEndConnector(pipeEnd, pEnd, otherConn);
				tr.Commit();
				return true;
			}
			catch (Exception ex)
			{
				if (tr.GetStatus() == TransactionStatus.Started) tr.RollBack();
				error = ex.Message;
				return false;
			}
		}
	}

	public static bool ExecuteBypass(Document doc, Element runningPipeElem, Element obstacleElem,
		double angleDeg, BypassDirection dir, double clearanceMm, out string error)
	{
		return ExecuteBypass(doc, runningPipeElem, obstacleElem, Transform.Identity, angleDeg, dir, clearanceMm, out error);
	}

	private static Pipe CreatePipe(Document doc, ElementId systemTypeId, ElementId pipeTypeId,
		ElementId levelId, XYZ start, XYZ end, double diameter)
	{
		Pipe result = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, start, end);
		result.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(diameter);
		return result;
	}

	// Solid intersection is the authority. The fallback never uses a beam midpoint.
	private static bool GetCrossingSpan(Pipe pipe, Element obstacle, Transform obstacleTransform, out XYZ pEntry, out XYZ pExit,
		out double obsBottomZ, out double obsTopZ)
	{
		pEntry = null;
		pExit = null;
		obsBottomZ = 0.0;
		obsTopZ = 0.0;
		Line pipeLine = (pipe.Location as LocationCurve)?.Curve as Line;
		if (pipeLine == null) return false;

		XYZ pStart = pipeLine.GetEndPoint(0);
		XYZ pipeDir = (pipeLine.GetEndPoint(1) - pStart).Normalize();
		BoundingBoxXYZ obstacleBox = obstacle.get_BoundingBox(null);
		double minT = double.MaxValue;
		double maxT = double.MinValue;
		double solidBottom = double.MaxValue;
		double solidTop = double.MinValue;
		bool foundSolidIntersection = false;

		Options options = new Options
		{
			DetailLevel = ViewDetailLevel.Fine,
			ComputeReferences = false,
			IncludeNonVisibleObjects = false
		};
		try
		{
			foreach (Solid solid in GetSolids(obstacle.get_Geometry(options)))
			{
				Solid solidToTest = solid;
				if (obstacleTransform != null && !obstacleTransform.IsIdentity)
				{
					try
					{
						solidToTest = SolidUtils.CreateTransformed(solid, obstacleTransform);
					}
					catch
					{
						solidToTest = solid;
					}
				}

				SolidCurveIntersection result;
				try
				{
					result = solidToTest.IntersectWithCurve(pipeLine, new SolidCurveIntersectionOptions
					{
						ResultType = SolidCurveIntersectionMode.CurveSegmentsInside
					});
				}
				catch
				{
					continue;
				}

				for (int i = 0; i < result.SegmentCount; i++)
				{
					Curve segment = result.GetCurveSegment(i);
					if (segment == null || segment.Length <= GeometryToleranceFeet) continue;
					double segmentStartT = (segment.GetEndPoint(0) - pStart).DotProduct(pipeDir);
					double segmentEndT = (segment.GetEndPoint(1) - pStart).DotProduct(pipeDir);
					minT = Math.Min(minT, Math.Min(segmentStartT, segmentEndT));
					maxT = Math.Max(maxT, Math.Max(segmentStartT, segmentEndT));
					foundSolidIntersection = true;
					if (TryGetBoundingBoxZ(solidToTest.GetBoundingBox(), Transform.Identity, out double solidMinZ, out double solidMaxZ))
					{
						solidBottom = Math.Min(solidBottom, solidMinZ);
						solidTop = Math.Max(solidTop, solidMaxZ);
					}
				}
			}
		}
		catch
		{
			// Fall through to the centerline/bounding-box calculation.
		}

		if (foundSolidIntersection)
		{
			pEntry = pStart + pipeDir * minT;
			pExit = pStart + pipeDir * maxT;
			if (!OrderCrossingPoints(pStart, pipeDir, ref pEntry, ref pExit)) return false;
			if (solidBottom != double.MaxValue && solidTop != double.MinValue)
			{
				obsBottomZ = solidBottom;
				obsTopZ = solidTop;
				return true;
			}
			return TryGetBoundingBoxZ(obstacleBox, obstacleTransform, out obsBottomZ, out obsTopZ);
		}

		if (obstacleBox == null || !TryGetBoundingBoxZ(obstacleBox, obstacleTransform, out obsBottomZ, out obsTopZ)) return false;
		Line obstacleLine = (obstacle.Location as LocationCurve)?.Curve as Line;
		if (obstacleLine != null && obstacleTransform != null && !obstacleTransform.IsIdentity)
		{
			try
			{
				obstacleLine = obstacleLine.CreateTransformed(obstacleTransform) as Line;
			}
			catch
			{
			}
		}
		if (obstacleLine != null)
		{
			FindClosestPointsBetweenLines(pipeLine, obstacleLine, out XYZ closestOnPipe, out XYZ closestOnObstacle);
			if (TryGetLineBoundingBoxIntersection(pipeLine, obstacleBox, obstacleTransform, out pEntry, out pExit))
			{
				return OrderCrossingPoints(pStart, pipeDir, ref pEntry, ref pExit);
			}

			double fallbackThickness = GetBoundingBoxWidthAlongDirection(obstacleBox, obstacleTransform, pipeDir);
			if (fallbackThickness <= GeometryToleranceFeet) return false;
			pEntry = closestOnPipe - pipeDir * fallbackThickness * 0.5;
			pExit = closestOnPipe + pipeDir * fallbackThickness * 0.5;
			return OrderCrossingPoints(pStart, pipeDir, ref pEntry, ref pExit);
		}

		return TryGetLineBoundingBoxIntersection(pipeLine, obstacleBox, obstacleTransform, out pEntry, out pExit)
			&& OrderCrossingPoints(pStart, pipeDir, ref pEntry, ref pExit);
	}

	private static IEnumerable<Solid> GetSolids(GeometryElement geometry)
	{
		if (geometry == null) yield break;
		foreach (GeometryObject item in geometry)
		{
			Solid solid = item as Solid;
			if (solid != null && solid.Faces.Size > 0 && solid.Edges.Size > 0)
			{
				yield return solid;
				continue;
			}

			GeometryInstance instance = item as GeometryInstance;
			if (instance == null) continue;
			GeometryElement instanceGeometry;
			try { instanceGeometry = instance.GetInstanceGeometry(); }
			catch { continue; }
			foreach (Solid instanceSolid in GetSolids(instanceGeometry)) yield return instanceSolid;
		}
	}

	// Closest points are constrained to both finite LocationCurve segments.
	private static bool FindClosestPointsBetweenLines(Line first, Line second, out XYZ firstPoint, out XYZ secondPoint)
	{
		XYZ p0 = first.GetEndPoint(0);
		XYZ q0 = second.GetEndPoint(0);
		XYZ firstVector = first.GetEndPoint(1) - p0;
		XYZ secondVector = second.GetEndPoint(1) - q0;
		XYZ separation = p0 - q0;
		double a = firstVector.DotProduct(firstVector);
		double e = secondVector.DotProduct(secondVector);
		double f = secondVector.DotProduct(separation);
		double s;
		double t;

		if (a <= GeometryToleranceFeet && e <= GeometryToleranceFeet)
		{
			firstPoint = p0;
			secondPoint = q0;
			return false;
		}
		if (a <= GeometryToleranceFeet)
		{
			s = 0.0;
			t = Clamp(f / e, 0.0, 1.0);
		}
		else
		{
			double c = firstVector.DotProduct(separation);
			if (e <= GeometryToleranceFeet)
			{
				t = 0.0;
				s = Clamp(-c / a, 0.0, 1.0);
			}
			else
			{
				double b = firstVector.DotProduct(secondVector);
				double denominator = a * e - b * b;
				s = Math.Abs(denominator) > GeometryToleranceFeet
					? Clamp((b * f - c * e) / denominator, 0.0, 1.0)
					: 0.0;
				double tNumerator = b * s + f;
				if (tNumerator <= 0.0)
				{
					t = 0.0;
					s = Clamp(-c / a, 0.0, 1.0);
				}
				else if (tNumerator >= e)
				{
					t = 1.0;
					s = Clamp((b - c) / a, 0.0, 1.0);
				}
				else t = tNumerator / e;
			}
		}

		firstPoint = p0 + firstVector * s;
		secondPoint = q0 + secondVector * t;
		return true;
	}

	private static bool TryGetLineBoundingBoxIntersection(Line line, BoundingBoxXYZ box, Transform obstacleTransform, out XYZ pEntry, out XYZ pExit)
	{
		pEntry = null;
		pExit = null;
		if (box == null) return false;
		XYZ modelStart = line.GetEndPoint(0);
		XYZ modelDir = (line.GetEndPoint(1) - modelStart).Normalize();
		Transform transform = box.Transform;
		if (obstacleTransform != null && !obstacleTransform.IsIdentity)
		{
			transform = transform == null ? obstacleTransform : obstacleTransform.Multiply(transform);
		}
		Transform inverse = transform?.Inverse;
		XYZ localStart = inverse == null ? modelStart : inverse.OfPoint(modelStart);
		XYZ localDir = inverse == null ? modelDir : inverse.OfVector(modelDir);
		double entryT = 0.0;
		double exitT = line.Length;
		if (!ClipLineToSlab(localStart.X, localDir.X, box.Min.X, box.Max.X, ref entryT, ref exitT)
			|| !ClipLineToSlab(localStart.Y, localDir.Y, box.Min.Y, box.Max.Y, ref entryT, ref exitT)
			|| !ClipLineToSlab(localStart.Z, localDir.Z, box.Min.Z, box.Max.Z, ref entryT, ref exitT)
			|| exitT - entryT <= GeometryToleranceFeet)
		{
			return false;
		}
		pEntry = modelStart + modelDir * entryT;
		pExit = modelStart + modelDir * exitT;
		return true;
	}

	private static bool ClipLineToSlab(double origin, double direction, double min, double max, ref double entryT, ref double exitT)
	{
		if (Math.Abs(direction) <= GeometryToleranceFeet) return origin >= min && origin <= max;
		double first = (min - origin) / direction;
		double second = (max - origin) / direction;
		if (first > second)
		{
			double swap = first;
			first = second;
			second = swap;
		}
		entryT = Math.Max(entryT, first);
		exitT = Math.Min(exitT, second);
		return entryT <= exitT;
	}

	private static bool OrderCrossingPoints(XYZ pStart, XYZ pipeDir, ref XYZ pEntry, ref XYZ pExit)
	{
		if (pEntry == null || pExit == null || pEntry.DistanceTo(pExit) <= GeometryToleranceFeet) return false;
		if ((pEntry - pStart).DotProduct(pipeDir) > (pExit - pStart).DotProduct(pipeDir))
		{
			XYZ swap = pEntry;
			pEntry = pExit;
			pExit = swap;
		}
		return true;
	}

	private static bool TryGetBoundingBoxZ(BoundingBoxXYZ box, Transform obstacleTransform, out double minZ, out double maxZ)
	{
		minZ = double.MaxValue;
		maxZ = double.MinValue;
		if (box == null) return false;
		foreach (XYZ point in GetBoundingBoxCorners(box, obstacleTransform))
		{
			minZ = Math.Min(minZ, point.Z);
			maxZ = Math.Max(maxZ, point.Z);
		}
		return minZ != double.MaxValue && maxZ != double.MinValue;
	}

	private static double GetObstacleWidthAlongDirection(Element obstacle, Transform obstacleTransform, XYZ direction)
	{
		return Math.Max(GetBoundingBoxWidthAlongDirection(obstacle.get_BoundingBox(null), obstacleTransform, direction), MinimumOffsetFeet);
	}

	private static double GetBoundingBoxWidthAlongDirection(BoundingBoxXYZ box, Transform obstacleTransform, XYZ direction)
	{
		if (box == null) return 0.0;
		double minProjection = double.MaxValue;
		double maxProjection = double.MinValue;
		foreach (XYZ point in GetBoundingBoxCorners(box, obstacleTransform))
		{
			double projection = point.DotProduct(direction);
			minProjection = Math.Min(minProjection, projection);
			maxProjection = Math.Max(maxProjection, projection);
		}
		return minProjection == double.MaxValue ? 0.0 : maxProjection - minProjection;
	}

	private static IEnumerable<XYZ> GetBoundingBoxCorners(BoundingBoxXYZ box, Transform obstacleTransform = null)
	{
		Transform transform = box.Transform;
		if (obstacleTransform != null && !obstacleTransform.IsIdentity)
		{
			transform = transform == null ? obstacleTransform : obstacleTransform.Multiply(transform);
		}
		double[] xs = { box.Min.X, box.Max.X };
		double[] ys = { box.Min.Y, box.Max.Y };
		double[] zs = { box.Min.Z, box.Max.Z };
		foreach (double x in xs)
		foreach (double y in ys)
		foreach (double z in zs)
		{
			XYZ point = new XYZ(x, y, z);
			yield return transform == null ? point : transform.OfPoint(point);
		}
	}

	private static bool ConnectPipesWithElbow(Document doc, Pipe firstPipe, Pipe secondPipe, XYZ point, ICollection<string> diagnostics)
	{
		Connector first = GetConnectorAt(firstPipe, point);
		Connector second = GetConnectorAt(secondPipe, point);
		if (first == null || second == null)
		{
			diagnostics.Add("Khong tim thay connector tai " + FormatPoint(point) + ".");
			return false;
		}
		if (IsConnectedTo(first, second)) return true;
		if (first.IsConnected || second.IsConnected)
		{
			diagnostics.Add("Connector tai " + FormatPoint(point) + " da ket noi voi phan tu khac.");
			return false;
		}
		try
		{
			doc.Create.NewElbowFitting(first, second);
			return true;
		}
		catch (Exception ex)
		{
			diagnostics.Add("NewElbowFitting tai " + FormatPoint(point) + " that bai: " + ex.Message);
			return false;
		}
	}

	private static void ReconnectEndConnector(Pipe pipeEnd, XYZ pEnd, Connector otherConn)
	{
		Connector replacementEnd = GetConnectorAt(pipeEnd, pEnd);
		if (replacementEnd == null) throw new InvalidOperationException("Khong tim thay connector dau cuoi cua ong thay the.");
		if (!IsConnectedTo(replacementEnd, otherConn))
		{
			if (replacementEnd.IsConnected) throw new InvalidOperationException("Connector dau cuoi cua ong thay the dang ket noi sai phan tu.");
			replacementEnd.ConnectTo(otherConn);
		}
		if (!IsConnectedTo(replacementEnd, otherConn)) throw new InvalidOperationException("Khong the khoi phuc ket noi tai dau cuoi ong.");
	}

	private static Connector GetConnectorAt(Pipe pipe, XYZ point)
	{
		if (pipe == null || pipe.ConnectorManager == null || point == null) return null;
		Connector closest = null;
		double closestDistance = double.MaxValue;
		foreach (Connector connector in pipe.ConnectorManager.Connectors)
		{
			double distance = connector.Origin.DistanceTo(point);
			if (distance < closestDistance)
			{
				closest = connector;
				closestDistance = distance;
			}
		}
		return closestDistance <= ConnectorToleranceFeet ? closest : null;
	}

	private static Connector GetConnectedRef(Connector connector)
	{
		if (connector == null || !connector.IsConnected) return null;
		foreach (Connector reference in connector.AllRefs)
		{
			if (reference != null && reference.Owner != null && connector.Owner != null
				&& reference.Owner.Id.GetIdInt() != connector.Owner.Id.GetIdInt()) return reference;
		}
		return null;
	}

	private static bool IsConnectedTo(Connector first, Connector second)
	{
		if (first == null || second == null || !first.IsConnected) return false;
		foreach (Connector reference in first.AllRefs)
		{
			if (reference != null && reference.Owner != null && second.Owner != null
				&& reference.Owner.Id.GetIdInt() == second.Owner.Id.GetIdInt()
				&& reference.Origin.DistanceTo(second.Origin) <= ConnectorToleranceFeet) return true;
		}
		return false;
	}

	private static double Clamp(double value, double min, double max)
	{
		return Math.Max(min, Math.Min(max, value));
	}

	private static string FormatPoint(XYZ point)
	{
		return "(" + Math.Round(point.X * 304.8) + ", " + Math.Round(point.Y * 304.8) + ", " + Math.Round(point.Z * 304.8) + " mm)";
	}

	private sealed class AvoidClashPipeSelectionFilter : ISelectionFilter
	{
		public bool AllowElement(Element element)
		{
			return element is Pipe || element?.Category?.Id.GetIdInt() == (int)BuiltInCategory.OST_PipeCurves;
		}

		public bool AllowReference(Reference reference, XYZ position)
		{
			return true;
		}
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
