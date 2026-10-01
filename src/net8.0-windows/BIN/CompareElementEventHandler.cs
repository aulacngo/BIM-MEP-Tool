using System;
using Autodesk.Revit.UI;

namespace BIN;

public class CompareElementEventHandler : IExternalEventHandler
{
	public Action Action { get; set; }

	public void Execute(UIApplication app)
	{
		try
		{
			Action?.Invoke();
		}
		catch
		{
		}
	}

	public string GetName()
	{
		return "CompareElementEvent";
	}
}
