using System;
using Autodesk.Revit.UI;

namespace BIN.Common.Revit;

public class RevitApiHandler : IExternalEventHandler
{
	public delegate Result RevitAction(UIApplication uiapp);

	public RevitAction Action { get; set; }

	public void Execute(UIApplication uiapp)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Action?.Invoke(uiapp);
		}
		catch (Exception ex)
		{
			TaskDialog.Show("RevitApiHandler Error", ex.Message);
		}
	}

	public string GetName()
	{
		return "RevitApiHandler";
	}
}
