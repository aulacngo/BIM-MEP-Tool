using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class CopyFromLinkCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Expected O, but got Unknown
		UIApplication app = commandData.Application;
		UIDocument uidoc = app.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			TaskDialog.Show("Thông báo", "Vui lòng chọn đối tượng trong file link cần copy");
			ISelectionFilter linkFilter = (ISelectionFilter)(object)new LinkSelectionFilter();
			IList<Reference> refs = uidoc.Selection.PickObjects((ObjectType)5, linkFilter, "Vui lòng chọn đối tượng trong file link cần copy");
			if (refs == null || refs.Count == 0)
			{
				return Result.Cancelled;
			}
			IEnumerable<IGrouping<ElementId, Reference>> groupedRefs = from r in refs
				group r by r.ElementId;
			Transaction tx = new Transaction(doc, "Copy From Link File");
			try
			{
				tx.Start();
				foreach (IGrouping<ElementId, Reference> group in groupedRefs)
				{
					ElementId linkInstanceId = group.Key;
					Element element = doc.GetElement(linkInstanceId);
					RevitLinkInstance linkInstance = (RevitLinkInstance)(object)((element is RevitLinkInstance) ? element : null);
					if (linkInstance == null)
					{
						continue;
					}
					Document linkDoc = linkInstance.GetLinkDocument();
					if (linkDoc != null)
					{
						ICollection<ElementId> elementsToCopy = group.Select((Reference r) => r.LinkedElementId).ToList();
						Transform transform = ((Instance)linkInstance).GetTransform();
						CopyPasteOptions options = new CopyPasteOptions();
						ElementTransformUtils.CopyElements(linkDoc, elementsToCopy, doc, transform, options);
					}
				}
				tx.Commit();
			}
			finally
			{
				((IDisposable)tx)?.Dispose();
			}
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			TaskDialog.Show("Lỗi", "Không thể copy đối tượng. Lỗi: " + ex.Message);
			return Result.Failed;
		}
	}
}
