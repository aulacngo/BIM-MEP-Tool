using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class SprinklerFlipper : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0229: Unknown result type (might be due to invalid IL or missing references)
		//IL_0230: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Expected O, but got Unknown
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			List<FamilySymbol> allSprinklerSymbols = (from FamilySymbol x in (IEnumerable)new FilteredElementCollector(doc).OfCategory((BuiltInCategory)(-2008099)).OfClass(typeof(FamilySymbol))
				orderby ((ElementType)x).FamilyName, ((Element)x).Name
				select x).ToList();
			if (!allSprinklerSymbols.Any())
			{
				TaskDialog.Show("Thong bao", "Khong tim thay Family Sprinkler nao trong du an.");
				return Result.Failed;
			}
			SprinklerSelectionWindow ui = new SprinklerSelectionWindow(allSprinklerSymbols);
			if (ui.ShowDialog() != true || ui.SelectedSymbol == null)
			{
				return Result.Cancelled;
			}
			FamilySymbol targetSymbol = ui.SelectedSymbol;
			SprinklerSelectionFilter filter = new SprinklerSelectionFilter();
			IList<Reference> refs;
			try
			{
				refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)filter, "Quet chon cac cum Sprinkler can lat (nhan Finish de chay)");
			}
			catch (Autodesk.Revit.Exceptions.OperationCanceledException)
			{
				return Result.Cancelled;
			}
			if (refs.Count == 0)
			{
				return Result.Cancelled;
			}
			SprinklerFlipperLogic logic = new SprinklerFlipperLogic();
			int successCount = 0;
			int failCount = 0;
			Transaction t = new Transaction(doc, "Flip Sprinklers with UI Selection");
			try
			{
				t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings());
				t.Start();
				if (!targetSymbol.IsActive)
				{
					targetSymbol.Activate();
				}
				foreach (Reference r in refs)
				{
					Element element = doc.GetElement(r);
					FamilyInstance sprinkler = (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
					if (sprinkler != null)
					{
						try
						{
							logic.Process(doc, sprinkler, targetSymbol);
							successCount++;
						}
						catch
						{
							failCount++;
						}
					}
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			TaskDialog.Show("Hoan thanh", $"Da lat xong {successCount} cụm Sprinkler sang loai: \n{((ElementType)targetSymbol).FamilyName} - {((Element)targetSymbol).Name}\nLoi / Bo qua: {failCount}");
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}
}
