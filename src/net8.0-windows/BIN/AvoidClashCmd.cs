using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class AvoidClashCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;

		try
		{
			// 1. Pick running pipe (Ống cần uốn)
			Reference refPipe = uidoc.Selection.PickObject(
				ObjectType.Element, 
				new SelectPipes(), 
				"1. Click ch\u1ecdn \u1ed0NG c\u1ea7n u\u1ed1n \u0111\u1ec3 n\u00e9 va ch\u1ea1m (Running Pipe)"
			);
			if (refPipe == null) return Result.Cancelled;

			Element runningPipeElem = doc.GetElement(refPipe);
			if (runningPipeElem == null) return Result.Cancelled;

			// 2. Pick obstacle (Vật cản)
			Reference refObstacle = uidoc.Selection.PickObject(
				ObjectType.Element, 
				"2. Click ch\u1ecdn V\u1eacT C\u1ea2N (\u1ed0ng, \u1ed0ng gi\u00f3, M\u00e1ng c\u00e1p ho\u1eb7c D\u1ea7m k\u1ebft c\u1ea5u)"
			);
			if (refObstacle == null) return Result.Cancelled;

			Element obstacleElem = doc.GetElement(refObstacle);
			if (obstacleElem == null) return Result.Cancelled;

			// 3. Show modern AvoidClashWindow
			AvoidClashWindow window = new AvoidClashWindow(uidoc, runningPipeElem, obstacleElem);
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
		double angleDeg, BypassDirection dir, double clearanceMm, out string error)
	{
		error = string.Empty;

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
			error = "Khong lay duoc truc tim cua ong.";
			return false;
		}

		XYZ pStart = pipeLine.GetEndPoint(0);
		XYZ pEnd = pipeLine.GetEndPoint(1);
		XYZ pipeDir = (pEnd - pStart).Normalize();
		double pipeLength = pipeLine.Length;

		double pipeDiameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble() ?? (50.0 / 304.8);
		double pipeRadius = pipeDiameter * 0.5;

		ElementId sysTypeId = pipe.MEPSystem?.GetTypeId() ?? pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId();
		if (sysTypeId == null || sysTypeId == ElementId.InvalidElementId)
		{
			sysTypeId = pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsElementId();
		}
		ElementId pipeTypeId = pipe.PipeType.Id;
		ElementId levelId = pipe.ReferenceLevel?.Id ?? pipe.LevelId;

		// Determine Obstacle center & half extent
		XYZ obsCenter = XYZ.Zero;
		double obsRadius = 100.0 / 304.8; // default 100mm

		if (obstacleElem is MEPCurve obsMep)
		{
			LocationCurve obsLoc = obsMep.Location as LocationCurve;
			Line obsLine = obsLoc?.Curve as Line;
			if (obsLine != null)
			{
				XYZ q0 = obsLine.GetEndPoint(0);
				XYZ q1 = obsLine.GetEndPoint(1);
				XYZ obsDir = (q1 - q0).Normalize();

				// Closest approach point between two 3D lines
				obsCenter = FindClosestPointOnLine(pStart, pipeDir, q0, obsDir);

				double obsDiam = obsMep.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.AsDouble()
					?? obsMep.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.AsDouble()
					?? (150.0 / 304.8);
				obsRadius = obsDiam * 0.5;
			}
			else
			{
				BoundingBoxXYZ bb = obstacleElem.get_BoundingBox(null);
				obsCenter = (bb.Min + bb.Max) * 0.5;
				obsRadius = Math.Max(bb.Max.X - bb.Min.X, Math.Max(bb.Max.Y - bb.Min.Y, bb.Max.Z - bb.Min.Z)) * 0.5;
			}
		}
		else
		{
			BoundingBoxXYZ bb = obstacleElem.get_BoundingBox(null);
			if (bb != null)
			{
				obsCenter = (bb.Min + bb.Max) * 0.5;
				obsRadius = (bb.Max.Z - bb.Min.Z) * 0.5;
			}
			else
			{
				error = "Khong xac dinh duoc vi tri cua vat can.";
				return false;
			}
		}

		// Project obstacle center onto pipe line
		double tProj = (obsCenter - pStart).DotProduct(pipeDir);
		XYZ pint = pStart + pipeDir * tProj;

		// Calculate Bypass Direction Vector U
		XYZ uDir = XYZ.BasisZ;
		if (dir == BypassDirection.Up)
		{
			uDir = XYZ.BasisZ;
		}
		else if (dir == BypassDirection.Down)
		{
			uDir = -XYZ.BasisZ;
		}
		else
		{
			XYZ horiz = new XYZ(pipeDir.X, pipeDir.Y, 0.0).Normalize();
			XYZ perp = new XYZ(-horiz.Y, horiz.X, 0.0);
			uDir = (dir == BypassDirection.Left) ? perp : -perp;
		}

		double clearanceFeet = clearanceMm / 304.8;
		double jumpHeight = pipeRadius + obsRadius + clearanceFeet;
		double halfSpan = Math.Max(obsRadius, 100.0 / 304.8) + (50.0 / 304.8);
		double slopeRun = (angleDeg == 45.0) ? jumpHeight : Math.Max(pipeRadius * 2.0, 60.0 / 304.8);

		XYZ c1 = pint - pipeDir * (halfSpan + slopeRun);
		XYZ c2 = pint - pipeDir * halfSpan + uDir * jumpHeight;
		XYZ c3 = pint + pipeDir * halfSpan + uDir * jumpHeight;
		XYZ c4 = pint + pipeDir * (halfSpan + slopeRun);

		// Check if bypass points fit on the pipe
		double tC1 = (c1 - pStart).DotProduct(pipeDir);
		double tC4 = (c4 - pStart).DotProduct(pipeDir);

		if (tC1 <= 0.1 || tC4 >= pipeLength - 0.1)
		{
			error = "Doan ong qua ngan de chen 4 cut ne va cham. Vui long giam khoang ho hoac chon vi tri khac.";
			return false;
		}

		using (Transaction tr = new Transaction(doc, "BIM - Avoid Clash Bypass"))
		{
			FailureHandlingOptions failOpt = tr.GetFailureHandlingOptions();
			failOpt.SetFailuresPreprocessor(new SuppressAllWarnings());
			tr.SetFailureHandlingOptions(failOpt);

			tr.Start();
			try
			{
				// 1. Shorten original pipe from pStart to c1
				locCurve.Curve = Line.CreateBound(pStart, c1);

				// 2. Create pipe 2: up-slope (c1 to c2)
				Pipe pipeSlope1 = Pipe.Create(doc, sysTypeId, pipeTypeId, levelId, c1, c2);
				pipeSlope1.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(pipeDiameter);

				// 3. Create pipe 3: bridge over obstacle (c2 to c3)
				Pipe pipeBridge = Pipe.Create(doc, sysTypeId, pipeTypeId, levelId, c2, c3);
				pipeBridge.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(pipeDiameter);

				// 4. Create pipe 4: down-slope (c3 to c4)
				Pipe pipeSlope2 = Pipe.Create(doc, sysTypeId, pipeTypeId, levelId, c3, c4);
				pipeSlope2.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(pipeDiameter);

				// 5. Create pipe 5: end segment (c4 to pEnd)
				Pipe pipeEnd = Pipe.Create(doc, sysTypeId, pipeTypeId, levelId, c4, pEnd);
				pipeEnd.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(pipeDiameter);

				doc.Regenerate();

				// Connect Elbow 1 at c1
				ConnectPipesWithElbow(doc, pipe, pipeSlope1, c1);

				// Connect Elbow 2 at c2
				ConnectPipesWithElbow(doc, pipeSlope1, pipeBridge, c2);

				// Connect Elbow 3 at c3
				ConnectPipesWithElbow(doc, pipeBridge, pipeSlope2, c3);

				// Connect Elbow 4 at c4
				ConnectPipesWithElbow(doc, pipeSlope2, pipeEnd, c4);

				tr.Commit();
				return true;
			}
			catch (Exception ex)
			{
				tr.RollBack();
				error = ex.Message;
				return false;
			}
		}
	}

	private static void ConnectPipesWithElbow(Document doc, Pipe p1, Pipe p2, XYZ nearPt)
	{
		try
		{
			Connector c1 = GetClosestConnector(p1, nearPt);
			Connector c2 = GetClosestConnector(p2, nearPt);
			if (c1 != null && c2 != null && !c1.IsConnected && !c2.IsConnected)
			{
				doc.Create.NewElbowFitting(c1, c2);
			}
		}
		catch { }
	}

	private static Connector GetClosestConnector(Pipe p, XYZ pt)
	{
		if (p == null || p.ConnectorManager == null) return null;
		Connector closest = null;
		double minDist = double.MaxValue;
		foreach (Connector c in p.ConnectorManager.Connectors)
		{
			double dist = c.Origin.DistanceTo(pt);
			if (dist < minDist)
			{
				minDist = dist;
				closest = c;
			}
		}
		return closest;
	}

	private static XYZ FindClosestPointOnLine(XYZ p1, XYZ dir1, XYZ p2, XYZ dir2)
	{
		XYZ p13 = p1 - p2;
		double d1343 = p13.DotProduct(dir2);
		double d4321 = dir2.DotProduct(dir1);
		double d1321 = p13.DotProduct(dir1);
		double d4343 = dir2.DotProduct(dir2);
		double d2121 = dir1.DotProduct(dir1);

		double denom = d2121 * d4343 - d4321 * d4321;
		if (Math.Abs(denom) < 1e-6)
		{
			// Parallel lines
			return p1;
		}

		double numer = d1343 * d4321 - d1321 * d4343;
		double mua = numer / denom;
		return p1 + dir1 * mua;
	}
}
