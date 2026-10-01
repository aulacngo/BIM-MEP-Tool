using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIN.Sheet_From_Excel;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class SheetFromExcel : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		View view = doc.ActiveView;
		SheetFromExcelView window = new SheetFromExcelView(doc);
		window.ShowDialog();
		return Result.Succeeded;
	}
}
