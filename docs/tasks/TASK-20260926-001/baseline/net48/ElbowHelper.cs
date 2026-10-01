using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public static class ElbowHelper
{
	public static Result ExecuteElbow(ExternalCommandData commandData, ref string message, ElbowDirection direction)
	{
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_028b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Expected O, but got Unknown
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		UIApplication uiApp = commandData.Application;
		UIDocument uiDoc = uiApp.ActiveUIDocument;
		Document doc = uiDoc.Document;
		try
		{
			ISelectionFilter filter = (ISelectionFilter)(object)new MepCurveFilter();
			Reference pickedRef = uiDoc.Selection.PickObject((ObjectType)1, filter, "Please pick a point on Duct, Pipe or Conduit.");
			Element element = doc.GetElement(pickedRef);
			XYZ pickedPoint = pickedRef.GlobalPoint;
			Connector[] unusedConns = NaviateHelper.ConnectorArrayUnused(element);
			if (unusedConns == null)
			{
				TaskDialog.Show("Elbow", "No open endings found on the selected element.");
				return Result.Failed;
			}
			Connector sourceConn = NaviateHelper.NearestConnector(unusedConns, pickedPoint);
			if (direction == ElbowDirection.Down45)
			{
				if (sourceConn.IsConnected || (Math.Round(sourceConn.CoordinateSystem.BasisZ.X, 3) == 0.0 && Math.Round(sourceConn.CoordinateSystem.BasisZ.Y, 3) == 0.0 && sourceConn.CoordinateSystem.BasisZ.Z > 0.0))
				{
					sourceConn = NaviateHelper.FarthestConnector(unusedConns, pickedPoint);
				}
			}
			else if (sourceConn.IsConnected)
			{
				sourceConn = NaviateHelper.FarthestConnector(unusedConns, pickedPoint);
			}
			if (sourceConn == null || sourceConn.IsConnected)
			{
				TaskDialog.Show("Elbow", "Selected ending is already connected.");
				return Result.Failed;
			}
			XYZ origin = sourceConn.Origin;
			double extLen = NaviateHelper.GetExtensionLength(sourceConn);
			XYZ endPoint = CalculateNaviateEndpoint(sourceConn, extLen, direction);
			Transaction tr = new Transaction(doc, "BIN Draw Elbow " + direction);
			try
			{
				tr.Start();
				if (element is Pipe)
				{
					NaviateHelper.DrawPipeWithElbow(doc, pickedRef, pickedPoint, sourceConn, origin, endPoint);
				}
				else if (element is Duct)
				{
					NaviateHelper.DrawDuct(doc, pickedRef, pickedPoint, sourceConn, origin, endPoint);
				}
				else if (element is Conduit)
				{
					NaviateHelper.DrawConduit(doc, pickedRef, pickedPoint, sourceConn, origin, endPoint, NaviateHelper.GetLevel(doc, origin.Z));
				}
				else if (element is CableTray)
				{
					NaviateHelper.DrawCableTray(doc, pickedRef, pickedPoint, sourceConn, origin, endPoint, NaviateHelper.GetLevel(doc, origin.Z));
				}
				tr.Commit();
			}
			finally
			{
				((IDisposable)tr)?.Dispose();
			}
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			TaskDialog.Show("Error", ex.Message);
			return Result.Failed;
		}
	}

	private static XYZ CalculateNaviateEndpoint(Connector connector, double ext, ElbowDirection dir)
	{
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Expected O, but got Unknown
		//IL_039e: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a8: Expected O, but got Unknown
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Expected O, but got Unknown
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Expected O, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Expected O, but got Unknown
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0172: Expected O, but got Unknown
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a9: Expected O, but got Unknown
		//IL_0335: Unknown result type (might be due to invalid IL or missing references)
		//IL_033c: Expected O, but got Unknown
		XYZ origin = connector.Origin;
		XYZ basisZ = connector.CoordinateSystem.BasisZ;
		XYZ basisY = connector.CoordinateSystem.BasisY;
		XYZ basisX = connector.CoordinateSystem.BasisX;
		double bX = Math.Round(basisZ.X, 3);
		double bY = Math.Round(basisZ.Y, 3);
		double bZ = Math.Round(basisZ.Z, 3);
		switch (dir)
		{
		case ElbowDirection.Up:
			if (Math.Abs(basisZ.X) < 0.001 && Math.Abs(basisZ.Y) < 0.001)
			{
				return new XYZ(origin.X, origin.Y + ((basisZ.Z > 0.0) ? (0.0 - ext) : ext), origin.Z);
			}
			return new XYZ(origin.X, origin.Y, origin.Z + ext);
		case ElbowDirection.Down:
			if (Math.Abs(basisZ.X) < 0.001 && Math.Abs(basisZ.Y) < 0.001)
			{
				return new XYZ(origin.X, origin.Y + ((basisZ.Z > 0.0) ? (0.0 - ext) : ext), origin.Z);
			}
			return new XYZ(origin.X, origin.Y, origin.Z - ext);
		case ElbowDirection.Down45:
		{
			if (Math.Abs(bZ) < 0.99)
			{
				XYZ d45Vec = basisZ + new XYZ(0.0, 0.0, -1.0);
				return origin + d45Vec.Normalize() * (ext * 1.414);
			}
			XYZ d45Vec2 = basisZ + ((basisY.Z < 0.0) ? basisY : (-basisY));
			return origin + d45Vec2.Normalize() * (ext * 1.414);
		}
		case ElbowDirection.Left:
		{
			if (Math.Abs(basisZ.X) < 0.001 && Math.Abs(basisZ.Y) < 0.001)
			{
				return new XYZ(origin.X + ((basisZ.Z > 0.0) ? ext : (0.0 - ext)), origin.Y, origin.Z);
			}
			XYZ leftVec = XYZ.BasisZ.CrossProduct(basisZ).Normalize();
			return origin + leftVec * ext;
		}
		case ElbowDirection.Right:
		{
			if (Math.Abs(basisZ.X) < 0.001 && Math.Abs(basisZ.Y) < 0.001)
			{
				return new XYZ(origin.X + ((basisZ.Z > 0.0) ? (0.0 - ext) : ext), origin.Y, origin.Z);
			}
			XYZ rightVec = basisZ.CrossProduct(XYZ.BasisZ).Normalize();
			return origin + rightVec * ext;
		}
		case ElbowDirection.Up45:
		{
			if (Math.Abs(bZ) < 0.99)
			{
				XYZ u45Vec = basisZ + new XYZ(0.0, 0.0, 1.0);
				return origin + u45Vec.Normalize() * (ext * 1.414);
			}
			XYZ u45Vec2 = basisZ + ((basisY.Z > 0.0) ? basisY : (-basisY));
			return origin + u45Vec2.Normalize() * (ext * 1.414);
		}
		case ElbowDirection.Left45:
		{
			if (Math.Abs(basisZ.X) < 0.001 && Math.Abs(basisZ.Y) < 0.001)
			{
				XYZ lVec = (basisZ.Z > 0.0) ? new XYZ(1.0, 0.0, 1.0) : new XYZ(-1.0, 0.0, -1.0);
				return origin + lVec.Normalize() * (ext * 1.414);
			}
			XYZ leftPerp = XYZ.BasisZ.CrossProduct(basisZ).Normalize();
			XYZ left45Vec = basisZ + leftPerp;
			return origin + left45Vec.Normalize() * (ext * 1.414);
		}
		case ElbowDirection.Right45:
		{
			if (Math.Abs(basisZ.X) < 0.001 && Math.Abs(basisZ.Y) < 0.001)
			{
				XYZ rVec = (basisZ.Z > 0.0) ? new XYZ(-1.0, 0.0, 1.0) : new XYZ(1.0, 0.0, -1.0);
				return origin + rVec.Normalize() * (ext * 1.414);
			}
			XYZ rightPerp = basisZ.CrossProduct(XYZ.BasisZ).Normalize();
			XYZ right45Vec = basisZ + rightPerp;
			return origin + right45Vec.Normalize() * (ext * 1.414);
		}
		default:
			return origin + basisZ * ext;
		}
	}
}
