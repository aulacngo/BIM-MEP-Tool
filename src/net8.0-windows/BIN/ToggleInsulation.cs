using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ToggleInsulation : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Expected O, but got Unknown
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		View activeView = doc.ActiveView;
		ToggleInsulationWindow ui = new ToggleInsulationWindow();
		if (ui.ShowDialog() != true)
		{
			return Result.Cancelled;
		}
		try
		{
			Transaction trans = new Transaction(doc, "Toggle Insulation");
			try
			{
				trans.Start();
				OverrideGraphicSettings overrideSettings = new OverrideGraphicSettings();
				if (ui.IsPipeInsulation)
				{
					Category pipeInsulationCat = doc.Settings.Categories.get_Item((BuiltInCategory)(-2008122));
					if (pipeInsulationCat != null)
					{
						ApplyVisibilitySettings(activeView, pipeInsulationCat, ui.SelectedOption, overrideSettings);
					}
				}
				if (ui.IsDuctInsulation)
				{
					Category ductInsulationCat = doc.Settings.Categories.get_Item((BuiltInCategory)(-2008123));
					if (ductInsulationCat != null)
					{
						ApplyVisibilitySettings(activeView, ductInsulationCat, ui.SelectedOption, overrideSettings);
					}
				}
				trans.Commit();
			}
			finally
			{
				((IDisposable)trans)?.Dispose();
			}
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private void ApplyVisibilitySettings(View view, Category category, int option, OverrideGraphicSettings overrideSettings)
	{
		if (view == null || category == null) return;
		switch (option)
		{
		case 0:
			if (view.CanCategoryBeHidden(category.Id))
			{
				view.SetCategoryHidden(category.Id, false);
			}
			if (view.AreGraphicsOverridesAllowed())
			{
				overrideSettings.SetSurfaceTransparency(0);
				view.SetCategoryOverrides(category.Id, overrideSettings);
			}
			break;
		case 1:
			if (view.CanCategoryBeHidden(category.Id))
			{
				view.SetCategoryHidden(category.Id, false);
			}
			if (view.AreGraphicsOverridesAllowed())
			{
				overrideSettings.SetSurfaceTransparency(50);
				view.SetCategoryOverrides(category.Id, overrideSettings);
			}
			break;
		case 2:
			if (view.CanCategoryBeHidden(category.Id))
			{
				view.SetCategoryHidden(category.Id, true);
			}
			break;
		}
	}
}
