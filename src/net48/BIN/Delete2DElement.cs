using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class Delete2DElement : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0302: Unknown result type (might be due to invalid IL or missing references)
		//IL_0306: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		UIApplication uiApp = commandData.Application;
		Document doc = uiApp.ActiveUIDocument.Document;
		try
		{
			Delete2DElementWindow window = new Delete2DElementWindow(doc);
			if (window.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			List<ElementId> idsToDelete = window.SelectedElementIds;
			string elementTypeName = window.SelectedElementTypeName;
			if (idsToDelete.Count > 0)
			{
				int deletedCount = 0;
				int failedCount = 0;
				List<string> failedElements = new List<string>();
				Transaction t = new Transaction(doc, "Delete " + elementTypeName);
				try
				{
					t.Start();
					foreach (ElementId id in idsToDelete)
					{
						try
						{
							Element elem = doc.GetElement(id);
							string elemName = ((elem != null) ? elem.Name : null) ?? ((object)id).ToString();
							if (elem != null && !elem.Pinned)
							{
								doc.Delete(id);
								deletedCount++;
							}
							else
							{
								failedCount++;
								failedElements.Add(elemName + " (Pinned)");
							}
						}
						catch (Exception ex)
						{
							failedCount++;
							Element elem2 = doc.GetElement(id);
							string elemName2 = ((elem2 != null) ? elem2.Name : null) ?? ((object)id).ToString();
							if (ex.Message.Contains("permission"))
							{
								failedElements.Add(elemName2 + " (No permission)");
							}
							else if (ex.Message.Contains("use") || ex.Message.Contains("referenced"))
							{
								failedElements.Add(elemName2 + " (In use)");
							}
							else
							{
								failedElements.Add(elemName2);
							}
						}
					}
					if (deletedCount > 0)
					{
						t.Commit();
					}
					else
					{
						t.RollBack();
					}
				}
				finally
				{
					((IDisposable)t)?.Dispose();
				}
				string resultMessage = $"Đã xóa thành công: {deletedCount}/{idsToDelete.Count} {elementTypeName}";
				if (failedCount > 0)
				{
					resultMessage += $"\n\nKhông thể xóa {failedCount} mục:";
					foreach (string name in failedElements.Take(10))
					{
						resultMessage = resultMessage + "\n• " + name;
					}
					if (failedElements.Count > 10)
					{
						resultMessage += $"\n... và {failedElements.Count - 10} mục khác";
					}
					MessageBox.Show(resultMessage, "Kết quả", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
				else
				{
					MessageBox.Show(resultMessage, "Kết quả", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
			}
			return Result.Succeeded;
		}
		catch (Exception ex2)
		{
			message = ex2.Message;
			return Result.Failed;
		}
	}
}
