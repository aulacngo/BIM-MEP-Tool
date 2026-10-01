using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class DrawDrainagePipe_AllTypes : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			DrainagePipeTypePickerWindow ui = new DrainagePipeTypePickerWindow();
			WindowInteropHelper helper = new WindowInteropHelper(ui);
			helper.Owner = commandData.Application.MainWindowHandle;
			if (ui.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			DrainagePipeOffsets.SetFromMm(ui.OffsetElbowMm, ui.OffsetYMm, ui.OffsetHMm, ui.OffsetZMm);
			return ((IExternalCommand)(ui.SelectedTypeIndex switch
			{
				0 => new DrawDrainagePipeType1(), 
				1 => new DrawDrainagePipeType2(), 
				2 => new DrawDrainagePipeType3(), 
				3 => new DrawDrainagePipeType4(), 
				4 => new DrawDrainagePipeType5(), 
				5 => new DrawDrainagePipeType6(), 
				_ => new DrawDrainagePipeType1(), 
			})).Execute(commandData, ref message, elements);
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}
}
