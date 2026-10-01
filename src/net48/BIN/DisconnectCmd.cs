using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class DisconnectCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Expected O, but got Unknown
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Expected O, but got Unknown
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			MepConnectableNewFilter filter = new MepConnectableNewFilter
			{
				PreviousElementID = null
			};
			Reference ref1 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn element MEP thứ 1");
			Element elem1 = doc.GetElement(ref1);
			filter.PreviousElementID = elem1.Id;
			Reference ref2 = uidoc.Selection.PickObject((ObjectType)1, (ISelectionFilter)(object)filter, "Chọn element MEP thứ 2 (đang kết nối với element 1)");
			Element elem2 = doc.GetElement(ref2);
			Transaction tr = new Transaction(doc, "Disconnect MEP");
			try
			{
				tr.Start();
				Connector[] connectors1 = NaviateHelper.ConnectorArray(elem1);
				if (connectors1 != null)
				{
					Connector[] array = connectors1;
					foreach (Connector c1 in array)
					{
						foreach (Connector allRef in c1.AllRefs)
						{
							Connector cRef = allRef;
							if (cRef.Owner.Id == elem2.Id)
							{
								try
								{
									cRef.DisconnectFrom(c1);
								}
								catch
								{
								}
								break;
							}
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
