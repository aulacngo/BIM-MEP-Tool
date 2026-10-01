using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class CopyView : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_0236: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Expected O, but got Unknown
		IntPtr revitHandle = Process.GetCurrentProcess().MainWindowHandle;
		Document doc = commandData.Application.ActiveUIDocument.Document;
		List<View> views = (from View v in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(View))
			where !v.IsTemplate && v.CanViewBeDuplicated((ViewDuplicateOption)0) && (int)v.ViewType != 11 && (int)v.ViewType != 5 && (int)v.ViewType != 123
			orderby ((Element)v).Name
			select v).ToList();
		CopyViewWindow ui = new CopyViewWindow(views);
		WindowInteropHelper helper = new WindowInteropHelper(ui);
		helper.Owner = revitHandle;
		if (ui.ShowDialog() == true)
		{
			Transaction t = new Transaction(doc, "CopyView_Tool");
			try
			{
				t.Start();
				foreach (View v2 in ui.ResultViews)
				{
					for (int i = 0; i < ui.NumCopies; i++)
					{
						try
						{
							ElementId newViewId = v2.Duplicate((ViewDuplicateOption)0);
							Element element = doc.GetElement(newViewId);
							View newView = (View)(object)((element is View) ? element : null);
							if (newView != null)
							{
								List<ElementId> cadIds = (from ImportInstance x in (IEnumerable)new FilteredElementCollector(doc, ((Element)v2).Id).OfClass(typeof(ImportInstance))
									where ((Element)x).OwnerViewId == ((Element)v2).Id
									select ((Element)x).Id).ToList();
								if (cadIds.Count > 0)
								{
									ElementTransformUtils.CopyElements(v2, (ICollection<ElementId>)cadIds, newView, Transform.Identity, new CopyPasteOptions());
								}
							}
						}
						catch
						{
						}
					}
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			return Result.Succeeded;
		}
		return Result.Cancelled;
	}
}
