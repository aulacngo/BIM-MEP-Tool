using System;
using Autodesk.Revit.UI;

namespace BIN;

public class CheckSprinklerHandler : IExternalEventHandler
{
	public delegate void RevitAction(UIApplication uiapp);

	public RevitAction Action { get; set; }

	public void Execute(UIApplication uiapp)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Action?.Invoke(uiapp);
		}
		catch (Exception ex)
		{
			TaskDialog.Show("Check Sprinkler Error", ex.Message);
		}
	}

	public string GetName()
	{
		return "CheckSprinklerHandler";
	}
}
