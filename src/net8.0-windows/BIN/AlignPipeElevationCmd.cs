using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class AlignPipeElevationCmd : IExternalCommand
{
	private class PipeElevationSelectionFilter : ISelectionFilter
	{
		public bool AllowElement(Element elem)
		{
			return elem is Pipe;
		}

		public bool AllowReference(Reference reference, XYZ position)
		{
			return false;
		}
	}

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;

		try
		{
			PipeElevationSelectionFilter filter = new PipeElevationSelectionFilter();

			// 1. Pick reference pipe (Ong chuan de lay cao do goc)
			Reference refPipeRef = uidoc.Selection.PickObject(
				ObjectType.Element, 
				filter, 
				"Chon ong CHUAN (lay cao do Middle Elevation goc)"
			);

			if (refPipeRef == null) return Result.Cancelled;

			Pipe refPipe = doc.GetElement(refPipeRef) as Pipe;
			if (refPipe == null) return Result.Failed;

			LocationCurve refLc = refPipe.Location as LocationCurve;
			if (refLc == null)
			{
				TaskDialog.Show("Align Pipe Elevation", "Ong chuan khong co LocationCurve hop le.");
				return Result.Failed;
			}

			XYZ refP0 = refLc.Curve.GetEndPoint(0);
			XYZ refP1 = refLc.Curve.GetEndPoint(1);
			double refMidZ = (refP0.Z + refP1.Z) / 2.0;

			int countSuccess = 0;

			// 2. Loop picking target pipes
			while (true)
			{
				Reference targetRef = null;
				try
				{
					targetRef = uidoc.Selection.PickObject(
						ObjectType.Element, 
						filter, 
						$"[Da can: {countSuccess} ong] Chon ong can MATCH cao do (Nhan ESC de ket thuc)"
					);
				}
				catch (Autodesk.Revit.Exceptions.OperationCanceledException)
				{
					break; // User pressed ESC to exit loop
				}

				if (targetRef == null) break;

				#if NET8_0_OR_GREATER || NETCOREAPP
				if (targetRef.ElementId.Value == refPipe.Id.Value) continue;
				#else
				if (targetRef.ElementId.IntegerValue == refPipe.Id.IntegerValue) continue;
				#endif

				Pipe targetPipe = doc.GetElement(targetRef) as Pipe;
				if (targetPipe == null) continue;

				LocationCurve targetLc = targetPipe.Location as LocationCurve;
				if (targetLc == null) continue;

				XYZ targetP0 = targetLc.Curve.GetEndPoint(0);
				XYZ targetP1 = targetLc.Curve.GetEndPoint(1);
				double targetMidZ = (targetP0.Z + targetP1.Z) / 2.0;

				double deltaZ = refMidZ - targetMidZ;
				if (Math.Abs(deltaZ) < 1e-5)
				{
					// Da cung cao do
					continue;
				}

				bool success = false;

				// Strategy 1: Update RBS_OFFSET_PARAM (Middle Elevation) truc tiep giong Properties Palette
				using (Transaction trParam = new Transaction(doc, "Align Pipe Elevation (Param)"))
				{
					trParam.Start();
					try
					{
						Parameter offsetParam = targetPipe.get_Parameter(BuiltInParameter.RBS_OFFSET_PARAM);
						if (offsetParam != null && !offsetParam.IsReadOnly)
						{
							double currentOffset = offsetParam.AsDouble();
							offsetParam.Set(currentOffset + deltaZ);
							trParam.Commit();
							success = true;
							countSuccess++;
						}
						else
						{
							trParam.RollBack();
						}
					}
					catch
					{
						if (trParam.GetStatus() == TransactionStatus.Started)
						{
							trParam.RollBack();
						}
					}
				}

				// Strategy 2: Neu sua Parameter khong thanh cong, dung ElementTransformUtils.MoveElement
				// (MoveElement se tu dong co/gian ong dung noi voi co cut)
				if (!success)
				{
					using (Transaction trMove = new Transaction(doc, "Align Pipe Elevation (Move)"))
					{
						trMove.Start();
						try
						{
							ElementTransformUtils.MoveElement(doc, targetPipe.Id, new XYZ(0, 0, deltaZ));
							trMove.Commit();
							countSuccess++;
						}
						catch
						{
							if (trMove.GetStatus() == TransactionStatus.Started)
							{
								trMove.RollBack();
							}
						}
					}
				}
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
