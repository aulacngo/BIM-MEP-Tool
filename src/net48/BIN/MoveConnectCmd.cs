using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class MoveConnectCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Expected O, but got Unknown
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			ICollection<ElementId> preSelected = uidoc.Selection.GetElementIds();
			bool hasPreSelection = preSelected.Count > 0;
			MepConnectableNewFilter filter = new MepConnectableNewFilter
			{
				PreviousElementID = null
			};
			Reference ref1 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn element MEP đích (giữ nguyên)");
			Element elem1 = doc.GetElement(ref1);
			filter.PreviousElementID = elem1.Id;
			Reference ref2 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn element MEP cần di chuyển");
			Element elem2 = doc.GetElement(ref2);
			Connector[] pair = NaviateHelper.ClosestConnectors(elem1, elem2, align: false);
			if (pair == null)
			{
				TaskDialog.Show("Move Connect", "Không tìm thấy connector phù hợp.");
				return Result.Failed;
			}
			if (pair[0] == null || pair[1] == null || pair[0].IsConnected || pair[1].IsConnected)
			{
				TaskDialog.Show("Move Connect", "Connector đã được kết nối hoặc không hợp lệ.");
				return Result.Failed;
			}
			Transaction tr = new Transaction(doc, "Move and Connect");
			try
			{
				tr.Start();
				XYZ moveVec = pair[0].Origin - pair[1].Origin;
				if (hasPreSelection)
				{
					ElementTransformUtils.MoveElements(doc, preSelected, moveVec);
				}
				else
				{
					ElementTransformUtils.MoveElement(doc, elem2.Id, moveVec);
				}
				pair[1].ConnectTo(pair[0]);
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
