using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class OptimizePipeSegments : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Invalid comparison between Unknown and I4
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_036e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Expected O, but got Unknown
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0337: Unknown result type (might be due to invalid IL or missing references)
		//IL_0363: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017b: Expected O, but got Unknown
		//IL_017e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0184: Invalid comparison between Unknown and I4
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Expected O, but got Unknown
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		TaskDialog mainDialog = new TaskDialog("BIM Tool - Optimize Pipe");
		mainDialog.MainInstruction = "Huong dan su dung";
		mainDialog.MainContent = "Tool xoa Reducer va tao Tee combo.\nVui long chon cac Reducer can xu ly.\n\nLuu y: Da tu dong loai tru cac Reducer co kich thuoc 25x15 va DNx65+";
		mainDialog.CommonButtons = (TaskDialogCommonButtons)9;
		mainDialog.DefaultButton = (TaskDialogResult)1;
		if ((int)mainDialog.Show() == 2)
		{
			return Result.Cancelled;
		}
		ISelectionFilter filter = (ISelectionFilter)(object)new ReducerSelectFilter();
		IList<Reference> pickedRefs;
		try
		{
			pickedRefs = uidoc.Selection.PickObjects((ObjectType)1, filter, "Vui long chon cac Reducer can xu ly (Quet chon hoac Click tung cai, nhan Finish).");
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex2)
		{
			message = ex2.Message;
			return Result.Failed;
		}
		if (pickedRefs.Count == 0)
		{
			TaskDialog.Show("Thong bao", "Khong co Pipe Reducer hop le nao duoc chon theo tieu chi loc.");
			return Result.Succeeded;
		}
		List<FamilyInstance> selectedReducers = (from f in pickedRefs.Select(delegate(Reference r)
			{
				Element element = doc.GetElement(r);
				return (FamilyInstance)(object)((element is FamilyInstance) ? element : null);
			})
			where f != null
			select f).ToList();
		int processedCount = 0;
		Transaction t = new Transaction(doc, "Optimize Pipe Segments - Reducer Removal");
		try
		{
			try { t.GetFailureHandlingOptions().SetFailuresPreprocessor(new SuppressAllWarnings()); } catch { }
				t.Start();
			foreach (FamilyInstance reducer in selectedReducers)
			{
				ConnectorSet connectors = reducer.MEPModel.ConnectorManager.Connectors;
				List<Pipe> connectedPipes = new List<Pipe>();
				foreach (Connector item in connectors)
				{
					Connector connector = item;
					if ((int)connector.Domain != 3)
					{
						continue;
					}
					ConnectorSet refs = connector.AllRefs;
					foreach (Connector item2 in refs)
					{
						Connector refConnector = item2;
						Element connectedElement = refConnector.Owner;
						Pipe pipe = (Pipe)(object)((connectedElement is Pipe) ? connectedElement : null);
						if (pipe != null && !connectedPipes.Contains(pipe))
						{
							connectedPipes.Add(pipe);
						}
					}
				}
				if (connectedPipes.Count != 2)
				{
					continue;
				}
				Pipe pipeA = connectedPipes[0];
				Pipe pipeB = connectedPipes[1];
				Parameter sizeParamA = ((Element)pipeA).get_Parameter((BuiltInParameter)(-1140225));
				Parameter sizeParamB = ((Element)pipeB).get_Parameter((BuiltInParameter)(-1140225));
				if (sizeParamA == null || sizeParamB == null || !sizeParamA.HasValue || !sizeParamB.HasValue)
				{
					continue;
				}
				double pipeSizeA = sizeParamA.AsDouble();
				double pipeSizeB = sizeParamB.AsDouble();
				double smallSize;
				Pipe pipeToResize;
				if (pipeSizeA < pipeSizeB - 1E-06)
				{
					smallSize = pipeSizeA;
					pipeToResize = pipeB;
				}
				else
				{
					if (!(pipeSizeB < pipeSizeA - 1E-06))
					{
						continue;
					}
					smallSize = pipeSizeB;
					pipeToResize = pipeA;
				}
				((Element)pipeToResize).get_Parameter((BuiltInParameter)(-1140225)).Set(smallSize);
				processedCount++;
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		TaskDialog.Show("Ho\ufffdn th\ufffdnh", $"\ufffd\ufffd x? l\ufffd th\ufffdnh c\ufffdng {processedCount} Reducer.");
		return Result.Succeeded;
	}
}
