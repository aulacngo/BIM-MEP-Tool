using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class BranchAlignLiteCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected O, but got Unknown
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		Document doc = commandData.Application.ActiveUIDocument.Document;
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		try
		{
			MepCurveFilter filter = new MepCurveFilter();
			filter.PreviousElementID = null;
			Reference ref1 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn đường CHÍNH (main)");
			Element mainElem = doc.GetElement(ref1);
			filter.PreviousElementID = mainElem.Id;
			Reference ref2 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn đường NHÁNH (branch)");
			Element branchElem = doc.GetElement(ref2);
			XYZ clickPt = ref1.GlobalPoint;
			Connector[] mainConns = NaviateHelper.ConnectorArray(mainElem);
			Connector[] branchConns = NaviateHelper.ConnectorArray(branchElem);
			Connector mainNearest = NaviateHelper.NearestConnector(mainConns, clickPt);
			Connector branchClosest = NaviateHelper.ClosestConnectorOfDomain(branchConns, mainConns, mainNearest.Domain);
			Connector mainFarthest = NaviateHelper.FarthestConnector(mainConns, clickPt);
			Connector branchFarthest = NaviateHelper.FarthestConnector(branchConns, branchClosest.Origin);
			if (mainNearest == null || branchClosest == null)
			{
				return Result.Failed;
			}
			Transaction tr = new Transaction(doc, "Align Pipe Branch Lite");
			try
			{
				tr.Start();
				XYZ mainDir = mainNearest.CoordinateSystem.BasisZ;
				XYZ mainOrigin = mainNearest.Origin;
				XYZ branchOrigin = branchClosest.Origin;
				XYZ intersection = NaviateHelper.IntersectionTwoVectors(branchFarthest.Origin, branchClosest.Origin, mainFarthest.Origin, mainNearest.Origin);
				XYZ perpOnMain = NaviateHelper.PerpIntersection(mainOrigin, mainOrigin + mainDir, intersection);
				XYZ branchFarDir = branchFarthest.CoordinateSystem.BasisZ;
				XYZ targetPt = NaviateHelper.PerpIntersection(perpOnMain, perpOnMain + branchFarDir, branchOrigin);
				XYZ moveVec = targetPt - branchOrigin;
				if (moveVec.GetLength() > 0.0)
				{
					ElementTransformUtils.MoveElement(doc, branchElem.Id, moveVec);
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
			message = ex.Message;
			return Result.Failed;
		}
	}
}
