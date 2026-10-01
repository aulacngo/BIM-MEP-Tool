using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public static class DevCommandRegistry
{
	private static readonly Dictionary<string, string> Commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	public static void Register(string key, string className) { Commands[key] = className; }
	public static string Get(string key) { return Commands.TryGetValue(key, out string className) ? className : null; }
}

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public abstract class DevCommandProxy : IExternalCommand
{
	protected abstract string CommandKey { get; }
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		string className = DevCommandRegistry.Get(CommandKey);
		string assemblyPath = null;
		try
		{
			if (string.IsNullOrWhiteSpace(className)) throw new InvalidOperationException("Command is not registered: " + CommandKey);
			assemblyPath = GetDevelopmentAssemblyPath();
			Assembly assembly = Assembly.GetExecutingAssembly();
			if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath)) assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
			CommandDiagnostics.Write(className, "started", commandData, assemblyPath: assemblyPath ?? assembly.Location);
			Type commandType = assembly.GetType("BIN." + className, false);
			if (commandType == null || !typeof(IExternalCommand).IsAssignableFrom(commandType)) throw new InvalidOperationException("Cannot find IExternalCommand BIN." + className + " in " + (assemblyPath ?? assembly.Location));
			IExternalCommand command = (IExternalCommand)Activator.CreateInstance(commandType);
			Result result = command.Execute(commandData, ref message, elements);
			CommandDiagnostics.Write(className, result == Result.Failed ? "failed" : "completed", commandData, result, message, assemblyPath: assemblyPath ?? assembly.Location);
			return result;
		}
		catch (Exception ex)
		{
			Exception actual = ex is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : ex;
			CommandDiagnostics.Write(className ?? CommandKey, "failed", commandData, Result.Failed, message, actual, assemblyPath);
			TaskDialog.Show("BIM TOOL - " + (className ?? CommandKey), actual.Message + "\n\nChi tiết đã được ghi vào diagnostics.");
			return Result.Cancelled;
		}
	}
	private static string GetDevelopmentAssemblyPath()
	{
		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string configPath = Path.Combine(localAppData, "BIM TOOL", "Development", "hotreload-net48.path");
		if (!File.Exists(configPath))
		{
			configPath = Path.Combine(localAppData, "BIN TOOL", "Development", "hotreload-net48.path");
		}
		if (!File.Exists(configPath)) return null;
		string path = File.ReadAllText(configPath).Trim().Trim('"');
		return string.IsNullOrWhiteSpace(path) ? null : path;
	}
}

