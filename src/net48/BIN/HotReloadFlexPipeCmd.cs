using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class HotReloadFlexPipeCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		try
		{
			// Load DLL dạng byte stream để không bao giờ bị khóa file (NO FILE LOCK)
			string dllPath = @"d:\Tool Revit\src\net48\bin\Release\net48\BIN.dll";
			if (!File.Exists(dllPath))
			{
				// Máy triển khai không có source: chạy command đã đóng gói trong chính BIN.dll.
				return new ConnectSprinklerFlexPipeCmd().Execute(commandData, ref message, elements);
			}

			byte[] assemblyBytes = File.ReadAllBytes(dllPath);
			byte[] pdbBytes = null;
			string pdbPath = @"d:\Tool Revit\src\net48\bin\Release\net48\BIN.pdb";
			if (File.Exists(pdbPath))
			{
				pdbBytes = File.ReadAllBytes(pdbPath);
			}

			Assembly asm = (pdbBytes != null) ? Assembly.Load(assemblyBytes, pdbBytes) : Assembly.Load(assemblyBytes);
			Type cmdType = asm.GetType("BIN.ConnectSprinklerFlexPipeCmd");
			if (cmdType == null)
			{
				TaskDialog.Show("Lỗi", "Không tìm thấy type BIN.ConnectSprinklerFlexPipeCmd trong DLL mới.");
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
