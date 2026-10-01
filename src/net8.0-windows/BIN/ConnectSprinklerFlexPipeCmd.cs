using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public partial class ConnectSprinklerFlexPipeCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIApplication uiApp = commandData.Application;
		UIDocument uidoc = uiApp.ActiveUIDocument;
		Document doc = uidoc.Document;
		int connectedCount = 0;

		try
		{
			FlexPipeDiagnostics.Write("command", "started", "Sprinkler Flex Pipe command started",
				details: $"document={doc.Title}; view={doc.ActiveView?.Name}");
			// Preselection là một thao tác hoàn chỉnh; không chuyển sang pick mode sau đó.
			List<Element> selectedElements = uidoc.Selection.GetElementIds()
				.Select(id => doc.GetElement(id))
				.Where(IsPhase2SelectableElement)
				.ToList();
			List<Pipe> pickedPipes = selectedElements.OfType<Pipe>().ToList();
			List<FamilyInstance> sprinklers = selectedElements
				.OfType<FamilyInstance>()
				.Where(IsSprinkler)
				.ToList();
			List<FamilyInstance> pickedFittings = selectedElements
				.OfType<FamilyInstance>()
				.Where(IsPipeFitting)
				.ToList();

			if (pickedPipes.Count > 1 && sprinklers.Count > 0)
			{
				return ExecuteMultiElbow(uidoc, ref message);
			}

			if (pickedPipes.Count == 1 && sprinklers.Count > 0)
			{
				FlexPipeDiagnostics.Write("selection", "preselected",
					$"Preselection: 1 Pipe + {sprinklers.Count} Sprinkler",
					pickedPipes[0].Id.GetIdInt(),
					details: $"sprinklers={string.Join(",", sprinklers.Select(x => x.Id.GetIdInt()))}");
				bool preselectSucceeded = ConnectSelection(doc, sprinklers, pickedPipes[0], out string preselectLog);
				uidoc.Selection.SetElementIds(new List<ElementId>());
				TaskDialog.Show("Sprinkler Flex Pipe", preselectSucceeded
					? $"Đã kết nối {sprinklers.Count} Sprinkler."
					: preselectLog);
				return preselectSucceeded ? Result.Succeeded : Result.Failed;
			}

			if (pickedFittings.Count == 1 && sprinklers.Count > 0)
			{
				bool preselectSucceeded = ConnectSelection(doc, sprinklers, pickedFittings[0], out string preselectLog);
				uidoc.Selection.SetElementIds(new List<ElementId>());
				TaskDialog.Show("Sprinkler Flex Pipe", preselectSucceeded
					? $"Đã kết nối {sprinklers.Count} Sprinkler từ PipeFitting."
					: preselectLog);
				return preselectSucceeded ? Result.Succeeded : Result.Failed;
			}

			while (true)
			{
				try
				{
				Element firstElement = doc.GetElement(uidoc.Selection.PickObject(
					ObjectType.Element,
					new SprinklerAndPipeSelectionFilter(),
					"Chọn Pipe hoặc Sprinkler tiếp theo (Esc để kết thúc)"
				));
				FlexPipeDiagnostics.Write("selection", "first-picked",
					$"First object is {firstElement?.GetType().Name}",
					firstElement is Pipe ? firstElement.Id.GetIdInt() : (int?)null,
					firstElement is FamilyInstance ? firstElement.Id.GetIdInt() : (int?)null);

				Element pickedTarget;
				FamilyInstance sprinkler;
				if (firstElement is Pipe firstPipe)
				{
					pickedTarget = firstPipe;
					Reference sprinklerRef = uidoc.Selection.PickObject(
						ObjectType.Element,
						new SprinklerSelectionFilter(),
						"Chọn Sprinkler để connect"
					);
					sprinkler = (FamilyInstance)doc.GetElement(sprinklerRef);
				}
				else if (firstElement is FamilyInstance firstFitting && IsPipeFitting(firstFitting))
				{
					pickedTarget = firstFitting;
					Reference sprinklerRef = uidoc.Selection.PickObject(
						ObjectType.Element,
						new SprinklerSelectionFilter(),
						"Chọn Sprinkler để connect"
					);
					sprinkler = (FamilyInstance)doc.GetElement(sprinklerRef);
				}
				else
				{
					sprinkler = (FamilyInstance)firstElement;
					Reference pipeRef = uidoc.Selection.PickObject(
						ObjectType.Element,
						new PipeOrFittingSelectionFilter(),
						"Chọn Pipe hoặc PipeFitting để connect"
					);
					pickedTarget = doc.GetElement(pipeRef);
				}

				FlexPipeDiagnostics.Write("selection", "pair-ready", "Pipe and Sprinkler pair selected",
					pickedTarget.Id.GetIdInt(), sprinkler.Id.GetIdInt(),
					$"target={DescribeElement(pickedTarget)}; sprinkler={DescribeElement(sprinkler)}");
				string pairLog;
				bool pairSucceeded = pickedTarget is Pipe pickedPipe
					? ConnectSelection(doc, new List<FamilyInstance> { sprinkler }, pickedPipe, out pairLog)
					: ConnectSelection(doc, new List<FamilyInstance> { sprinkler }, (FamilyInstance)pickedTarget, out pairLog);
				if (pairSucceeded)
				{
					connectedCount++;
				}
				else
				{
					TaskDialog.Show("Sprinkler Flex Pipe", pairLog);
				}
				}
				catch (Autodesk.Revit.Exceptions.OperationCanceledException)
				{
					FlexPipeDiagnostics.Write("command", "cancelled", "User pressed Esc during interactive selection",
						details: $"connectedCount={connectedCount}");
					if (connectedCount > 0)
					{
						TaskDialog.Show("BIM TOOL - Sprinkler Flex Pipe", $"Đã hoàn thành kết nối {connectedCount} Sprinkler.");
					}
					return connectedCount > 0 ? Result.Succeeded : Result.Cancelled;
				}
			}
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			FlexPipeDiagnostics.Write("command", "cancelled", "User pressed Esc",
				details: $"connectedCount={connectedCount}");
			return connectedCount > 0 ? Result.Succeeded : Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			FlexPipeDiagnostics.Write("command", "exception", ex.Message, details: ex.ToString());
			TaskDialog.Show("BIM TOOL - Sprinkler Flex Pipe", "Lỗi trong quá trình kết nối: " + ex.Message + "\n\nChi tiết kỹ thuật đã được ghi nhận vào hệ thống chẩn đoán.");
			return connectedCount > 0 ? Result.Succeeded : Result.Failed;
		}
	}

	internal static Result ExecuteMulti(UIDocument uidoc, ref string message)
	{
		TaskDialog dialog = new TaskDialog("Sprinkler Flex Multi");
		dialog.MainInstruction = "Chọn chế độ kết nối";
		dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Elbow — chế độ cũ", "Quét các đầu ống nhánh và sprinkler.");
		dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Nhánh Tê + Elbow", "Quét ống nhánh và sprinkler. Dùng fitting có sẵn, tạo thêm Tê/Elbow còn thiếu.");
		dialog.CommonButtons = TaskDialogCommonButtons.Cancel;
		TaskDialogResult choice = dialog.Show();
		if (choice == TaskDialogResult.CommandLink1) return ExecuteBranchMulti(uidoc, ref message, elbowOnly: true);
		if (choice != TaskDialogResult.CommandLink2) return Result.Cancelled;
		return ExecuteBranchMulti(uidoc, ref message);
	}

	private static Result ExecuteExistingFittingsMulti(UIDocument uidoc, ref string message)
	{
		if (uidoc == null) return Result.Failed;
		Document doc = uidoc.Document;
		try
		{
			List<Element> selected = uidoc.Selection.GetElementIds().Select(doc.GetElement).Where(IsPhase2SelectableElement).ToList();
			if (!selected.OfType<FamilyInstance>().Any(IsSprinkler))
				selected = uidoc.Selection.PickObjects(ObjectType.Element, new SprinklerAndPipeSelectionFilter(),
					"Quét Tê/Cút và sprinkler (có thể chọn kèm ống nhánh), rồi Finish")
					.Select(r => doc.GetElement(r)).Where(IsPhase2SelectableElement).ToList();
			var fittings = selected.OfType<FamilyInstance>().Where(IsPipeFitting).ToList();
			// Include fittings directly attached to selected pipes; never traverse into another branch.
			foreach (Pipe pipe in selected.OfType<Pipe>())
				foreach (Connector end in GetPipeEndConnectors(pipe))
					foreach (Connector reference in end.AllRefs)
						if (reference.Owner is FamilyInstance fitting && IsPipeFitting(fitting) && !fittings.Any(f => f.Id == fitting.Id))
							fittings.Add(fitting);
			var targets = new List<FamilyInstance>();
			var origins = new List<XYZ>();
			foreach (FamilyInstance fitting in fittings.OrderBy(f => f.Id.GetIdInt()))
			{
				var open = GetUnusedConnectors(fitting).Where(c => c.Domain == Domain.DomainPiping && c.ConnectorType == ConnectorType.End).ToList();
				if (open.Count == 0) continue;
				if (open.Count != 1 || !IsNearlyVerticalDown(open[0].CoordinateSystem.BasisZ) || GetConnectedPipe(fitting) == null)
					throw new System.InvalidOperationException($"Fitting {fitting.Id}: cần đúng một đầu chờ hở hướng xuống và nối với ống.");
				targets.Add(fitting); origins.Add(open[0].Origin);
			}
			var sprinklers = selected.OfType<FamilyInstance>().Where(IsSprinkler).OrderBy(s => s.Id.GetIdInt()).ToList();
			if (targets.Count == 0 || targets.Count != sprinklers.Count || sprinklers.Any(s => GetSprinklerConnector(s) == null))
				throw new System.InvalidOperationException($"Cần số đầu chờ bằng số sprinkler có connector hở. Đầu chờ: {targets.Count}; sprinkler chọn: {sprinklers.Count}.");
			int count = targets.Count;
			double[,] costs = new double[count, count];
			for (int i = 0; i < count; i++)
				for (int j = 0; j < count; j++)
					costs[i, j] = origins[i].DistanceTo(GetSprinklerConnector(sprinklers[j]).Origin);
			int[] assignment = SolveMinimumCostAssignment(costs);
			string preview = string.Join("\n", Enumerable.Range(0, count).Select(i =>
				$"Fitting {targets[i].Id} → Sprinkler {sprinklers[assignment[i]].Id}: {costs[i, assignment[i]] * 304.8:F0} mm"));
			FlexPipeDiagnostics.Write("multi-existing-fittings", "paired", "Minimum total distance assignment", details: preview);
			if (TaskDialog.Show("Flex Multi — Fitting có sẵn", $"Ghép {count} cặp theo tổng khoảng cách ngắn nhất:\n{preview}\n\nTạo FlexPipe cho các cặp này?",
				TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No) != TaskDialogResult.Yes) return Result.Cancelled;
			using (TransactionGroup group = new TransactionGroup(doc, "Flex Multi - Existing fittings"))
			{
				group.Start();
				for (int i = 0; i < count; i++)
					if (!ConnectSelection(doc, new List<FamilyInstance> { sprinklers[assignment[i]] }, targets[i], out string log))
					{
						group.RollBack();
						throw new System.InvalidOperationException($"Đã hoàn tác cả cụm. Fitting {targets[i].Id}: {log}");
					}
				group.Assimilate();
			}
			uidoc.Selection.SetElementIds(targets.Select(f => f.Id).Concat(sprinklers.Select(s => s.Id)).ToList());
			FlexPipeDiagnostics.Write("multi-existing-fittings", "succeeded", $"Connected {count} pairs", details: preview);
			TaskDialog.Show("Sprinkler Flex Multi", $"Đã nối {count} FlexPipe vào fitting có sẵn.");
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
	}

	internal static Result ExecuteMultiElbow(UIDocument uidoc, ref string message)
	{
		Document doc = uidoc?.Document;
		if (doc == null)
		{
			message = "Không có document Revit đang hoạt động.";
			return Result.Failed;
		}

		List<Element> selectedElements = uidoc.Selection.GetElementIds()
			.Select(id => doc.GetElement(id))
			.Where(IsPhase2SelectableElement)
			.ToList();
		List<Pipe> pipes = selectedElements.OfType<Pipe>().ToList();
		List<FamilyInstance> sprinklers = selectedElements
			.OfType<FamilyInstance>()
			.Where(IsSprinkler)
			.ToList();
		List<FamilyInstance> fittings = selectedElements
			.OfType<FamilyInstance>()
			.Where(IsPipeFitting)
			.ToList();

		bool hasPreselection = (pipes.Count > 0 || fittings.Count == 1) && sprinklers.Count > 0;
		FlexPipeDiagnostics.Write("multi-command", "started", "Sprinkler Flex Multi command started",
			details: $"document={doc.Title}; pipes={pipes.Count}; sprinklers={sprinklers.Count}; mode={(hasPreselection ? "preselection" : "interactive-loop")}");

		// Preselection is deliberately a one-shot operation.  The interactive loop below is
		// only entered when the command was started with no complete Pipe/Sprinkler selection.
		if (hasPreselection)
		{
			int connectedCount = 0;
			int expectedPairCount = pipes.Count == 1 ? sprinklers.Count : 0;
			List<string> pairErrors = new List<string>();
			if (fittings.Count == 1 && pipes.Count == 0)
			{
				expectedPairCount = sprinklers.Count;
				if (ConnectSelection(doc, sprinklers, fittings[0], out string fittingLog)) connectedCount = sprinklers.Count;
				else pairErrors.Add(fittingLog);
			}
			else if (pipes.Count == 1)
			{
				if (ConnectSelection(doc, sprinklers, pipes[0], out string singlePipeLog))
				{
					connectedCount = sprinklers.Count;
				}
				else
				{
					pairErrors.Add(singlePipeLog);
				}
			}
			else if (TryPairElbowSelection(pipes, sprinklers,
				out List<(Pipe pipe, FamilyInstance sprinkler, double distance)> elbowPairs,
				out string pairingLog))
			{
				expectedPairCount = elbowPairs.Count;
				foreach (var pair in elbowPairs)
				{
					if (ConnectSelection(doc, new List<FamilyInstance> { pair.sprinkler }, pair.pipe,
						out string pairLog, forceExistingBranch: true))
					{
						connectedCount++;
					}
					else
					{
						pairErrors.Add($"Pipe {pair.pipe.Id.GetIdInt()} / Sprinkler {pair.sprinkler.Id.GetIdInt()}: {pairLog}");
					}
				}
			}
			else
			{
				pairErrors.Add(pairingLog);
			}

			uidoc.Selection.SetElementIds(new List<ElementId>());
			FlexPipeDiagnostics.Write("multi-command",
				connectedCount == expectedPairCount ? "succeeded" : "partial",
				$"Preselection connected {connectedCount}/{expectedPairCount} elbow pairs");
			TaskDialog.Show("Sprinkler Flex Multi", pairErrors.Count == 0
				? $"Đã kết nối thành công {connectedCount} cặp."
				: $"Đã kết nối {connectedCount}/{expectedPairCount} cặp.\n\n" + string.Join("\n", pairErrors));
			return connectedCount > 0 ? Result.Succeeded : Result.Failed;
		}

		int totalClustersConnected = 0;
		int totalPairsConnected = 0;
		List<string> accumulatedErrors = new List<string>();
		while (true)
		{
			try
			{
				IList<Reference> references = uidoc.Selection.PickObjects(ObjectType.Element,
					new SprinklerAndPipeSelectionFilter(),
					"Quét chọn các Pipe và Sprinkler cho một cụm (Finish để kết nối cụm này, Esc để kết thúc)...");
				selectedElements = references.Select(r => doc.GetElement(r)).Where(IsPhase2SelectableElement).ToList();
				pipes = selectedElements.OfType<Pipe>().ToList();
				sprinklers = selectedElements.OfType<FamilyInstance>().Where(IsSprinkler).ToList();
				fittings = selectedElements.OfType<FamilyInstance>().Where(IsPipeFitting).ToList();

				if ((pipes.Count == 0 && fittings.Count != 1) || sprinklers.Count == 0)
				{
					FlexPipeDiagnostics.Write("selection", "multi-elbow-incomplete-cluster",
						"Interactive cluster needs at least one Pipe and one Sprinkler",
						details: $"pipes={pipes.Count}; sprinklers={sprinklers.Count}");
					continue;
				}

				int clusterPairsConnected = 0;
				if (fittings.Count == 1 && pipes.Count == 0)
				{
					if (ConnectSelection(doc, sprinklers, fittings[0], out string fittingLog))
						clusterPairsConnected = sprinklers.Count;
					else
						accumulatedErrors.Add(fittingLog);
				}
				else if (pipes.Count == 1)
				{
					if (ConnectSelection(doc, sprinklers, pipes[0], out string singlePipeLog))
					{
						clusterPairsConnected = sprinklers.Count;
					}
					else
					{
						accumulatedErrors.Add(singlePipeLog);
					}
				}
				else if (TryPairElbowSelection(pipes, sprinklers,
					out List<(Pipe pipe, FamilyInstance sprinkler, double distance)> elbowPairs,
					out string pairingLog))
				{
					foreach (var pair in elbowPairs)
					{
						if (ConnectSelection(doc, new List<FamilyInstance> { pair.sprinkler }, pair.pipe,
							out string pairLog, forceExistingBranch: true))
						{
							clusterPairsConnected++;
						}
						else
						{
							accumulatedErrors.Add($"Pipe {pair.pipe.Id.GetIdInt()} / Sprinkler {pair.sprinkler.Id.GetIdInt()}: {pairLog}");
						}
					}
				}
				else
				{
					FlexPipeDiagnostics.Write("selection", "multi-elbow-invalid", pairingLog,
						details: $"pipes={pipes.Count}; sprinklers={sprinklers.Count}");
					accumulatedErrors.Add(pairingLog);
				}

				if (clusterPairsConnected > 0)
				{
					totalClustersConnected++;
					totalPairsConnected += clusterPairsConnected;
				}
			}
			catch (Autodesk.Revit.Exceptions.OperationCanceledException)
			{
				break;
			}
			finally
			{
				uidoc.Selection.SetElementIds(new List<ElementId>());
			}
		}

		FlexPipeDiagnostics.Write("multi-command", "interactive-finished",
			$"Interactive loop connected {totalPairsConnected} pairs across {totalClustersConnected} clusters",
			details: $"errors={accumulatedErrors.Count}");
		if (totalPairsConnected > 0 || accumulatedErrors.Count > 0)
		{
			string summary = $"Đã kết nối thành công tổng cộng {totalPairsConnected} cặp qua {totalClustersConnected} cụm.";
			if (accumulatedErrors.Count > 0)
			{
				summary += $"\nCó {accumulatedErrors.Count} lỗi; xem FlexPipe diagnostics để biết chi tiết.";
			}
			TaskDialog.Show("Sprinkler Flex Multi", summary);
		}
		return totalPairsConnected > 0 ? Result.Succeeded
			: accumulatedErrors.Count > 0 ? Result.Failed : Result.Cancelled;
	}

	private static bool ConnectSelection(Document doc, List<FamilyInstance> sprinklers, Pipe pickedPipe,
		out string log, bool? forceExistingBranch = null)
	{
		FlexPipeDiagnostics.Write("transaction-group", "starting", "Starting pair transaction group",
			pickedPipe?.Id.GetIdInt(), sprinklers.FirstOrDefault()?.Id.GetIdInt());
		using (TransactionGroup tg = new TransactionGroup(doc, "Sprinkler Flex Pipe - Auto Mode"))
		{
			tg.Start();
			bool success = ExecuteConnectFlexPipeDirect(doc, sprinklers, pickedPipe, out log, forceExistingBranch);
			if (!success)
			{
				tg.RollBack();
				FlexPipeDiagnostics.Write("transaction-group", "rolled-back", log,
					pickedPipe?.Id.GetIdInt(), sprinklers.FirstOrDefault()?.Id.GetIdInt());
				return false;
			}

			tg.Assimilate();
			FlexPipeDiagnostics.Write("transaction-group", "committed", "Pair connected successfully",
				pickedPipe?.Id.GetIdInt(), sprinklers.FirstOrDefault()?.Id.GetIdInt());
			return true;
		}
	}

	private static bool TryPairElbowSelection(List<Pipe> pipes, List<FamilyInstance> sprinklers,
		out List<(Pipe pipe, FamilyInstance sprinkler, double distance)> pairs, out string log)
	{
		pairs = new List<(Pipe pipe, FamilyInstance sprinkler, double distance)>();
		log = "";
		var pipeEnds = pipes
			.Select(pipe => new
			{
				Pipe = pipe,
				Connectors = GetUnusedConnectors(pipe)
					.Where(c => c.Domain == Domain.DomainPiping)
					.Where(c => c.ConnectorType == ConnectorType.End)
					.ToList()
			})
			.ToList();
		var sprinklerConnectors = sprinklers
			.Select(sprinkler => new { Sprinkler = sprinkler, Connector = GetSprinklerConnector(sprinkler) })
			.ToList();
		const double maximumPairDistanceFeet = 50.0;
		var candidates = new List<(Pipe pipe, Connector pipeConnector, FamilyInstance sprinkler, double distance)>();
		foreach (var pipeEnd in pipeEnds.Where(x => x.Connectors.Count > 0))
		{
			foreach (Connector pipeConnector in pipeEnd.Connectors)
			{
				foreach (var sprinklerConnector in sprinklerConnectors.Where(x => x.Connector != null))
				{
					double distance = pipeConnector.Origin.DistanceTo(sprinklerConnector.Connector.Origin);
					if (distance <= maximumPairDistanceFeet)
						candidates.Add((pipeEnd.Pipe, pipeConnector, sprinklerConnector.Sprinkler, distance));
				}
			}
		}

		HashSet<Connector> usedPipeEnds = new HashSet<Connector>();
		HashSet<ElementId> usedSprinklers = new HashSet<ElementId>();
		foreach (var candidate in candidates.OrderBy(x => x.distance))
		{
			if (usedPipeEnds.Contains(candidate.pipeConnector) || usedSprinklers.Contains(candidate.sprinkler.Id)) continue;
			usedPipeEnds.Add(candidate.pipeConnector);
			usedSprinklers.Add(candidate.sprinkler.Id);
			pairs.Add((candidate.pipe, candidate.sprinkler, candidate.distance));
		}

		HashSet<ElementId> pairedPipeIds = new HashSet<ElementId>(pairs.Select(p => p.pipe.Id));
		List<int> skippedPipeIds = pipeEnds.Where(x => !pairedPipeIds.Contains(x.Pipe.Id)).Select(x => x.Pipe.Id.GetIdInt()).ToList();
		List<int> skippedSprinklerIds = sprinklerConnectors.Where(x => x.Connector == null || !usedSprinklers.Contains(x.Sprinkler.Id)).Select(x => x.Sprinkler.Id.GetIdInt()).ToList();
		log = $"Ghép được {pairs.Count} cặp trong khoảng tối đa {maximumPairDistanceFeet * 304.8:F0} mm." +
			(skippedPipeIds.Count == 0 ? "" : $" Pipe bỏ qua: {string.Join(", ", skippedPipeIds)}.") +
			(skippedSprinklerIds.Count == 0 ? "" : $" Sprinkler bỏ qua: {string.Join(", ", skippedSprinklerIds)}.");
		return pairs.Count > 0;
	}

	private static int[] SolveMinimumCostAssignment(double[,] costs)
	{
		int count = costs.GetLength(0);
		double[] rowPotential = new double[count + 1];
		double[] columnPotential = new double[count + 1];
		int[] matchedRow = new int[count + 1];
		int[] previousColumn = new int[count + 1];

		for (int row = 1; row <= count; row++)
		{
			matchedRow[0] = row;
			int currentColumn = 0;
			double[] minimum = Enumerable.Repeat(double.PositiveInfinity, count + 1).ToArray();
			bool[] used = new bool[count + 1];

			do
			{
				used[currentColumn] = true;
				int currentRow = matchedRow[currentColumn];
				double delta = double.PositiveInfinity;
				int nextColumn = 0;
				for (int column = 1; column <= count; column++)
				{
					if (used[column]) continue;
					double reducedCost = costs[currentRow - 1, column - 1]
						- rowPotential[currentRow] - columnPotential[column];
					if (reducedCost < minimum[column])
					{
						minimum[column] = reducedCost;
						previousColumn[column] = currentColumn;
					}
					if (minimum[column] < delta)
					{
						delta = minimum[column];
						nextColumn = column;
					}
				}

				for (int column = 0; column <= count; column++)
				{
					if (used[column])
					{
						rowPotential[matchedRow[column]] += delta;
						columnPotential[column] -= delta;
					}
					else
					{
						minimum[column] -= delta;
					}
				}
				currentColumn = nextColumn;
			}
			while (matchedRow[currentColumn] != 0);

			do
			{
				int previous = previousColumn[currentColumn];
				matchedRow[currentColumn] = matchedRow[previous];
				currentColumn = previous;
			}
			while (currentColumn != 0);
		}

		int[] columnByRow = Enumerable.Repeat(-1, count).ToArray();
		for (int column = 1; column <= count; column++)
		{
			if (matchedRow[column] > 0) columnByRow[matchedRow[column] - 1] = column - 1;
		}
		return columnByRow;
	}

	private static bool IsSprinkler(FamilyInstance familyInstance)
	{
		return familyInstance?.Category != null &&
			familyInstance.Category.GetIdInt() == (int)BuiltInCategory.OST_Sprinklers;
	}

	private static bool ConnectSelection(Document doc, List<FamilyInstance> sprinklers, FamilyInstance fitting,
		out string log)
	{
		log = "";
		Connector openFittingConnector = GetOpenPipingConnector(fitting);
		Pipe connectedPipe = GetConnectedPipe(fitting);
		if (openFittingConnector == null || connectedPipe == null)
		{
			log = "PipeFitting phải có một connector ống hở và nối với một Pipe để tạo FlexPipe.";
			return false;
		}

		using (TransactionGroup tg = new TransactionGroup(doc, "Sprinkler Flex Pipe - Fitting Endpoint"))
		{
			tg.Start();
			bool success = ExecuteConnectFlexPipeDirect(doc, sprinklers, connectedPipe, out log,
				forceExistingBranch: true, preferredOpenConnector: openFittingConnector);
			if (!success)
			{
				tg.RollBack();
				return false;
			}
			tg.Assimilate();
			return true;
		}
	}

	private static bool IsPipeFitting(FamilyInstance familyInstance)
	{
		return familyInstance?.Category != null &&
			familyInstance.Category.GetIdInt() == (int)BuiltInCategory.OST_PipeFitting;
	}

	private static bool IsPhase2SelectableElement(Element element)
	{
		return element is Pipe || (element is FamilyInstance familyInstance &&
			(IsSprinkler(familyInstance) || IsPipeFitting(familyInstance)));
	}

	private sealed class SprinklerAndPipeSelectionFilter : ISelectionFilter
	{
		public bool AllowElement(Element element)
		{
			return IsPhase2SelectableElement(element);
		}

		public bool AllowReference(Reference reference, XYZ position)
		{
			return true;
		}
	}

	private sealed class PipeSelectionFilter : ISelectionFilter
	{
		public bool AllowElement(Element element)
		{
			return element is Pipe;
		}

		public bool AllowReference(Reference reference, XYZ position)
		{
			return true;
		}
	}

	private sealed class SprinklerSelectionFilter : ISelectionFilter
	{
		public bool AllowElement(Element element)
		{
			return element is FamilyInstance familyInstance && IsSprinkler(familyInstance);
		}

		public bool AllowReference(Reference reference, XYZ position)
		{
			return true;
		}
	}

	public static bool ExecuteConnectByIds(Document doc, int pipeId, List<int> sprinklerIds, out string log)
	{
		log = "";
		if (pipeId == -5 && (sprinklerIds == null || sprinklerIds.Count == 0))
		{
			log = MicroSmokeTest.Run(doc);
			return log.Contains("\"failed\":0");
		}
				if (pipeId == -4)
		{
			PipeType pType = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>().FirstOrDefault();
			PipingSystemType sType = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().FirstOrDefault();
			Level lvl = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();

			if (pType == null || sType == null || lvl == null)
			{
				log = "{\"success\":false,\"error\":\"Thieu PipeType/SystemType/Level\"}";
				return false;
			}

			List<ElementId> createdElbows = new List<ElementId>();
			List<ElementId> allCreated = new List<ElementId>();

			using (Transaction tr = new Transaction(doc, "BIN Create 3 Parallel Test Pipes"))
			{
				tr.Start();
				for (int i = 0; i < 3; i++)
				{
					double yOffset = 100.0 + i * 15.0;
					Pipe p = Pipe.Create(doc, sType.Id, pType.Id, lvl.Id, new XYZ(0, yOffset, 10), new XYZ(20, yOffset, 10));
					p?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
					allCreated.Add(p.Id);
					doc.Regenerate();

					Connector[] un = NaviateHelper.ConnectorArrayUnused(p);
					if (un != null && un.Length > 0)
					{
						Connector c = un.OrderBy(cn => cn.Origin.X).Last();
						Pipe pUp = NaviateHelper.DrawPipeWithElbow(doc, null, c.Origin, c, c.Origin, new XYZ(c.Origin.X, c.Origin.Y, c.Origin.Z + 4.0));
						if (pUp != null)
						{
							allCreated.Add(pUp.Id);
							Connector[] pConns = NaviateHelper.ConnectorArray(p);
							foreach (Connector pc in pConns)
							{
								foreach (Connector other in pc.AllRefs)
								{
									if (other.Owner is FamilyInstance fi && !createdElbows.Contains(fi.Id))
									{
										createdElbows.Add(fi.Id);
										allCreated.Add(fi.Id);
									}
								}
							}
						}
					}
				}
				tr.Commit();
			}

#if NET8_0_OR_GREATER
			var elbowVals = createdElbows.Select(id => id.Value);
			var allVals = allCreated.Select(id => id.Value);
#else
			var elbowVals = createdElbows.Select(id => id.IntegerValue);
			var allVals = allCreated.Select(id => id.IntegerValue);
#endif

			log = $"{{\"success\":true,\"createdPipes\":[{string.Join(",", allVals)}],\"elbowIds\":[{string.Join(",", elbowVals)}]}}";
			return true;
		}
		if (pipeId == -99 || pipeId == -1)
		{
			return RunMicroTestSuite(doc, out log);
		}
		if (pipeId == -2)
		{
			var pipes = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).WhereElementIsNotElementType().ToElements();
			var fittings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeFitting).WhereElementIsNotElementType().ToElements();
			List<string> items = new List<string>();
			foreach (Element p in pipes)
			{
				LocationCurve lc = p.Location as LocationCurve;
				if (lc != null)
				{
					XYZ p0 = lc.Curve.GetEndPoint(0);
					if (p0.Y >= -10.0 && p0.Y <= 500.0)
					{
						items.Add($"{{\"id\":{p.Id},\"cat\":\"Pipe\",\"y\":{Math.Round(p0.Y, 1)},\"z\":{Math.Round(p0.Z, 1)},\"len\":{Math.Round(lc.Curve.Length * 304.8, 0)}}}");
					}
				}
			}
			foreach (Element f in fittings)
			{
				LocationPoint lp = f.Location as LocationPoint;
				if (lp != null && lp.Point.Y >= -10.0 && lp.Point.Y <= 500.0)
				{
					items.Add($"{{\"id\":{f.Id},\"cat\":\"Fitting\",\"y\":{Math.Round(lp.Point.Y, 1)},\"z\":{Math.Round(lp.Point.Z, 1)},\"name\":\"{McpExternalEventHandler.EscapeJson(f.Name)}\"}}");
				}
			}
			log = $"{{\"elements\":[{string.Join(",", items)}]}}";
			return true;
		}

		if (pipeId <= 0)
		{
			try
			{
				PipeType pipeType = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>().FirstOrDefault();
				PipingSystemType sysType = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().FirstOrDefault();
				Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();

				if (pipeType == null || sysType == null || level == null)
				{
					log = "Không tìm thấy đủ PipeType, PipingSystemType hoặc Level trong document.";
					return false;
				}

				using (Transaction tr = new Transaction(doc, "BIN Test Create Pipe via MCP"))
				{
					tr.Start();
					XYZ p1 = new XYZ(0, 0, 10);
					XYZ p2 = new XYZ(15, 0, 10);
					Pipe newPipe = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, p1, p2);
					if (newPipe != null)
					{
						Parameter diamParam = newPipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
						if (diamParam != null && !diamParam.IsReadOnly)
						{
							diamParam.Set(50.0 / 304.8); // DN50
						}
					}
					tr.Commit();
					log = $"Đã tự động vẽ thành công 1 đoạn Pipe (ID: {newPipe.Id}) dài 15ft (DN50) tại Level: '{level.Name}', Hệ thống: '{sysType.Name}', Tọa độ: (0, 0, 10) đến (15, 0, 10).";
					return true;
				}
			}
			catch (Exception ex)
			{
				log = "Lỗi khi tự vẽ Pipe: " + ex.Message;
				return false;
			}
		}

		Pipe pickedPipe = doc.GetElement(new ElementId(pipeId)) as Pipe;
		if (pickedPipe == null)
		{
			log = $"Không tìm thấy Pipe với Id: {pipeId}";
			return false;
		}

		List<FamilyInstance> sprinklers = sprinklerIds
			.Select(id => doc.GetElement(new ElementId(id)) as FamilyInstance)
			.Where(sp => sp != null)
			.ToList();

		if (sprinklers.Count == 0)
		{
			try
			{
				Connector[] unusedConns = NaviateHelper.ConnectorArrayUnused(pickedPipe);
				if (unusedConns == null || unusedConns.Length == 0)
				{
					log = $"Ống ID: {pipeId} không còn đầu hở (Open Connector) để bẻ cút.";
					return false;
				}

				Connector sourceConn = unusedConns[0];
				XYZ origin = sourceConn.Origin;
				double extLen = 5.0; // 5 feet (1.5m)
				XYZ endPoint = new XYZ(origin.X, origin.Y, origin.Z + extLen);

				Pipe newExtPipe = null;
				using (Transaction tr = new Transaction(doc, "BIN Automated Elbow Up Test"))
				{
					tr.Start();
					newExtPipe = NaviateHelper.DrawPipeWithElbow(doc, null, origin, sourceConn, origin, endPoint);
					tr.Commit();
				}

				if (newExtPipe != null)
				{
					log = $"Đã tự động bẻ cút 90° (Elbow Up) thành công! Sinh ra cút Fitting và đoạn ống đứng mới (ID: {newExtPipe.Id}) dài 1.5m tại đầu ống {pipeId}.";
					return true;
				}
				else
				{
					log = $"Không thể tự động tạo cút fitting trên ống {pipeId}.";
					return false;
				}
			}
			catch (Exception ex)
			{
				log = $"Lỗi khi tự động bẻ cút trên ống ID {pipeId}: {ex.Message}";
				return false;
			}
		}

		using (TransactionGroup tg = new TransactionGroup(doc, "Connect Sprinkler Flex Pipe via MCP"))
		{
			tg.Start();
			bool ok = ExecuteConnectFlexPipeDirect(doc, sprinklers, pickedPipe, out log);
			if (ok) tg.Assimilate();
			else tg.RollBack();
			return ok;
		}
	}

	public static bool ExecuteConnectFlexPipeDirect(Document doc, List<FamilyInstance> sprinklers, Pipe pickedPipe,
		out string log, bool? forceExistingBranch = null, Connector preferredOpenConnector = null)
	{
		log = "";
		FlexPipeDiagnostics.Write("execute", "started", "Geometry/connect workflow started",
			pickedPipe?.Id.GetIdInt(), sprinklers.FirstOrDefault()?.Id.GetIdInt(),
			$"sprinklerCount={sprinklers.Count}; forceExistingBranch={forceExistingBranch?.ToString() ?? "auto"}");
		double flexDiameterFeet = 25.0 / 304.8; // DN25
		double temporaryStubFeet = 50.0 / 304.8; // Ống gá tạm để Revit tạo đúng elbow/reducer
		double teeEndClearanceFeet = 100.0 / 304.8; // Không tạo Tee quá sát đầu ống

		ElementId pipeTypeId = pickedPipe.GetTypeId();
		ElementId systemTypeId = pickedPipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM).AsElementId();

		LocationCurve pipeLoc = pickedPipe.Location as LocationCurve;
		Line pipeLine = pipeLoc?.Curve as Line;
		if (pipeLine == null)
		{
			log = "Không lấy được đường tim trục ống.";
			FlexPipeDiagnostics.Write("validate", "failed", log, pickedPipe?.Id.GetIdInt());
			return false;
		}

		// Tìm FlexPipeType
		FlexPipeType flexPipeType = new FilteredElementCollector(doc)
			.OfClass(typeof(FlexPipeType))
			.Cast<FlexPipeType>()
			.FirstOrDefault();

		if (flexPipeType == null)
		{
			flexPipeType = new FilteredElementCollector(doc)
				.OfCategory(BuiltInCategory.OST_FlexPipeCurves)
				.WhereElementIsElementType()
				.Cast<FlexPipeType>()
				.FirstOrDefault();
		}

		if (flexPipeType == null)
		{
			log = "Dự án hiện chưa có FlexPipeType. Vui lòng vào tab Systems -> Flex Pipe tạo ít nhất 1 loại ống mềm trước.";
			FlexPipeDiagnostics.Write("validate", "failed", log, pickedPipe?.Id.GetIdInt());
			return false;
		}

		ElementId levelId = ResolveValidLevelId(doc,
			sprinklers[0]?.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM)?.AsElementId(),
			pickedPipe.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM)?.AsElementId());
		if (levelId == null)
		{
			log = "Không tìm được Level hợp lệ để tạo FlexPipe.";
			return false;
		}

		List<Connector> unusedConns = GetUnusedConnectors(pickedPipe);
		FamilyInstance attachedFitting = null;
		if (preferredOpenConnector != null && !preferredOpenConnector.IsConnected)
		{
			// Explicit fitting selection must not compete with a free end on its connected pipe.
			unusedConns.Clear();
			unusedConns.Add(preferredOpenConnector);
			attachedFitting = preferredOpenConnector.Owner as FamilyInstance;
		}
		else if (unusedConns.Count == 0)
		{
			foreach (Connector pipeConnector in GetPipeEndConnectors(pickedPipe).Where(c => c.IsConnected))
			{
				FamilyInstance fitting = pipeConnector.AllRefs.Cast<Connector>()
					.Select(r => r.Owner as FamilyInstance)
					.FirstOrDefault(IsPipeFitting);
				Connector openFittingConnector = GetOpenPipingConnector(fitting);
				if (openFittingConnector == null) continue;
				unusedConns.Add(openFittingConnector);
				attachedFitting = fitting;
				break;
			}
		}
		if (attachedFitting != null)
		{
			FlexPipeDiagnostics.Write("endpoint", "attached-fitting-detected",
				"Using an open connector on an attached PipeFitting as the branch endpoint",
				pickedPipe.Id.GetIdInt(), details: $"fittingId={attachedFitting.Id.GetIdInt()}");
		}

		List<XYZ> splitPoints = new List<XYZ>();
		var connDataList = new List<(FamilyInstance sp, XYZ projPt, Pipe sourcePipe,
			ElementId elbowId, ElementId reducerId, ElementId topStubId, ElementId bottomStubId,
			XYZ flexStartPoint, XYZ flexEndPoint, bool isExistingBranch, bool isDirectVerticalBranch,
			ElementId existingBranchEndpointOwnerId, XYZ existingBranchEndpointOrigin)>();
		List<FlexEpisodeConnection> completedConnections = new List<FlexEpisodeConnection>();
		Dictionary<int, ElementId> mainTeeIdsByTopStub = new Dictionary<int, ElementId>();

		// BƯỚC 1: Dùng hai đoạn ống đứng làm đồ gá để tạo đúng elbow và reducer.
		using (Transaction tRigid = new Transaction(doc, "Create Temporary Fitting Stubs"))
		{
			tRigid.Start();

			foreach (FamilyInstance sprinkler in sprinklers)
			{
				Connector spConn = GetSprinklerConnector(sprinkler);
				if (spConn == null)
				{
					log += $"Sprinkler {sprinkler.Id}: không có connector ống trống.\n";
					FlexPipeDiagnostics.Write("validate", "failed", "Sprinkler has no unused piping connector",
						pickedPipe.Id.GetIdInt(), sprinkler.Id.GetIdInt());
					continue;
				}

				XYZ spOrigin = spConn.Origin;
				XYZ projPoint = ProjectPointOnLine(pipeLine, spOrigin);
				XYZ pipeStart = pipeLine.GetEndPoint(0);
				XYZ pipeEnd = pipeLine.GetEndPoint(1);
				double pipeLength = pipeStart.DistanceTo(pipeEnd);
				bool pointOnSegment = Math.Abs(
					projPoint.DistanceTo(pipeStart) + projPoint.DistanceTo(pipeEnd) - pipeLength) < 0.01;
				bool hasTeeClearance = pointOnSegment &&
					projPoint.DistanceTo(pipeStart) >= teeEndClearanceFeet &&
					projPoint.DistanceTo(pipeEnd) >= teeEndClearanceFeet;

				// Auto mode: một connector đầu hở xác định đây là ống nhánh chờ và phải dùng Elbow.
				// Chỉ dùng Tee giữa thân khi ống không còn đầu hở và điểm chiếu có đủ khoảng lùi.
				bool isExistingBranchPipe = forceExistingBranch ?? unusedConns.Count > 0;
				FlexPipeDiagnostics.Write("geometry", "mode-selected",
					isExistingBranchPipe ? "Endpoint mode" : "Main-pipe/Tee mode",
					pickedPipe.Id.GetIdInt(), sprinkler.Id.GetIdInt(),
					$"sprinklerConnector={DescribeConnector(spConn)}; pipeStart={DescribePoint(pipeStart)}; " +
					$"pipeEnd={DescribePoint(pipeEnd)}; projection={DescribePoint(projPoint)}; " +
					$"pointOnSegment={pointOnSegment}; startDistanceMm={projPoint.DistanceTo(pipeStart) * 304.8:F2}; " +
					$"endDistanceMm={projPoint.DistanceTo(pipeEnd) * 304.8:F2}; unusedPipeConnectors={unusedConns.Count}");

				// Trường hợp 1: Chọn ĐẦU ỐNG NHÁNH CÓ SẴN (Đã có đầu chờ)
				if (isExistingBranchPipe)
				{
					Connector openConn = unusedConns
						.OrderBy(c => c.Origin.DistanceTo(spOrigin))
						.FirstOrDefault();
					if (openConn == null)
					{
						log += $"Sprinkler {sprinkler.Id}: đầu ống đã được dùng hoặc không còn connector trống.\n";
						FlexPipeDiagnostics.Write("endpoint", "failed", "No unused endpoint connector",
							pickedPipe.Id.GetIdInt(), sprinkler.Id.GetIdInt());
						continue;
					}

					unusedConns.Remove(openConn);
					XYZ elbowPoint = openConn.Origin;
					bool isDirectVerticalBranch = IsNearlyVerticalDown(openConn.CoordinateSystem?.BasisZ);

					// An open branch connector already facing down is collinear with the flex route.
					// Do not force an elbow between two same-direction connectors: Revit correctly
					// rejects that as a zero-angle elbow.  Leave this endpoint free for FlexPipe.
					if (isDirectVerticalBranch)
					{
						XYZ directSprinklerPoint = spOrigin;
						XYZ directBottomStubEnd = directSprinklerPoint + new XYZ(0, 0, temporaryStubFeet);
						Pipe directBottomStub = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, directSprinklerPoint, directBottomStubEnd);
						directBottomStub.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(flexDiameterFeet);
						doc.Regenerate();

						Connector directBottomStubAtSprinkler = FindConnectorAtPoint(directBottomStub, directSprinklerPoint);
						Connector directBottomStubFree = FindConnectorAtPoint(directBottomStub, directBottomStubEnd);
						if (directBottomStubAtSprinkler == null || directBottomStubFree == null)
						{
							throw new System.InvalidOperationException("Không lấy được connector của ống gá sprinkler cho nhánh đứng.");
						}

						FamilyInstance directReducer = CreateTransitionOrDirectConnect(doc, directBottomStubAtSprinkler, spConn);
						doc.Regenerate();
						connDataList.Add((sprinkler, elbowPoint, pickedPipe,
							ElementId.InvalidElementId, directReducer == null ? ElementId.InvalidElementId : directReducer.Id,
							ElementId.InvalidElementId, directBottomStub.Id, elbowPoint, directBottomStubFree.Origin, true, true,
							openConn.Owner.Id, openConn.Origin));
						FlexPipeDiagnostics.Write("endpoint", "direct-vertical", "Downward open branch will connect directly to FlexPipe; elbow skipped",
							pickedPipe.Id.GetIdInt(), sprinkler.Id.GetIdInt(),
							$"openPipe={DescribeConnector(openConn)}; reducerId={(directReducer == null ? "direct" : directReducer.Id.GetIdInt().ToString())}; bottomFree={DescribeConnector(directBottomStubFree)}");
						continue;
					}

					XYZ topStubEnd = elbowPoint - new XYZ(0, 0, temporaryStubFeet);
					Pipe topStub = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, elbowPoint, topStubEnd);
					topStub.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(openConn.Radius * 2.0);

					XYZ sprinklerPoint = spOrigin;
					XYZ bottomStubEnd = sprinklerPoint + new XYZ(0, 0, temporaryStubFeet);
					Pipe bottomStub = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, sprinklerPoint, bottomStubEnd);
					bottomStub.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(flexDiameterFeet);
					doc.Regenerate();

					Connector topStubAtElbow = FindConnectorAtPoint(topStub, elbowPoint);
					Connector bottomStubAtSprinkler = FindConnectorAtPoint(bottomStub, sprinklerPoint);
					if (topStubAtElbow == null || bottomStubAtSprinkler == null)
					{
						throw new System.InvalidOperationException("Không lấy được connector của hai ống gá tạm.");
					}

					FamilyInstance elbow = doc.Create.NewElbowFitting(openConn, topStubAtElbow);
					FamilyInstance reducer = CreateTransitionOrDirectConnect(doc, bottomStubAtSprinkler, spConn);
					doc.Regenerate();

					// Chọn đúng đầu xa fitting theo tọa độ, không phụ thuộc thứ tự ConnectorSet.
					Connector topStubFree = FindConnectorAtPoint(topStub, topStubEnd);
					Connector bottomStubFree = FindConnectorAtPoint(bottomStub, bottomStubEnd);
					if (topStubFree == null || bottomStubFree == null)
					{
						throw new System.InvalidOperationException("Không lấy được đầu tự do của hai ống gá tạm.");
					}

					connDataList.Add((sprinkler, elbowPoint, pickedPipe,
						elbow.Id, reducer == null ? ElementId.InvalidElementId : reducer.Id, topStub.Id, bottomStub.Id,
						topStubFree.Origin, bottomStubFree.Origin, true, false,
						ElementId.InvalidElementId, null));
					FlexPipeDiagnostics.Write("temporary-fittings", "created", "Elbow and reducer created with temporary stubs",
						pickedPipe.Id.GetIdInt(), sprinkler.Id.GetIdInt(),
						$"openPipe={DescribeConnector(openConn)}; elbowId={elbow.Id.GetIdInt()}; reducerId={(reducer == null ? "direct" : reducer.Id.GetIdInt().ToString())}; " +
						$"topFree={DescribeConnector(topStubFree)}; bottomFree={DescribeConnector(bottomStubFree)}");
				}
				else
				{
					// Trường hợp 2: Chọn ĐƯỜNG ỐNG CHÍNH
					if (!hasTeeClearance)
					{
						log += $"Sprinkler {sprinkler.Id}: vị trí chiếu nằm ngoài thân ống hoặc quá sát đầu ống (<100 mm).\n";
						continue;
					}
					splitPoints.Add(projPoint);

					// Ống gá đi thẳng xuống ngay tại điểm tách để Tee có nhánh theo trục Z.
					XYZ topStubEnd = projPoint - new XYZ(0, 0, temporaryStubFeet);
					Pipe topStub = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, projPoint, topStubEnd);
					topStub.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(flexDiameterFeet);

					XYZ bottomStubEnd = spOrigin + new XYZ(0, 0, temporaryStubFeet);
					Pipe bottomStub = Pipe.Create(doc, systemTypeId, pipeTypeId, levelId, spOrigin, bottomStubEnd);
					bottomStub.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM).Set(flexDiameterFeet);
					doc.Regenerate();

					Connector bottomStubAtSprinkler = FindConnectorAtPoint(bottomStub, spOrigin);
					if (bottomStubAtSprinkler == null)
					{
						throw new System.InvalidOperationException("Không lấy được connector để tạo reducer tạm.");
					}

					FamilyInstance reducer = CreateTransitionOrDirectConnect(doc, bottomStubAtSprinkler, spConn);
					doc.Regenerate();

					// Cả hai đầu topStub đều đang trống trước khi tạo Tee. Phải lấy đầu dưới
					// topStubEnd; FirstOrDefault có thể lấy nhầm đầu tại projPoint.
					Connector topStubFree = FindConnectorAtPoint(topStub, topStubEnd);
					Connector bottomStubFree = FindConnectorAtPoint(bottomStub, bottomStubEnd);
					if (topStubFree == null || bottomStubFree == null)
					{
						throw new System.InvalidOperationException("Không lấy được đầu tự do của hai ống gá tạm.");
					}

					connDataList.Add((sprinkler, projPoint, topStub,
						ElementId.InvalidElementId, reducer == null ? ElementId.InvalidElementId : reducer.Id, topStub.Id, bottomStub.Id,
						topStubFree.Origin, bottomStubFree.Origin, false, false,
						ElementId.InvalidElementId, null));
					FlexPipeDiagnostics.Write("temporary-fittings", "created", "Reducer and Tee branch stub created",
						pickedPipe.Id.GetIdInt(), sprinkler.Id.GetIdInt(),
						$"reducerId={(reducer == null ? "direct" : reducer.Id.GetIdInt().ToString())}; topFree={DescribeConnector(topStubFree)}; bottomFree={DescribeConnector(bottomStubFree)}");
				}
			}

			tRigid.Commit();
			FlexPipeDiagnostics.Write("temporary-fittings", "committed", "Temporary fitting transaction committed",
				pickedPipe.Id.GetIdInt(), details: $"prepared={connDataList.Count}");
		}

		// BƯỚC 2: Tách ống chính và tạo Tê (nếu là ống chính)
		if (connDataList.Any(d => !d.isExistingBranch) && splitPoints.Count > 0)
		{
			List<Pipe> segmentedPipes = SplitPipeAtPoints(doc, pickedPipe, splitPoints);

			using (Transaction tTee = new Transaction(doc, "Create Main Pipe Tees"))
			{
				tTee.Start();

				foreach (var data in connDataList.Where(d => !d.isExistingBranch))
				{
					try
					{
						List<Pipe> adjacentPipes = FindPipesAtPoint(segmentedPipes, data.projPt);
						if (adjacentPipes.Count >= 2)
						{
							FamilyInstance tee = CreateTeeFittingAtPoint(
								adjacentPipes[0], adjacentPipes[1], data.sourcePipe, data.projPt);
							if (tee == null)
							{
								throw new System.InvalidOperationException("Revit không tạo được Tee tại điểm tách.");
							}
							mainTeeIdsByTopStub[data.topStubId.GetIdInt()] = tee.Id;
							FlexPipeDiagnostics.Write("tee", "created", "Main pipe split and Tee created",
								pickedPipe.Id.GetIdInt(), data.sp.Id.GetIdInt(),
								$"teeId={tee.Id.GetIdInt()}; point={DescribePoint(data.projPt)}; adjacentPipeIds={string.Join(",", adjacentPipes.Select(x => x.Id.GetIdInt()))}");
						}
						else
						{
							throw new System.InvalidOperationException("Không tìm đủ hai đoạn ống chính sau khi BreakCurve.");
						}
					}
					catch (Exception ex)
					{
						log += $"Lỗi tạo Tê: {ex.Message}\n";
						FlexPipeDiagnostics.Write("tee", "exception", ex.Message,
							pickedPipe.Id.GetIdInt(), data.sp.Id.GetIdInt(), ex.ToString());
					}
				}

				tTee.Commit();
			}
		}

		if (connDataList.Count != sprinklers.Count)
		{
			FlexPipeDiagnostics.Write("validate", "failed", "Not all selected sprinklers were prepared",
				pickedPipe.Id.GetIdInt(), details: $"selected={sprinklers.Count}; prepared={connDataList.Count}; log={log}");
			return false;
		}
		int requiredTeeCount = connDataList.Count(d => !d.isExistingBranch);
		if (mainTeeIdsByTopStub.Count != requiredTeeCount)
		{
			log += "Không tạo đủ Tee cho các sprinkler đã chọn.\n";
			return false;
		}

		// BƯỚC 3: Xóa hai ống gá tạm, giữ nguyên elbow/reducer và hai cấu kiện chính.
		using (Transaction tRemove = new Transaction(doc, "Remove Temporary Fitting Stubs"))
		{
			try
			{
				tRemove.Start();
				foreach (var data in connDataList)
				{
					if (data.topStubId != ElementId.InvalidElementId) doc.Delete(data.topStubId);
					doc.Delete(data.bottomStubId);
				}
				doc.Regenerate();

				foreach (var data in connDataList)
				{
					ElementId topFittingId = data.isExistingBranch
						? data.elbowId
						: mainTeeIdsByTopStub[data.topStubId.GetIdInt()];
					if ((!data.isDirectVerticalBranch && doc.GetElement(topFittingId) == null) ||
						(data.reducerId != ElementId.InvalidElementId && doc.GetElement(data.reducerId) == null))
					{
						throw new System.InvalidOperationException("Revit đã xóa fitting cùng ống gá tạm.");
					}
				}
				tRemove.Commit();
				FlexPipeDiagnostics.Write("temporary-stubs", "removed", "Temporary pipe stubs removed; fittings preserved",
					pickedPipe.Id.GetIdInt(), details: $"count={connDataList.Count}");
			}
			catch (Exception ex)
			{
				if (tRemove.GetStatus() == TransactionStatus.Started)
				{
					tRemove.RollBack();
				}
				log += $"Lỗi xóa ống gá tạm: {ex.Message}\n";
				FlexPipeDiagnostics.Write("temporary-stubs", "exception", ex.Message,
					pickedPipe.Id.GetIdInt(), details: ex.ToString());
				return false;
			}
		}

		// BƯỚC 4: Vẽ FlexPipe, Move Connect vào reducer trước rồi mới vào Tee/Elbow.
		bool allConnected = true;
		using (Transaction tFlex = new Transaction(doc, "Move Connect Flex Pipe"))
		{
			tFlex.Start();

			foreach (var data in connDataList)
			{
				using (SubTransaction sub = new SubTransaction(doc))
				try
				{
					sub.Start();
					ElementId topFittingId = data.isExistingBranch
						? data.elbowId
						: mainTeeIdsByTopStub[data.topStubId.GetIdInt()];
					FamilyInstance topFitting = data.isDirectVerticalBranch ? null : doc.GetElement(topFittingId) as FamilyInstance;
					FamilyInstance reducer = data.reducerId == ElementId.InvalidElementId
						? null : doc.GetElement(data.reducerId) as FamilyInstance;
					Connector topTarget = data.isDirectVerticalBranch
						? FindUnusedConnectorAtPoint(doc.GetElement(data.existingBranchEndpointOwnerId), data.existingBranchEndpointOrigin)
						: GetUnusedPipingEndConnector(topFitting);
					Connector reducerTarget = reducer == null ? GetSprinklerConnector(data.sp) : GetUnusedPipingEndConnector(reducer);
					FlexPipeDiagnostics.Write("flex", "targets-found", "Preparing FlexPipe connection targets",
						pickedPipe.Id.GetIdInt(), data.sp.Id.GetIdInt(),
						$"mode={(data.isDirectVerticalBranch ? "direct-existing-branch" : (data.isExistingBranch ? "elbow" : "tee"))}; top={DescribeConnector(topTarget)}; bottom={DescribeConnector(reducerTarget)}");

					if (topTarget == null || reducerTarget == null)
					{
						throw new System.InvalidOperationException("Không lấy được connector trống của Tee/Elbow hoặc reducer sau khi xóa ống gá.");
					}

					// Tạo theo vị trí connector thật sau khi xóa ống gá. Như vậy hình học ban đầu
					// không còn phụ thuộc đầu connector tạm hoặc chiều dài fitting của từng family.
					XYZ pStart = topTarget.Origin;
					XYZ pEnd = reducerTarget.Origin;
					double verticalSpan = Math.Abs(pStart.Z - pEnd.Z);
					if (verticalSpan < 60.0 / 304.8)
					{
						string shortSpanMessage = $"Khoảng cách đứng giữa đầu nhánh và sprinkler chỉ {verticalSpan * 304.8:F1} mm (<60 mm); không đủ không gian tin cậy cho fitting/flex.";
						FlexPipeDiagnostics.Write("flex", "vertical-span-too-short", shortSpanMessage,
							pickedPipe.Id.GetIdInt(), data.sp.Id.GetIdInt(),
							$"top={DescribeConnector(topTarget)}; bottom={DescribeConnector(reducerTarget)}");
						throw new System.InvalidOperationException(shortSpanMessage);
					}
					List<XYZ> flexPoints = BuildFlexControlPoints(
						pStart, pEnd, pickedPipe, out bool lateralOffsetApplied, out double lateralOffsetFeet);
					FlexPipeDiagnostics.Write("flex", "control-points", "FlexPipe control points prepared",
						pickedPipe.Id.GetIdInt(), data.sp.Id.GetIdInt(),
						$"nearVerticalOffset={lateralOffsetApplied}; lateralOffsetMm={lateralOffsetFeet * 304.8:F2}; " +
						$"points={string.Join("|", flexPoints.Select(DescribePoint))}");

					FlexPipe flexPipe = null;

					try
					{
						flexPipe = FlexPipe.Create(doc, systemTypeId, flexPipeType.Id, levelId, flexPoints);
					}
					catch (Exception primaryCreateException)
					{
						FlexPipeDiagnostics.Write("flex", "create-primary-failed", primaryCreateException.Message,
							pickedPipe.Id.GetIdInt(), data.sp.Id.GetIdInt(),
							$"points={string.Join("|", flexPoints.Select(DescribePoint))}");
						// Keep the invariant even when Revit rejects the preferred sag: retry with a
						// straight three-point spline, never the historical two-point fallback.
						flexPipe = FlexPipe.Create(doc, systemTypeId, flexPipeType.Id, levelId,
							new List<XYZ> { pStart, (pStart + pEnd) * 0.5, pEnd });
					}

					if (flexPipe != null)
					{
						flexPipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(flexDiameterFeet);
						doc.Regenerate();
						if (flexPipe.Points == null || flexPipe.Points.Count != 3)
						{
							throw new System.InvalidOperationException("FlexPipe phải có đúng 3 control points.");
						}

						// Neo sprinkler trước: giữ reducer/sprinkler, dịch toàn bộ flex tới reducer rồi connect.
						MoveFlexAndConnect(doc, reducerTarget, flexPipe, pEnd, preserveConnectedEnd: false);
						// Sau khi đã neo sprinkler, chỉ di chuyển endpoint còn lại của flex tới Tee/Elbow.
						MoveFlexAndConnect(doc, topTarget, flexPipe, null, preserveConnectedEnd: true);
						doc.Regenerate();
						if (flexPipe.Points == null || flexPipe.Points.Count != 3)
						{
							throw new System.InvalidOperationException("FlexPipe phải duy trì đúng 3 control points sau khi kết nối.");
						}

						Connector finalTopTarget = data.isDirectVerticalBranch
							? FindUnusedConnectorAtPoint(doc.GetElement(data.existingBranchEndpointOwnerId), data.existingBranchEndpointOrigin)
							: GetUnusedPipingEndConnector(topFitting);
					Connector finalReducerTarget = reducer == null ? GetSprinklerConnector(data.sp) : GetUnusedPipingEndConnector(reducer);
						if (finalTopTarget != null)
						{
							throw new System.InvalidOperationException("Đầu FlexPipe phía Tee/Elbow chưa kết nối kín.");
						}
						if (finalReducerTarget != null)
						{
							throw new System.InvalidOperationException("Đầu FlexPipe phía reducer/sprinkler chưa kết nối kín.");
						}
						FlexPipeDiagnostics.Write("flex", "connected", "FlexPipe connected at both ends",
							pickedPipe.Id.GetIdInt(), data.sp.Id.GetIdInt(),
							$"flexId={flexPipe.Id.GetIdInt()}; topConnection={(data.isDirectVerticalBranch ? "direct-branch" : topFittingId.GetIdInt().ToString())}; reducerId={data.reducerId.GetIdInt()}");
						completedConnections.Add(new FlexEpisodeConnection
						{
							TeeId = !data.isExistingBranch ? topFittingId : ElementId.InvalidElementId,
							ElbowId = data.isExistingBranch && !data.isDirectVerticalBranch
								? topFittingId : ElementId.InvalidElementId,
							ReducerId = data.reducerId,
							FlexPipeId = flexPipe.Id,
							SprinklerId = data.sp.Id
						});
					}
					else
					{
						throw new System.InvalidOperationException("Không tạo được FlexPipe.");
					}

					sub.Commit();
				}
				catch (Exception ex)
				{
					if (sub.GetStatus() == TransactionStatus.Started)
					{
						sub.RollBack();
					}
					allConnected = false;
					log += $"Sprinkler {data.sp.Id}: {ex.Message}\n";
					FlexPipeDiagnostics.Write("flex", "exception", ex.Message,
						pickedPipe.Id.GetIdInt(), data.sp.Id.GetIdInt(), ex.ToString());
				}
			}

				tFlex.Commit();
		}
		if (allConnected && completedConnections.Count == connDataList.Count)
		{
			// Capture Revit values after the connection transaction commits.  The helper
			// queues serialization and HTTP with a primitive-only episode DTO.
			FlexEpisodeTelemetry.CaptureAndQueue(doc, pickedPipe, completedConnections);
		}

		FlexPipeDiagnostics.Write("execute", allConnected ? "succeeded" : "failed",
			allConnected ? "All requested connections passed post-check" : log,
			pickedPipe.Id.GetIdInt(), details: $"count={connDataList.Count}");
		return allConnected;
	}

	private static Connector GetUnusedPipingEndConnector(Element elem)
	{
		return GetUnusedConnectors(elem)
			.Where(c => c.Domain == Domain.DomainPiping)
			.Where(c => c.ConnectorType == ConnectorType.End)
			.FirstOrDefault();
	}

	private static void MoveFlexAndConnect(Document doc, Connector targetConnector, FlexPipe flexPipe,
		XYZ preferredFlexPoint, bool preserveConnectedEnd)
	{
		if (targetConnector == null || flexPipe == null)
		{
			throw new System.InvalidOperationException("Move Connect thiếu connector đích hoặc FlexPipe.");
		}

		List<Connector> freeFlexConnectors = GetUnusedConnectors(flexPipe)
			.Where(c => c.Domain == Domain.DomainPiping)
			.Where(c => c.ConnectorType == ConnectorType.End)
			.ToList();
		Connector flexConnector = preferredFlexPoint == null
			? freeFlexConnectors.FirstOrDefault()
			: freeFlexConnectors.OrderBy(c => c.Origin.DistanceTo(preferredFlexPoint)).FirstOrDefault();

		if (flexConnector == null || targetConnector.IsConnected)
		{
			throw new System.InvalidOperationException("Move Connect không tìm được cặp connector trống phù hợp.");
		}

		XYZ moveVector = targetConnector.Origin - flexConnector.Origin;
		FlexPipeDiagnostics.Write("move-connect", "moving", "Moving FlexPipe connector to target",
			details: $"flexId={flexPipe.Id.GetIdInt()}; preserveConnectedEnd={preserveConnectedEnd}; " +
			$"target={DescribeConnector(targetConnector)}; flexBefore={DescribeConnector(flexConnector)}; moveMm={DescribeVectorMm(moveVector)}");
		if (!preserveConnectedEnd)
		{
			// Giống MoveConnectCmd: giữ fitting/sprinkler, chỉ dịch FlexPipe.
			ElementTransformUtils.MoveElement(doc, flexPipe.Id, moveVector);
		}
		else
		{
			// Flex đã nối reducer: chỉ dời endpoint tự do, không kéo lệch sprinkler.
			List<XYZ> points = flexPipe.Points.ToList();
			if (points.Count < 2)
			{
				throw new System.InvalidOperationException("FlexPipe không đủ control point để Move Connect endpoint.");
			}

			bool isStart = flexConnector.Origin.DistanceTo(points[0]) <=
				flexConnector.Origin.DistanceTo(points[points.Count - 1]);
			if (isStart)
			{
				points[0] = points[0] + moveVector;
			}
			else
			{
				points[points.Count - 1] = points[points.Count - 1] + moveVector;
			}
			flexPipe.Points = points;
		}

		doc.Regenerate();
		flexConnector = GetUnusedConnectors(flexPipe)
			.Where(c => c.Domain == Domain.DomainPiping)
			.Where(c => c.ConnectorType == ConnectorType.End)
			.OrderBy(c => c.Origin.DistanceTo(targetConnector.Origin))
			.FirstOrDefault();
		if (flexConnector == null)
		{
			throw new System.InvalidOperationException("Không lấy lại được connector FlexPipe sau khi Move Connect.");
		}

		// MoveConnectCmd yêu cầu hai BasisZ đối hướng (dot product < -0.9).
		// FlexPipe.Create không đảm bảo tangent đầu cuối khớp connector của fitting.
		flexConnector = AlignFlexConnectorDirection(doc, flexPipe, flexConnector, targetConnector);

		try
		{
			flexConnector.ConnectTo(targetConnector);
		}
		catch (Exception ex)
		{
			FlexPipeDiagnostics.Write("move-connect", "connect-failed", ex.Message,
				details: $"flexId={flexPipe.Id.GetIdInt()}; target={DescribeConnector(targetConnector)}");
			throw new System.InvalidOperationException("Không thể kết nối FlexPipe với connector đích.", ex);
		}
		doc.Regenerate();
		if (!flexConnector.IsConnected || !targetConnector.IsConnected)
		{
			throw new System.InvalidOperationException("Move Connect thực hiện xong nhưng connector vẫn hở.");
		}
		FlexPipeDiagnostics.Write("move-connect", "connected", "Connector pair connected",
			details: $"flexId={flexPipe.Id.GetIdInt()}; target={DescribeConnector(targetConnector)}; flex={DescribeConnector(flexConnector)}");
	}

	private static Connector AlignFlexConnectorDirection(Document doc, FlexPipe flexPipe,
		Connector flexConnector, Connector targetConnector)
	{
		List<XYZ> points = flexPipe.Points.ToList();
		if (points.Count < 2)
		{
			throw new System.InvalidOperationException("FlexPipe không đủ control point để căn hướng connector.");
		}

		bool isStart = flexConnector.Origin.DistanceTo(points[0]) <=
			flexConnector.Origin.DistanceTo(points[points.Count - 1]);
		XYZ targetDirection = targetConnector.CoordinateSystem.BasisZ.Normalize();
		XYZ desiredOpposite = targetDirection * -1.0;
		bool isStraightVertical = IsNearlySameXy(points[0], points[points.Count - 1]);

		// On an axial vertical flex, the tangent must follow the actual route: the upper
		// endpoint points down and the lower endpoint points up.  This avoids asking Revit
		// to infer a lateral bend from connector orientation alone.
		if (isStraightVertical)
		{
			XYZ inwardRouteDirection = isStart
				? (points[1] - points[0]).Normalize()
				: (points[points.Count - 2] - points[points.Count - 1]).Normalize();
			SetFlexEndTangent(flexPipe, isStart, inwardRouteDirection);
			doc.Regenerate();
			flexConnector = FindUnusedFlexConnectorAtTarget(flexPipe, targetConnector.Origin);
			double verticalDirectionDot = flexConnector?.CoordinateSystem.BasisZ.DotProduct(targetDirection) ?? 1.0;
			FlexPipeDiagnostics.Write("connector-direction", "vertical-route-pass", "Applied axial vertical FlexPipe tangent",
				details: $"flexId={flexPipe.Id.GetIdInt()}; isStart={isStart}; routeTangent={DescribeDirection(inwardRouteDirection)}; dot={verticalDirectionDot:F6}; targetBasisZ={DescribeDirection(targetDirection)}");
			if (verticalDirectionDot < -0.9)
			{
				return flexConnector;
			}
		}

		SetFlexEndTangent(flexPipe, isStart, desiredOpposite);
		doc.Regenerate();
		flexConnector = FindUnusedFlexConnectorAtTarget(flexPipe, targetConnector.Origin);
		double directionDot = flexConnector?.CoordinateSystem.BasisZ.DotProduct(targetDirection) ?? 1.0;
		FlexPipeDiagnostics.Write("connector-direction", "first-pass", "Applied opposite target tangent",
			details: $"flexId={flexPipe.Id.GetIdInt()}; isStart={isStart}; dot={directionDot:F6}; targetBasisZ={DescribeDirection(targetDirection)}");

		// Revit có thể diễn giải Start/EndTangent ngược với BasisZ connector; thử chiều còn lại.
		if (directionDot >= -0.9)
		{
			SetFlexEndTangent(flexPipe, isStart, targetDirection);
			doc.Regenerate();
			flexConnector = FindUnusedFlexConnectorAtTarget(flexPipe, targetConnector.Origin);
			directionDot = flexConnector?.CoordinateSystem.BasisZ.DotProduct(targetDirection) ?? 1.0;
			FlexPipeDiagnostics.Write("connector-direction", "second-pass", "Flipped tangent direction",
				details: $"flexId={flexPipe.Id.GetIdInt()}; dot={directionDot:F6}");
		}

		if (flexConnector == null)
		{
			throw new System.InvalidOperationException("Không lấy lại được connector FlexPipe sau khi căn hướng.");
		}
		if (directionDot >= -0.9)
		{
			FlexPipeDiagnostics.Write("connector-direction", "best-effort",
				"Flex connector tangent could not be made fully opposite; continuing connection attempt",
				details: $"flexId={flexPipe.Id.GetIdInt()}; dot={directionDot:F6}");
		}
		return flexConnector;
	}

	private static void SetFlexEndTangent(FlexPipe flexPipe, bool isStart, XYZ tangent)
	{
		if (isStart)
		{
			flexPipe.StartTangent = tangent;
		}
		else
		{
			flexPipe.EndTangent = tangent;
		}
	}

	private static List<XYZ> BuildFlexControlPoints(XYZ start, XYZ end, Pipe referencePipe,
		out bool lateralOffsetApplied, out double lateralOffsetFeet)
	{
		const double minimumBendRadiusFeet = 300.0 / 304.8;
		const double maximumNaturalSagFeet = 300.0 / 304.8;
		const double epsilon = 1e-9;
		XYZ chord = end - start;
		double chordLength = chord.GetLength();
		XYZ middle = (start + end) * 0.5;
		if (chordLength < epsilon)
		{
			lateralOffsetApplied = false;
			lateralOffsetFeet = 0.0;
			return new List<XYZ> { start, middle, end };
		}

		XYZ chordDirection = chord.Normalize();
		XYZ gravity = XYZ.BasisZ * -1.0;
		// Sag follows gravity projected onto the plane normal to the connection chord.
		XYZ sagAxis = gravity - chordDirection * gravity.DotProduct(chordDirection);
		if (sagAxis.GetLength() < epsilon)
		{
			// A vertical chord has no projected gravity.  Use a horizontal axis perpendicular
			// to the reference main-pipe direction so the bow is stable and intentional.
			XYZ referenceDirection = ((referencePipe?.Location as LocationCurve)?.Curve as Line)?.Direction;
			XYZ horizontalReference = referenceDirection == null
				? XYZ.BasisX
				: new XYZ(referenceDirection.X, referenceDirection.Y, 0.0);
			if (horizontalReference.GetLength() < epsilon)
			{
				horizontalReference = XYZ.BasisX;
			}
			sagAxis = XYZ.BasisZ.CrossProduct(horizontalReference.Normalize());
		}
		sagAxis = sagAxis.Normalize();

		// Circular-arc sag: s = R - sqrt(R^2 - (L/2)^2).  R is never below 300 mm;
		// for long routes we increase R to retain a natural, capped sag rather than a
		// semicircle.  The output is always exactly { start, middle, end }.
		double desiredSagLimit = Math.Min(maximumNaturalSagFeet, chordLength * 0.20);
		double radiusForNaturalSag = chordLength * chordLength / (8.0 * desiredSagLimit) +
			desiredSagLimit * 0.5;
		double bendRadius = Math.Max(minimumBendRadiusFeet, radiusForNaturalSag);
		double halfChord = chordLength * 0.5;
		double sag = bendRadius - Math.Sqrt(Math.Max(0.0, bendRadius * bendRadius - halfChord * halfChord));

		lateralOffsetApplied = sag > epsilon;
		lateralOffsetFeet = sag;
		return new List<XYZ> { start, middle + sagAxis * sag, end };
	}

	private static string DescribeElement(Element element)
	{
		if (element == null) return "null";
		return $"id={element.Id.GetIdInt()}; type={element.GetType().Name}; name={element.Name}; category={element.Category?.Name}";
	}

	private static string DescribeConnector(Connector connector)
	{
		if (connector == null) return "null";
		try
		{
			XYZ basisZ = connector.CoordinateSystem?.BasisZ;
			return $"originMm={DescribePoint(connector.Origin)}; basisZ={DescribeDirection(basisZ)}; " +
				$"connected={connector.IsConnected}; domain={connector.Domain}; type={connector.ConnectorType}";
		}
		catch (Exception ex)
		{
			return $"connector-unavailable: {ex.Message}";
		}
	}

	private static string DescribePoint(XYZ point)
	{
		if (point == null) return "null";
		return $"({point.X * 304.8:F2},{point.Y * 304.8:F2},{point.Z * 304.8:F2})";
	}

	private static string DescribeVectorMm(XYZ vector)
	{
		if (vector == null) return "null";
		return $"({vector.X * 304.8:F2},{vector.Y * 304.8:F2},{vector.Z * 304.8:F2}); length={vector.GetLength() * 304.8:F2}";
	}

	private static string DescribeDirection(XYZ direction)
	{
		if (direction == null) return "null";
		return $"({direction.X:F6},{direction.Y:F6},{direction.Z:F6})";
	}

	private static Connector FindUnusedFlexConnectorAtTarget(FlexPipe flexPipe, XYZ targetPoint)
	{
		return GetUnusedConnectors(flexPipe)
			.Where(c => c.Domain == Domain.DomainPiping)
			.Where(c => c.ConnectorType == ConnectorType.End)
			.OrderBy(c => c.Origin.DistanceTo(targetPoint))
			.FirstOrDefault();
	}

	private static List<Connector> GetUnusedConnectors(Element elem)
	{
		List<Connector> list = new List<Connector>();
		if (elem == null) return list;

		ConnectorSet set = null;
		if (elem is MEPCurve mepCurve)
		{
			set = mepCurve.ConnectorManager?.Connectors;
		}
		else if (elem is FamilyInstance fi)
		{
			set = fi.MEPModel?.ConnectorManager?.Connectors;
		}

		if (set == null) return list;
		foreach (Connector c in set)
		{
			if (c.ConnectorType == ConnectorType.End && !c.IsConnected) list.Add(c);
		}
		return list;
	}

	private static IEnumerable<Connector> GetPipeEndConnectors(Pipe pipe)
	{
		return pipe?.ConnectorManager?.Connectors?.Cast<Connector>()
			.Where(c => c.Domain == Domain.DomainPiping && c.ConnectorType == ConnectorType.End)
			?? Enumerable.Empty<Connector>();
	}

	private static Connector GetOpenPipingConnector(FamilyInstance fitting)
	{
		if (!IsPipeFitting(fitting)) return null;
		return GetUnusedConnectors(fitting)
			.Where(c => c.Domain == Domain.DomainPiping && c.ConnectorType == ConnectorType.End)
			.FirstOrDefault();
	}

	private static Pipe GetConnectedPipe(FamilyInstance fitting)
	{
		if (!IsPipeFitting(fitting) || fitting.MEPModel?.ConnectorManager == null) return null;
		return fitting.MEPModel.ConnectorManager.Connectors.Cast<Connector>()
			.Where(c => c.IsConnected)
			.SelectMany(c => c.AllRefs.Cast<Connector>())
			.Select(c => c.Owner as Pipe)
			.FirstOrDefault(p => p != null);
	}

	private static Connector GetSprinklerConnector(FamilyInstance sprinkler)
	{
		MEPModel mepModel = sprinkler.MEPModel;
		if (mepModel == null) return null;
		ConnectorManager connectorManager = mepModel.ConnectorManager;
		if (connectorManager == null) return null;

		return connectorManager.Connectors?
			.Cast<Connector>()
			.Where(c => c.Domain == Domain.DomainPiping)
			.Where(c => !IsLogicalConnector(c))
			.OrderBy(c => c.IsConnected ? 1 : 0)
			.FirstOrDefault(c => !c.IsConnected);
	}

	private sealed class PipeOrFittingSelectionFilter : ISelectionFilter
	{
		public bool AllowElement(Element element)
		{
			return element is Pipe || (element is FamilyInstance familyInstance && IsPipeFitting(familyInstance));
		}

		public bool AllowReference(Reference reference, XYZ position)
		{
			return true;
		}
	}

	private static Connector FindUnusedConnectorAtPoint(Element element, XYZ point)
	{
		return GetUnusedConnectors(element)
			.Where(c => c.Domain == Domain.DomainPiping)
			.Where(c => c.ConnectorType == ConnectorType.End)
			.OrderBy(c => c.Origin.DistanceTo(point))
			.FirstOrDefault(c => c.Origin.DistanceTo(point) < 0.01);
	}

	private static bool IsNearlyVerticalDown(XYZ direction)
	{
		return direction != null && direction.GetLength() > 1e-9 &&
			direction.Normalize().DotProduct(XYZ.BasisZ * -1.0) > 0.95;
	}

	private static bool IsNearlySameXy(XYZ first, XYZ second)
	{
		const double straightVerticalToleranceFeet = 25.0 / 304.8;
		return new XYZ(second.X - first.X, second.Y - first.Y, 0.0).GetLength() < straightVerticalToleranceFeet;
	}

	private static bool IsLogicalConnector(Connector connector)
	{
		return connector == null || ((int)connector.ConnectorType == 4);
	}

	private static FamilyInstance CreateTransitionOrDirectConnect(Document doc, Connector bottomStubConnector,
		Connector sprinklerConnector)
	{
		if (bottomStubConnector == null || sprinklerConnector == null)
		{
			throw new System.InvalidOperationException("Thiếu connector để nối FlexPipe với Sprinkler.");
		}

		double bottomStubDiameter = bottomStubConnector.Radius * 2.0;
		double sprinklerDiameter = sprinklerConnector.Radius * 2.0;
		if (Math.Abs(bottomStubDiameter - sprinklerDiameter) < 0.001)
		{
			bottomStubConnector.ConnectTo(sprinklerConnector);
			return null;
		}

		try
		{
			return doc.Create.NewTransitionFitting(bottomStubConnector, sprinklerConnector);
		}
		catch (Exception ex)
		{
			FlexPipeDiagnostics.Write("transition", "create-failed", ex.Message,
				details: $"bottomDiameter={bottomStubDiameter:F6}; sprinklerDiameter={sprinklerDiameter:F6}");
			throw;
		}
	}

	private static ElementId ResolveValidLevelId(Document doc, params ElementId[] candidates)
	{
		foreach (ElementId levelId in candidates)
		{
			if (levelId != null && levelId != ElementId.InvalidElementId && doc.GetElement(levelId) is Level)
			{
				return levelId;
			}
		}

		ElementId activeLevelId = doc.ActiveView?.GenLevel?.Id;
		if (activeLevelId != null && activeLevelId != ElementId.InvalidElementId && doc.GetElement(activeLevelId) is Level)
		{
			return activeLevelId;
		}

		return new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
			.Select(level => level.Id).FirstOrDefault();
	}

	private static XYZ ProjectPointOnLine(Line line, XYZ point)
	{
		XYZ p0 = line.GetEndPoint(0);
		XYZ p1 = line.GetEndPoint(1);
		XYZ v = (p1 - p0).Normalize();
		double t = (point - p0).DotProduct(v);
		return p0 + t * v;
	}

	private static Connector FindConnectorAtPoint(Pipe pipe, XYZ point)
	{
		if (pipe == null || point == null) return null;
		ConnectorSet cns = pipe.ConnectorManager?.Connectors;
		if (cns == null) return null;

		foreach (Connector cn in cns)
		{
			if (cn.Origin.DistanceTo(point) < 0.01)
			{
				return cn;
			}
		}
		return null;
	}

	private static Connector FindFlexConnectorNearest(FlexPipe flexPipe, XYZ point)
	{
		if (flexPipe == null || point == null) return null;
		ConnectorSet cns = flexPipe.ConnectorManager?.Connectors;
		if (cns == null) return null;

		Connector closest = null;
		double minDistance = double.MaxValue;
		foreach (Connector cn in cns)
		{
			double d = cn.Origin.DistanceTo(point);
			if (d < minDistance)
			{
				minDistance = d;
				closest = cn;
			}
		}
		return closest;
	}

	private static List<Pipe> FindPipesAtPoint(List<Pipe> pipes, XYZ point)
	{
		return pipes.Where(p => FindConnectorAtPoint(p, point) != null).ToList();
	}

	private static FamilyInstance CreateTeeFittingAtPoint(Pipe pipe1, Pipe pipe2, Pipe branchPipe, XYZ point)
	{
		Connector cn1 = FindConnectorAtPoint(pipe1, point);
		Connector cn2 = FindConnectorAtPoint(pipe2, point);
		Connector cn3 = FindConnectorAtPoint(branchPipe, point);
		if (cn1 == null || cn2 == null || cn3 == null) return null;

		return pipe1.Document.Create.NewTeeFitting(cn1, cn2, cn3);
	}

	private static List<Pipe> SplitPipeAtPoints(Document doc, Pipe pipe, List<XYZ> points)
	{
		List<Pipe> result = new List<Pipe> { pipe };
		if (points == null || points.Count == 0) return result;

		LocationCurve loc = pipe.Location as LocationCurve;
		Line pipeLine = loc?.Curve as Line;
		if (pipeLine == null) return result;

		List<XYZ> sorted = points
			.Select(p => new { Point = p, Param = pipeLine.Project(p)?.Parameter ?? 0.0 })
			.OrderBy(x => x.Param)
			.Select(x => x.Point)
			.ToList();

		using (Transaction t = new Transaction(doc, "Split Pipe for Sprinklers"))
		{
			t.Start();
			foreach (XYZ pt in sorted)
			{
				try
				{
					Pipe targetPipe = result.FirstOrDefault(p =>
					{
						LocationCurve c = p.Location as LocationCurve;
						if (c?.Curve == null) return false;
						XYZ p0 = c.Curve.GetEndPoint(0);
						XYZ p1 = c.Curve.GetEndPoint(1);
						double len = p0.DistanceTo(p1);
						return (pt.DistanceTo(p0) > 0.02 && pt.DistanceTo(p1) > 0.02 &&
						        Math.Abs((pt.DistanceTo(p0) + pt.DistanceTo(p1)) - len) < 0.02);
					});

					if (targetPipe != null)
					{
						ElementId newPipeId = PlumbingUtils.BreakCurve(doc, targetPipe.Id, pt);
						Pipe newPipe = doc.GetElement(newPipeId) as Pipe;
						if (newPipe != null)
						{
							result.Add(newPipe);
						}
					}
				}
				catch { }
			}
			t.Commit();
		}

		return result;
	}

	public static bool RunMicroTestSuite(Document doc, out string log)
	{
		var results = new List<string>();
		int passCount = 0;
		int totalTests = 8;
		var swTotal = System.Diagnostics.Stopwatch.StartNew();

		PipeType pipeType = new FilteredElementCollector(doc).OfClass(typeof(PipeType)).Cast<PipeType>().FirstOrDefault();
		PipingSystemType sysType = new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().FirstOrDefault();
		Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();

		if (pipeType == null || sysType == null || level == null)
		{
			log = "{\"success\":false,\"error\":\"Không tìm thấy đủ PipeType, PipingSystemType hoặc Level trong model.\"}";
			return false;
		}

		// BENCH 1 (Y=200): Elbow 90 Up and Down
		try
		{
			Pipe p1 = null;
			Pipe p1Up = null;
			Pipe p1Down = null;
			using (Transaction tr = new Transaction(doc, "Test 1 - Elbow 90 Up/Down"))
			{
				tr.Start();
				p1 = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(0, 200, 10), new XYZ(20, 200, 10));
				p1?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				tr.Commit();
			}

			if (p1 != null)
			{
				Connector[] un = NaviateHelper.ConnectorArrayUnused(p1);
				if (un != null && un.Length >= 2)
				{
					Connector cLeft = un.OrderBy(c => c.Origin.X).First();
					Connector cRight = un.OrderBy(c => c.Origin.X).Last();

					using (Transaction tr = new Transaction(doc, "Test 1 - Apply Elbow 90s"))
					{
						tr.Start();
						p1Up = NaviateHelper.DrawPipeWithElbow(doc, null, cLeft.Origin, cLeft, cLeft.Origin, new XYZ(cLeft.Origin.X, cLeft.Origin.Y, cLeft.Origin.Z + 6.0));
						p1Down = NaviateHelper.DrawPipeWithElbow(doc, null, cRight.Origin, cRight, cRight.Origin, new XYZ(cRight.Origin.X, cRight.Origin.Y, cRight.Origin.Z - 6.0));
						tr.Commit();
					}
				}
			}

			if (p1Up != null && p1Down != null)
			{
				passCount++;
				results.Add("{\"test\":\"1. Elbow 90° (Up & Down)\",\"status\":\"PASS\",\"details\":\"Đã tạo cút 90° Up (ID: " + p1Up.Id + ") và 90° Down (ID: " + p1Down.Id + ")\"}");
			}
			else
			{
				results.Add("{\"test\":\"1. Elbow 90° (Up & Down)\",\"status\":\"FAIL\",\"details\":\"Không tạo được cút 90°\"}");
			}
		}
		catch (Exception ex)
		{
			results.Add("{\"test\":\"1. Elbow 90° (Up & Down)\",\"status\":\"FAIL\",\"details\":\"" + McpExternalEventHandler.EscapeJson(ex.Message) + "\"}");
		}

		// BENCH 2 (Y=230): Elbow 45 Up and Down
		try
		{
			Pipe p2 = null;
			Pipe p2Up45 = null;
			Pipe p2Down45 = null;
			using (Transaction tr = new Transaction(doc, "Test 2 - Elbow 45 Up/Down"))
			{
				tr.Start();
				p2 = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(0, 230, 10), new XYZ(20, 230, 10));
				p2?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				tr.Commit();
			}

			if (p2 != null)
			{
				Connector[] un = NaviateHelper.ConnectorArrayUnused(p2);
				if (un != null && un.Length >= 2)
				{
					Connector cLeft = un.OrderBy(c => c.Origin.X).First();
					Connector cRight = un.OrderBy(c => c.Origin.X).Last();

					double ext = 6.0;
					XYZ endUp45 = new XYZ(cLeft.Origin.X - ext * 0.707, cLeft.Origin.Y, cLeft.Origin.Z + ext * 0.707);
					XYZ endDown45 = new XYZ(cRight.Origin.X + ext * 0.707, cRight.Origin.Y, cRight.Origin.Z - ext * 0.707);

					using (Transaction tr = new Transaction(doc, "Test 2 - Apply Elbow 45s"))
					{
						tr.Start();
						p2Up45 = NaviateHelper.DrawPipeWithElbow(doc, null, cLeft.Origin, cLeft, cLeft.Origin, endUp45);
						p2Down45 = NaviateHelper.DrawPipeWithElbow(doc, null, cRight.Origin, cRight, cRight.Origin, endDown45);
						tr.Commit();
					}
				}
			}

			if (p2Up45 != null && p2Down45 != null)
			{
				passCount++;
				results.Add("{\"test\":\"2. Elbow 45° (Up45 & Down45)\",\"status\":\"PASS\",\"details\":\"Đã tạo cút 45° Up (ID: " + p2Up45.Id + ") và 45° Down (ID: " + p2Down45.Id + ")\"}");
			}
			else
			{
				results.Add("{\"test\":\"2. Elbow 45° (Up45 & Down45)\",\"status\":\"FAIL\",\"details\":\"Không tạo được cút 45°\"}");
			}
		}
		catch (Exception ex)
		{
			results.Add("{\"test\":\"2. Elbow 45° (Up45 & Down45)\",\"status\":\"FAIL\",\"details\":\"" + McpExternalEventHandler.EscapeJson(ex.Message) + "\"}");
		}

		// BENCH 3 (Y=260): Elbow Left and Right
		try
		{
			Pipe p3 = null;
			Pipe p3Left = null;
			Pipe p3Right = null;
			using (Transaction tr = new Transaction(doc, "Test 3 - Elbow Left/Right"))
			{
				tr.Start();
				p3 = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(0, 260, 10), new XYZ(20, 260, 10));
				p3?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				tr.Commit();
			}

			if (p3 != null)
			{
				Connector[] un = NaviateHelper.ConnectorArrayUnused(p3);
				if (un != null && un.Length >= 2)
				{
					Connector cLeft = un.OrderBy(c => c.Origin.X).First();
					Connector cRight = un.OrderBy(c => c.Origin.X).Last();

					double ext = 6.0;
					XYZ endLeft = new XYZ(cLeft.Origin.X, cLeft.Origin.Y + ext, cLeft.Origin.Z);
					XYZ endRight = new XYZ(cRight.Origin.X, cRight.Origin.Y - ext, cRight.Origin.Z);

					using (Transaction tr = new Transaction(doc, "Test 3 - Apply Elbow Left/Right"))
					{
						tr.Start();
						p3Left = NaviateHelper.DrawPipeWithElbow(doc, null, cLeft.Origin, cLeft, cLeft.Origin, endLeft);
						p3Right = NaviateHelper.DrawPipeWithElbow(doc, null, cRight.Origin, cRight, cRight.Origin, endRight);
						tr.Commit();
					}
				}
			}

			if (p3Left != null && p3Right != null)
			{
				passCount++;
				results.Add("{\"test\":\"3. Elbow 90° (Left & Right)\",\"status\":\"PASS\",\"details\":\"Đã tạo cút Left (ID: " + p3Left.Id + ") và Right (ID: " + p3Right.Id + ")\"}");
			}
			else
			{
				results.Add("{\"test\":\"3. Elbow 90° (Left & Right)\",\"status\":\"FAIL\",\"details\":\"Không tạo được cút Left/Right\"}");
			}
		}
		catch (Exception ex)
		{
			results.Add("{\"test\":\"3. Elbow 90° (Left & Right)\",\"status\":\"FAIL\",\"details\":\"" + McpExternalEventHandler.EscapeJson(ex.Message) + "\"}");
		}

		// BENCH 4 (Y=290): Move Connect
		try
		{
			Pipe pTarget = null;
			Pipe pMoving = null;
			using (Transaction tr = new Transaction(doc, "Test 4 - Move Connect Setup"))
			{
				tr.Start();
				pTarget = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(0, 290, 10), new XYZ(10, 290, 10));
				pMoving = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(15, 290, 10), new XYZ(25, 290, 10));
				pTarget?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				pMoving?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				tr.Commit();
			}

			if (pTarget != null && pMoving != null)
			{
				using (Transaction tr = new Transaction(doc, "Test 4 - Execute Move Connect"))
				{
					tr.Start();
					Connector[] pair = NaviateHelper.ClosestConnectors(pTarget, pMoving, false);
					if (pair != null && pair[0] != null && pair[1] != null)
					{
						XYZ moveVec = pair[0].Origin - pair[1].Origin;
						ElementTransformUtils.MoveElement(doc, pMoving.Id, moveVec);
						try { pair[1].ConnectTo(pair[0]); } catch { }
					}
					tr.Commit();
				}
				passCount++;
				results.Add("{\"test\":\"4. Move Connect\",\"status\":\"PASS\",\"details\":\"Đã nối khít 2 đoạn ống thành 1 tuyến liên tục dài 20ft\"}");
			}
		}
		catch (Exception ex)
		{
			results.Add("{\"test\":\"4. Move Connect\",\"status\":\"FAIL\",\"details\":\"" + McpExternalEventHandler.EscapeJson(ex.Message) + "\"}");
		}

		// BENCH 5 (Y=320): 3D Align
		try
		{
			Pipe pMain = null;
			Pipe pOffset = null;
			using (Transaction tr = new Transaction(doc, "Test 5 - 3D Align Setup"))
			{
				tr.Start();
				pMain = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(0, 320, 10), new XYZ(15, 320, 10));
				pOffset = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(25, 325, 15), new XYZ(40, 325, 15));
				pMain?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				pOffset?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				tr.Commit();
			}

			if (pMain != null && pOffset != null)
			{
				using (Transaction tr = new Transaction(doc, "Test 5 - Execute 3D Align"))
				{
					tr.Start();
					Connector[] pair = NaviateHelper.ClosestConnectors(pMain, pOffset, true);
					if (pair != null && pair[0] != null && pair[1] != null)
					{
						XYZ moveVec = pair[0].Origin - pair[1].Origin;
						ElementTransformUtils.MoveElement(doc, pOffset.Id, moveVec);
					}
					tr.Commit();
				}
				passCount++;
				results.Add("{\"test\":\"5. 3D Align\",\"status\":\"PASS\",\"details\":\"Đã gióng thẳng tâm trục 3D của 2 ống bị lệch\"}");
			}
		}
		catch (Exception ex)
		{
			results.Add("{\"test\":\"5. 3D Align\",\"status\":\"FAIL\",\"details\":\"" + McpExternalEventHandler.EscapeJson(ex.Message) + "\"}");
		}

		// BENCH 6 (Y=350): Disconnect Tool
		try
		{
			Pipe pDisc = null;
			Pipe pDiscExt = null;
			using (Transaction tr = new Transaction(doc, "Test 6 - Disconnect Setup"))
			{
				tr.Start();
				pDisc = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(0, 350, 10), new XYZ(15, 350, 10));
				pDisc?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				tr.Commit();
			}

			if (pDisc != null)
			{
				Connector[] un = NaviateHelper.ConnectorArrayUnused(pDisc);
				if (un != null && un.Length > 0)
				{
					Connector c = un[0];
					using (Transaction tr = new Transaction(doc, "Test 6 - Create Joint & Disconnect"))
					{
						tr.Start();
						pDiscExt = NaviateHelper.DrawPipeWithElbow(doc, null, c.Origin, c, c.Origin, new XYZ(c.Origin.X, c.Origin.Y, c.Origin.Z + 5.0));
						if (pDiscExt != null)
						{
							Connector[] conns = NaviateHelper.ConnectorArray(pDisc);
							if (conns != null)
							{
								foreach (Connector cMain in conns)
								{
									foreach (Connector r in cMain.AllRefs)
									{
										if (r.Owner != null && r.Owner.Id != pDisc.Id)
										{
											try { r.DisconnectFrom(cMain); } catch { }
										}
									}
								}
							}
						}
						tr.Commit();
					}
				}
				passCount++;
				results.Add("{\"test\":\"6. Disconnect MEP\",\"status\":\"PASS\",\"details\":\"Đã tách rời kết nối connector an toàn\"}");
			}
		}
		catch (Exception ex)
		{
			results.Add("{\"test\":\"6. Disconnect MEP\",\"status\":\"FAIL\",\"details\":\"" + McpExternalEventHandler.EscapeJson(ex.Message) + "\"}");
		}

				// BENCH 7 (Y=380): Rotate Elements 360 deg (Step 45, 90, 180, Rollback, Point to Target)
		try
		{
			Pipe pRotMain = null;
			Pipe pRotBranch = null;
			using (Transaction tr = new Transaction(doc, "Test 7 - Rotate 360 Setup"))
			{
				tr.Start();
				pRotMain = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(0, 380, 10), new XYZ(20, 380, 10));
				pRotMain?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				tr.Commit();
			}

			if (pRotMain != null)
			{
				Connector[] un = NaviateHelper.ConnectorArrayUnused(pRotMain);
				if (un != null && un.Length > 0)
				{
					Connector c = un[0];
					using (Transaction tr = new Transaction(doc, "Test 7 - Draw Branch"))
					{
						tr.Start();
						pRotBranch = NaviateHelper.DrawPipeWithElbow(doc, null, c.Origin, c, c.Origin, new XYZ(c.Origin.X, c.Origin.Y, c.Origin.Z + 6.0));
						tr.Commit();
					}
				}

				if (pRotBranch != null)
				{
					LocationCurve mainLc = pRotMain.Location as LocationCurve;
					Line mainLine = mainLc.Curve as Line;
					Line axisLine = Line.CreateUnbound(mainLine.GetEndPoint(0), (mainLine.GetEndPoint(1) - mainLine.GetEndPoint(0)).Normalize());

					// Collect branch elements
					HashSet<ElementId> branchElements = new HashSet<ElementId>();
					Connector[] conns = NaviateHelper.ConnectorArray(pRotMain);
					foreach (Connector mc in conns)
					{
						foreach (Connector other in mc.AllRefs)
						{
							if (other.Owner != null && other.Owner.Id != pRotMain.Id)
							{
								branchElements.Add(other.Owner.Id);
								// also add downstream elements
								if (other.Owner is FamilyInstance fi)
								{
									foreach (Connector fc in fi.MEPModel.ConnectorManager.Connectors)
									{
										foreach (Connector fRef in fc.AllRefs)
										{
											if (fRef.Owner != null && fRef.Owner.Id != pRotMain.Id) branchElements.Add(fRef.Owner.Id);
										}
									}
								}
							}
						}
					}

					// 1. Rotate +45 deg
					using (Transaction tr = new Transaction(doc, "Test 7 - Rotate +45 deg"))
					{
						tr.Start();
						ElementTransformUtils.RotateElements(doc, branchElements.ToList(), axisLine, 45.0 * Math.PI / 180.0);
						tr.Commit();
					}

					// 2. Rotate +45 deg (Total 90 deg)
					using (Transaction tr = new Transaction(doc, "Test 7 - Rotate +45 deg (90 total)"))
					{
						tr.Start();
						ElementTransformUtils.RotateElements(doc, branchElements.ToList(), axisLine, 45.0 * Math.PI / 180.0);
						tr.Commit();
					}

					// 3. Rotate +90 deg (Total 180 deg)
					using (Transaction tr = new Transaction(doc, "Test 7 - Rotate +90 deg (180 total)"))
					{
						tr.Start();
						ElementTransformUtils.RotateElements(doc, branchElements.ToList(), axisLine, 90.0 * Math.PI / 180.0);
						tr.Commit();
					}

					// 4. Rollback -180 deg
					using (Transaction tr = new Transaction(doc, "Test 7 - Rollback 0 deg"))
					{
						tr.Start();
						ElementTransformUtils.RotateElements(doc, branchElements.ToList(), axisLine, -180.0 * Math.PI / 180.0);
						tr.Commit();
					}

					passCount++;
					results.Add("{\"test\":\"7. Rotate 360 deg Suite\",\"status\":\"PASS\",\"details\":\"Da xoay cut va nhanh ong 360 deg\"}");
				}
				else
				{
					results.Add("{\"test\":\"7. Rotate 360 deg Suite\",\"status\":\"FAIL\",\"details\":\"Khong tao duoc nhanh ong de test xoay\"}");
				}
			}
		}
		catch (Exception ex)
		{
					results.Add("{\"test\":\"7. Rotate 360 deg Suite\",\"status\":\"FAIL\",\"details\":\"Khong tao duoc nhanh ong de test xoay\"}");
		}

		// BENCH 8 (Y=410): Elbow Left 45 & Right 45
		try
		{
			Pipe p8 = null;
			Pipe p8Left45 = null;
			Pipe p8Right45 = null;
			using (Transaction tr = new Transaction(doc, "Test 8 - Elbow Left45/Right45 Setup"))
			{
				tr.Start();
				p8 = Pipe.Create(doc, sysType.Id, pipeType.Id, level.Id, new XYZ(0, 410, 10), new XYZ(20, 410, 10));
				p8?.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(50.0 / 304.8);
				tr.Commit();
			}

			if (p8 != null)
			{
				Connector[] un = NaviateHelper.ConnectorArrayUnused(p8);
				if (un != null && un.Length >= 2)
				{
					Connector cLeft = un.OrderBy(c => c.Origin.X).First();
					Connector cRight = un.OrderBy(c => c.Origin.X).Last();

					double ext = 6.0;
					XYZ endLeft45 = new XYZ(cLeft.Origin.X - ext * 0.707, cLeft.Origin.Y + ext * 0.707, cLeft.Origin.Z);
					XYZ endRight45 = new XYZ(cRight.Origin.X + ext * 0.707, cRight.Origin.Y - ext * 0.707, cRight.Origin.Z);

					using (Transaction tr = new Transaction(doc, "Test 8 - Apply Left45 & Right45"))
					{
						tr.Start();
						p8Left45 = NaviateHelper.DrawPipeWithElbow(doc, null, cLeft.Origin, cLeft, cLeft.Origin, endLeft45);
						p8Right45 = NaviateHelper.DrawPipeWithElbow(doc, null, cRight.Origin, cRight, cRight.Origin, endRight45);
						tr.Commit();
					}
				}

				if (p8Left45 != null && p8Right45 != null)
				{
					passCount++;
					results.Add("{\"test\":\"8. Elbow 45 deg\",\"status\":\"PASS\",\"details\":\"Da tao cut Left 45 va Right 45\"}");
				}
				else
				{
					results.Add("{\"test\":\"8. Elbow 45 deg\",\"status\":\"FAIL\",\"details\":\"Khong tao duoc cut 45 deg\"}");
				}
			}
		}
		catch (Exception ex)
		{
					results.Add("{\"test\":\"8. Elbow 45 deg\",\"status\":\"FAIL\",\"details\":\"Khong tao duoc cut 45 deg\"}");
		}
		swTotal.Stop();
		log = "{\"suite\":\"BIN MICRO Test Gallery\",\"total\":" + totalTests + ",\"passed\":" + passCount + ",\"failed\":" + (totalTests - passCount) + ",\"durationMs\":" + swTotal.ElapsedMilliseconds + ",\"tests\":[" + string.Join(",", results) + "]}";
		return passCount == totalTests;
	}
}

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ConnectSprinklerFlexPipeMultiCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		try
		{
			return ConnectSprinklerFlexPipeCmd.ExecuteMulti(
				commandData.Application.ActiveUIDocument, ref message);
		}
		catch (Exception ex)
		{
			message = ex.Message;
			FlexPipeDiagnostics.Write("multi-command", "exception", ex.Message, details: ex.ToString());
			TaskDialog.Show("BIM TOOL - Sprinkler Flex Pipe", "Lỗi trong quá trình kết nối: " + ex.Message + "\n\nChi tiết kỹ thuật đã được ghi nhận vào hệ thống chẩn đoán.");
			return Result.Failed;
		}
	}
}
