using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIN.SelectByParam.ViewModels;
using BIN.SelectByParam.Views;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class SelectByParameterCommand : IExternalCommand
{
	private static SelectByParameterView _currentView;

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (_currentView != null && _currentView.IsLoaded)
			{
				_currentView.Activate();
				_currentView.Focus();
				return Result.Succeeded;
			}
			UIApplication uiapp = commandData.Application;
			SelectByParameterViewModel viewModel = new SelectByParameterViewModel(uiapp);
			_currentView = new SelectByParameterView(viewModel);
			_currentView.Closed += delegate
			{
				_currentView = null;
			};
			_currentView.Show();
			return Result.Succeeded;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}
}