[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_OptimizePipeSegments : DevCommandProxy { protected override string CommandKey => "OptimizePipeSegments"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ReplacePipeWithNipple : DevCommandProxy { protected override string CommandKey => "ReplacePipeWithNipple"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ShortenSprinkler : DevCommandProxy { protected override string CommandKey => "ShortenSprinkler"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ToReducingElbow : DevCommandProxy { protected override string CommandKey => "ToReducingElbow"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_SprinklerFlipper : DevCommandProxy { protected override string CommandKey => "SprinklerFlipper"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_RemovePipeCoupling : DevCommandProxy { protected override string CommandKey => "RemovePipeCoupling"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ConnectSprinklerToPipe : DevCommandProxy { protected override string CommandKey => "ConnectSprinklerToPipe"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ConnectSprinklerFlexPipe : DevCommandProxy { protected override string CommandKey => "ConnectSprinklerFlexPipe"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ConnectSprinklerFlexMulti : DevCommandProxy { protected override string CommandKey => "ConnectSprinklerFlexMulti"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ConnectTeeAndReducer_Groove : DevCommandProxy { protected override string CommandKey => "ConnectTeeAndReducer_Groove"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CheckSprinkler : DevCommandProxy { protected override string CommandKey => "CheckSprinkler"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_PlaceMultiPipeSupport : DevCommandProxy { protected override string CommandKey => "PlaceMultiPipeSupport"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_PlaceSupport : DevCommandProxy { protected override string CommandKey => "PlaceSupport"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_AlignSupport : DevCommandProxy { protected override string CommandKey => "AlignSupport"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_MoveSupport : DevCommandProxy { protected override string CommandKey => "MoveSupport"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ConnectVentRiser : DevCommandProxy { protected override string CommandKey => "ConnectVentRiser"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_Create2Elbows45 : DevCommandProxy { protected override string CommandKey => "Create2Elbows45"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_Join2Elbows : DevCommandProxy { protected override string CommandKey => "Join2Elbows"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_TurnDrainagePipe : DevCommandProxy { protected override string CommandKey => "TurnDrainagePipe"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_DrawDrainagePipeType1 : DevCommandProxy { protected override string CommandKey => "DrawDrainagePipeType1"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_DrawCleanOut : DevCommandProxy { protected override string CommandKey => "DrawCleanOut"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_Bloom : DevCommandProxy { protected override string CommandKey => "Bloom"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_SplitDuct : DevCommandProxy { protected override string CommandKey => "SplitDuct"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_PlaceFamily : DevCommandProxy { protected override string CommandKey => "PlaceFamily"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_RotateFamilyInstance : DevCommandProxy { protected override string CommandKey => "RotateFamilyInstance"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ChangeReferenceLevel : DevCommandProxy { protected override string CommandKey => "ChangeReferenceLevel"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ToggleInsulation : DevCommandProxy { protected override string CommandKey => "ToggleInsulation"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_TurnMEP : DevCommandProxy { protected override string CommandKey => "TurnMEP"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_MEPOffset : DevCommandProxy { protected override string CommandKey => "MEPOffset"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CutMEPByLevel : DevCommandProxy { protected override string CommandKey => "CutMEPByLevel"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_PipeInsulation : DevCommandProxy { protected override string CommandKey => "PipeInsulation"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_DrawMultiPipe : DevCommandProxy { protected override string CommandKey => "DrawMultiPipe"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ManageElement : DevCommandProxy { protected override string CommandKey => "ManageElement"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_SelectByParameter : DevCommandProxy { protected override string CommandKey => "SelectByParameter"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CompareElement : DevCommandProxy { protected override string CommandKey => "CompareElement"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ClashDetective : DevCommandProxy { protected override string CommandKey => "ClashDetective"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_Trim3D : DevCommandProxy { protected override string CommandKey => "Trim3D"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CheckAirTerminal : DevCommandProxy { protected override string CommandKey => "CheckAirTerminal"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CheckPipeFitting : DevCommandProxy { protected override string CommandKey => "CheckPipeFitting"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CheckConnectedPipe : DevCommandProxy { protected override string CommandKey => "CheckConnectedPipe"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CheckUndefinedPipe : DevCommandProxy { protected override string CommandKey => "CheckUndefinedPipe"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CheckPipeClash : DevCommandProxy { protected override string CommandKey => "CheckPipeClash"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_FlexDuctAvoidMep : DevCommandProxy { protected override string CommandKey => "FlexDuctAvoidMep"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_GetId : DevCommandProxy { protected override string CommandKey => "GetId"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_MoveConnect : DevCommandProxy { protected override string CommandKey => "MoveConnect"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_Disconnect : DevCommandProxy { protected override string CommandKey => "Disconnect"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ElbowUp : DevCommandProxy { protected override string CommandKey => "ElbowUp"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ElbowUp45 : DevCommandProxy { protected override string CommandKey => "ElbowUp45"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ElbowDown : DevCommandProxy { protected override string CommandKey => "ElbowDown"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ElbowDown45 : DevCommandProxy { protected override string CommandKey => "ElbowDown45"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ElbowLeft : DevCommandProxy { protected override string CommandKey => "ElbowLeft"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ElbowRight : DevCommandProxy { protected override string CommandKey => "ElbowRight"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ElbowLeft45 : DevCommandProxy { protected override string CommandKey => "ElbowLeft45"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ElbowRight45 : DevCommandProxy { protected override string CommandKey => "ElbowRight45"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] [Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_AvoidClash : DevCommandProxy { protected override string CommandKey => "AvoidClash"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_RotateElements : DevCommandProxy { protected override string CommandKey => "RotateElements"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_RotateMulti : DevCommandProxy { protected override string CommandKey => "RotateMulti"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_DeleteSystem : DevCommandProxy { protected override string CommandKey => "DeleteSystem"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_DeleteOrphanInsu : DevCommandProxy { protected override string CommandKey => "DeleteOrphanInsu"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ThreeDAlign : DevCommandProxy { protected override string CommandKey => "ThreeDAlign"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_BranchAlignLite : DevCommandProxy { protected override string CommandKey => "BranchAlignLite"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_AlignPipeElevation : DevCommandProxy { protected override string CommandKey => "AlignPipeElevation"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_SheetFromExcel : DevCommandProxy { protected override string CommandKey => "SheetFromExcel"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ExportFamily : DevCommandProxy { protected override string CommandKey => "ExportFamily"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_TransferDocument : DevCommandProxy { protected override string CommandKey => "TransferDocument"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CopyView : DevCommandProxy { protected override string CommandKey => "CopyView"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CopySheet : DevCommandProxy { protected override string CommandKey => "CopySheet"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ExportScheduleToExcel : DevCommandProxy { protected override string CommandKey => "ExportScheduleToExcel"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_Delete2DElement : DevCommandProxy { protected override string CommandKey => "Delete2DElement"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_Rename2DElement : DevCommandProxy { protected override string CommandKey => "Rename2DElement"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ManageGridLevel : DevCommandProxy { protected override string CommandKey => "ManageGridLevel"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_ToggleSectionHead : DevCommandProxy { protected override string CommandKey => "ToggleSectionHead"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_BOQ : DevCommandProxy { protected override string CommandKey => "BOQ"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CopyFromLink : DevCommandProxy { protected override string CommandKey => "CopyFromLink"; }
[Transaction(TransactionMode.Manual)] [Regeneration(RegenerationOption.Manual)] public class DevProxy_CopyFilter : DevCommandProxy { protected override string CommandKey => "CopyFilter"; }
