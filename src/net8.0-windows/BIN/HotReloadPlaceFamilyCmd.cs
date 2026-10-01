using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class HotReloadPlaceFamilyCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		try
		{
			string dllPath = @"d:\Tool Revit\src\net8.0-windows\bin\Release\net8.0-windows\BIN.dll";
			if (!File.Exists(dllPath))
			{
				return new PlaceFamily().Execute(commandData, ref message, elements);
			}

			byte[] assemblyBytes = File.ReadAllBytes(dllPath);
			byte[] pdbBytes = null;
			string pdbPath = @"d:\Tool Revit\src\net8.0-windows\bin\Release\net8.0-windows\BIN.pdb";
			if (File.Exists(pdbPath))
			{
				pdbBytes = File.ReadAllBytes(pdbPath);
			}

			Assembly asm = (pdbBytes != null) ? Assembly.Load(assemblyBytes, pdbBytes) : Assembly.Load(assemblyBytes);
			Type cmdType = asm.GetType("BIN.PlaceFamily");
			if (cmdType == null)
			{
				TaskDialog.Show("Lỗi", "Không tìm thấy type BIN.PlaceFamily trong DLL mới.");
				return Result.Failed;
			}

			IExternalCommand cmd = Activator.CreateInstance(cmdType) as IExternalCommand;
			return cmd.Execute(commandData, ref message, elements);
		}
		catch (Exception ex)
		{
			TaskDialog.Show("Hot Reload Lỗi", ex.Message + "\n" + ex.StackTrace);
			return Result.Failed;
		}
	}
}
