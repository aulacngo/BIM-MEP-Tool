using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ThreeDAlignCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_027b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0248: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Expected O, but got Unknown
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			MepConnectableNewFilter filter = new MepConnectableNewFilter
			{
				PreviousElementID = null
			};
			Reference ref1 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn element MEP đích (giữ nguyên)");
			Element elem1 = doc.GetElement(ref1);
			filter.PreviousElementID = elem1.Id;
			Reference ref2 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn element MEP cần căn chỉnh 3D");
			Element elem2 = doc.GetElement(ref2);
			Connector[] conns1 = NaviateHelper.ConnectorArrayUnused(elem1);
			Connector[] conns2 = NaviateHelper.ConnectorArrayUnused(elem2);
			if (conns1 == null || conns2 == null)
			{
				TaskDialog.Show("3D Align", "Không tìm thấy connector trống.");
				return Result.Failed;
			}
			Connector[] pair = NaviateHelper.ClosestConnectors(elem1, elem2, align: true);
			if (pair == null || pair[0] == null || pair[1] == null)
			{
				TaskDialog.Show("3D Align", "Không tìm thấy connector phù hợp.");
				return Result.Failed;
			}
			Transaction tr = new Transaction(doc, "3D Align");
			try
			{
				tr.Start();
				XYZ moveVec = pair[0].Origin - pair[1].Origin;
				ElementTransformUtils.MoveElement(doc, elem2.Id, moveVec);
				Connector[] updatedConns2 = NaviateHelper.ConnectorArrayUnused(elem2);
				if (updatedConns2 != null)
				{
					Connector movedConn = NaviateHelper.NearestConnector(updatedConns2, pair[0].Origin);
					if (movedConn != null)
					{
						XYZ d1 = pair[0].CoordinateSystem.BasisZ;
						XYZ d2 = movedConn.CoordinateSystem.BasisZ;
						double dot = d1.DotProduct(d2);
						if (Math.Abs(dot + 1.0) > 0.01)
						{
							XYZ cross = d1.CrossProduct(d2);
							if (cross.GetLength() > 0.001)
							{
								double angle = Math.PI - d1.AngleTo(d2);
								Line axis = Line.CreateBound(pair[0].Origin, pair[0].Origin + cross.Normalize() * 10.0);
								ElementTransformUtils.RotateElement(doc, elem2.Id, axis, angle);
							}
						}
						try
						{
							movedConn.ConnectTo(pair[0]);
						}
						catch
						{
						}
					}
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
