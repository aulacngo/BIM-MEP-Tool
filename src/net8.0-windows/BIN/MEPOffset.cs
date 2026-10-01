using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class MEPOffset : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_01bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			MEPOffsetWindow window = new MEPOffsetWindow();
			if (window.ShowDialog() != true)
			{
				return Result.Cancelled;
			}
			double offsetMm = window.Offset;
			double offsetFeet = offsetMm / 304.8;
			string angle = window.Angle;
			bool isTop = window.Option == "Cut Up";
			int mepType = window.MEPType;
			string mepName = mepType switch
			{
				1 => "duct", 
				0 => "pipe", 
				_ => "cable tray", 
			};
			ISelectionFilter obj;
			switch (mepType)
			{
			default:
			{
				ISelectionFilter val = (ISelectionFilter)(object)new SelectCableTrays();
				obj = val;
				break;
			}
			case 1:
			{
				ISelectionFilter val = (ISelectionFilter)(object)new SelectDucts();
				obj = val;
				break;
			}
			case 0:
			{
				ISelectionFilter val = (ISelectionFilter)(object)new SelectPipes();
				obj = val;
				break;
			}
			}
			ISelectionFilter filter = obj;
			string prompt1 = "Pick first point on " + mepName;
			string prompt2 = "Pick second point on " + mepName;
			Reference r1 = uidoc.Selection.PickObject((ObjectType)1, filter, prompt1);
			Reference r2 = uidoc.Selection.PickObject((ObjectType)1, filter, prompt2);
			switch (mepType)
			{
			case 0:
				if (angle == "45")
				{
					PipeOffsetUtils.PipeCut45(doc, r1, r2, offsetFeet, isTop);
				}
				else
				{
					PipeOffsetUtils.PipeCut90(doc, r1, r2, offsetFeet, isTop);
				}
				break;
			case 1:
				if (angle == "45")
				{
					DuctOffsetUltils.DuctCut45(doc, r1, r2, offsetFeet, isTop);
				}
				else
				{
					DuctOffsetUltils.DuctCut90(doc, r1, r2, offsetFeet, isTop);
				}
				break;
			case 2:
				if (angle == "45")
				{
					CableTrayOffsetUltils.CableTrayCut45(doc, r1, r2, offsetFeet, isTop);
				}
				else
				{
					CableTrayOffsetUltils.CableTrayCut90(doc, r1, r2, offsetFeet, isTop);
				}
				break;
			}
			return Result.Succeeded;
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
