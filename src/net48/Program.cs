using System;
using System.Reflection;

internal class Program
{
	private static void Main()
	{
		Assembly asm = Assembly.LoadFrom("C:\\Program Files\\Autodesk\\Revit 2023\\RevitAPI.dll");
		Type mepCurveType = asm.GetType("Autodesk.Revit.DB.MEPCurve");
		PropertyInfo[] properties = mepCurveType.GetProperties();
		foreach (PropertyInfo p in properties)
		{
			if (p.Name.Contains("Level"))
			{
				Console.WriteLine("MEPCurve Property: " + p.Name + " : " + p.PropertyType.Name);
			}
		}
		Type elemType = asm.GetType("Autodesk.Revit.DB.Element");
		PropertyInfo[] properties2 = elemType.GetProperties();
		foreach (PropertyInfo p2 in properties2)
		{
			if (p2.Name.Contains("Level"))
			{
				Console.WriteLine("Element Property: " + p2.Name + " : " + p2.PropertyType.Name);
			}
		}
	}
}
