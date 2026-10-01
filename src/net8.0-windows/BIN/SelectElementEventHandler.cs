using System;
using Autodesk.Revit.UI;

namespace BIN;

public class SelectElementEventHandler : IExternalEventHandler
{
	public Action Action { get; set; }

	public void Execute(UIApplication app)
	{
		Action?.Invoke();
	}

	public string GetName()
	{
		return "SelectElementEventHandler";
	}
}
