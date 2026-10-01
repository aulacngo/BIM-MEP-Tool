# Prompt Codex: Update Dedicated Ribbon Button Icon References in Panel.cs

## Target Files
1. `D:\Tool Revit\src\net48\BIN\Panel.cs`
2. `D:\Tool Revit\src\net8.0-windows\BIN\Panel.cs`

## Context & Requirement
The project has newly generated dedicated 32x32 and 16x16 PNG icons in `Resources/` for 7 commands that were previously borrowing icons from other tools.
Update both `Panel.cs` files so that the fourth argument (`imgName`) of `CreatePushData` uses the dedicated icon names:

1. `AlignPipeElevation`:
   Change image from `"AlignIn3D"` to `"AlignPipeElevation"`.
   Line in `Micro_Tool`:
   `sbAlign.AddPushButton(CreatePushData("AlignPipeElevation", "Align\nElev", "AlignPipeElevationCmd", "AlignPipeElevation", "Align Pipe Elevation - Can bang cao do ong ngang (Middle Elevation)"));`

2. `ConnectSprinklerFlexPipe`:
   Change image from `"ConnectSprinklerToPipe"` to `"ConnectSprinklerFlexPipe"`.
   Line in `Sprinkler_Tool`:
   `CreatePushData("ConnectSprinklerFlexPipe", "Sprinkler\nFlex Pipe", "ConnectSprinklerFlexPipeCmd", "ConnectSprinklerFlexPipe", "Connect Sprinkler to Pipe using Flexible Pipe and Reducer"),`

3. `ConnectSprinklerFlexMulti`:
   Change image from `"ConnectSprinklerToPipe"` to `"ConnectSprinklerFlexMulti"`.
   Line in `Sprinkler_Tool`:
   `CreatePushData("ConnectSprinklerFlexMulti", "Sprinkler\nFlex Multi", "ConnectSprinklerFlexPipeMultiCmd", "ConnectSprinklerFlexMulti", "Preselect multiple open-end Pipes and Sprinklers, then connect all pairs using Elbows"),`

4. `CheckUndefinedPipe`:
   Change image from `"CheckConnectedPipe"` to `"CheckUndefinedPipe"`.
   Line in `Check_Tool`:
   `CreatePushData("CheckUndefinedPipe", "Check\nUndefined Pipe", "CheckUndefinedPipeCmd", "CheckUndefinedPipe", "Check Undefined Pipe"),`

5. `FlexDuctAvoidMep`:
   Change image from `"CheckAirTerminal"` to `"FlexDuctAvoidMep"`.
   Line in `Check_Tool`:
   `CreatePushData("FlexDuctAvoidMep", "Flex Duct\nAvoid MEP", "FlexDuctAvoidMepCmd", "FlexDuctAvoidMep", "Flex Duct Avoid MEP"),`

6. `CheckPipeClash`:
   Change image from `"CheckPipeFitting"` to `"CheckPipeClash"`.
   Line in `Check_Tool`:
   `CreatePushData("CheckPipeClash", "Pipe Clash\nClearance", "CheckPipeClashCmd", "CheckPipeClash", "Local 2-3 Pipe Clash and insulation clearance check")`

7. `CopyFilter`:
   Change image from `"CopyView"` to `"CopyFilter"`.
   Line in `SheetView_Tool`:
   `CreatePushData("CopyFilter", "Copy\nFilter", "CopyFilterCmd", "CopyFilter", "Copy view filters to other views")`

## Constraints
- Modify ONLY these exact 7 icon name parameters in both `src/net48/BIN/Panel.cs` and `src/net8.0-windows/BIN/Panel.cs`.
- Maintain identical formatting, indentation, and structure across both files.
