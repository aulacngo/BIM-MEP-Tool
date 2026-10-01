using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ExportFamily : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0294: Unknown result type (might be due to invalid IL or missing references)
		//IL_0298: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0225: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0234: Expected O, but got Unknown
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		List<Family> allEditableFamilies = (from Family f in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(Family))
			where f.IsEditable && f.FamilyCategory != null
			select f).ToList();
		List<string> categoryNames = (from n in allEditableFamilies.Select((Family f) => f.FamilyCategory.Name).Distinct()
			orderby n
			select n).ToList();
		ExportFamilyWindow window = new ExportFamilyWindow(categoryNames);
		if (window.ShowDialog() != true || !window.SelectedCategoryNames.Any())
		{
			return Result.Cancelled;
		}
		string selectedPath = string.Empty;
		using (FolderBrowserDialog dialog = new FolderBrowserDialog())
		{
			if (dialog.ShowDialog() == DialogResult.OK)
			{
				selectedPath = dialog.SelectedPath;
			}
		}
		if (string.IsNullOrEmpty(selectedPath))
		{
			return Result.Failed;
		}
		List<Family> listToExport = allEditableFamilies.Where((Family f) => window.SelectedCategoryNames.Contains(f.FamilyCategory.Name)).ToList();
		using (ProcessbarView bv = new ProcessbarView(listToExport.Count))
		{
			bv.Show();
			foreach (Family family in listToExport)
			{
				if (bv.IsCanceled)
				{
					break;
				}
				bv.Step();
				try
				{
					Document familyDoc = doc.EditFamily(family);
					if (familyDoc != null)
					{
						string categoryFolder = Path.Combine(selectedPath, family.FamilyCategory.Name);
						if (!Directory.Exists(categoryFolder))
						{
							Directory.CreateDirectory(categoryFolder);
						}
						string cleanName = string.Join("_", ((Element)family).Name.Split(Path.GetInvalidFileNameChars()));
						string outputPath = Path.Combine(categoryFolder, cleanName + ".rfa");
						SaveAsOptions opt = new SaveAsOptions
						{
							OverwriteExistingFile = true
						};
						familyDoc.SaveAs(ModelPathUtils.ConvertUserVisiblePathToModelPath(outputPath), opt);
						familyDoc.Close(false);
					}
				}
				catch
				{
				}
			}
		}
		TaskDialog.Show("Thông báo", "Quá trình xuất Family hoàn tất!");
		return Result.Succeeded;
	}
}
