using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIN.Common.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class MoveConnectCmd : IExternalCommand
{
	private const double FacingDotThreshold = -0.7;
	private const double RectangularToleranceFeet = 0.03; // Approximately 10 mm.
	private const double RoundToleranceFeet = 0.015; // Approximately 5 mm.

	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;

		try
		{
			ICollection<ElementId> preSelected = uidoc.Selection.GetElementIds();
			bool hasPreSelection = preSelected.Count > 0;
			MepConnectableNewFilter filter = new MepConnectableNewFilter
			{
				PreviousElementID = null
			};

			Reference targetReference = uidoc.Selection.PickObject(
				ObjectType.Element,
				filter,
				"Chọn element MEP đích (giữ nguyên)");
			Element elem1 = doc.GetElement(targetReference);
			filter.PreviousElementID = elem1.Id;

			Reference sourceReference = uidoc.Selection.PickObject(
				ObjectType.Element,
				filter,
				"Chọn element MEP cần di chuyển");
			Element elem2 = doc.GetElement(sourceReference);

			Connector[] targetConnectors = NaviateHelper.ConnectorArrayUnused(elem1);
			if (targetConnectors == null || targetConnectors.Length == 0)
			{
				string diagnosticJson = BuildDiagnosticJson("TARGET_NO_UNUSED_CONNECTORS", elem1, elem2, null);
				return Fail(
					commandData,
					ref message,
					"Phần tử đích không có đầu nối trống",
					$"Phần tử đích '{GetElementName(elem1)}' không còn đầu nối trống để kết nối.",
					"Hãy chọn một FCU/ống/phụ kiện có đầu nối MEP chưa được kết nối, rồi chạy lại Move Connect.",
					diagnosticJson);
			}

			Connector[] sourceConnectors = NaviateHelper.ConnectorArrayUnused(elem2);
			if (sourceConnectors == null || sourceConnectors.Length == 0)
			{
				string diagnosticJson = BuildDiagnosticJson("SOURCE_NO_UNUSED_CONNECTORS", elem1, elem2, null);
				return Fail(
					commandData,
					ref message,
					"Phần tử cần di chuyển không có đầu nối trống",
					$"Phần tử cần di chuyển '{GetElementName(elem2)}' không còn đầu nối trống để kết nối.",
					"Hãy chọn Box gió/ống/phụ kiện có đầu nối MEP chưa được kết nối, rồi chạy lại Move Connect.",
					diagnosticJson);
			}

			Dictionary<Domain, List<Connector>> targetByDomain = GroupByDomain(targetConnectors);
			Dictionary<Domain, List<Connector>> sourceByDomain = GroupByDomain(sourceConnectors);
			List<Domain> sharedDomains = GetSharedDomains(targetByDomain, sourceByDomain);
			if (sharedDomains.Count == 0)
			{
				string diagnosticJson = BuildDiagnosticJson("DOMAIN_MISMATCH", elem1, elem2, null);
				return Fail(
					commandData,
					ref message,
					"Loại hệ MEP của hai đầu nối không khớp",
					$"'{GetElementName(elem1)}' và '{GetElementName(elem2)}' không có đầu nối trống cùng Domain (ví dụ Piping và HVAC không thể kết nối trực tiếp).",
					$"Domain đích: {DescribeDomains(targetByDomain)}\nDomain phần tử di chuyển: {DescribeDomains(sourceByDomain)}",
					diagnosticJson);
			}

			ConnectorCandidate best = FindBestCandidate(targetByDomain, sourceByDomain, sharedDomains);
			if (best == null)
			{
				string diagnosticJson = BuildDiagnosticJson("NO_MATCHING_CONNECTOR_PAIR", elem1, elem2, null);
				return Fail(
					commandData,
					ref message,
					"Không tìm thấy cặp đầu nối phù hợp",
					"Không thể tạo cặp đầu nối trống trong cùng Domain.",
					"Kiểm tra lại các đầu nối MEP của hai phần tử.",
					diagnosticJson);
			}

			if (best.Dot >= FacingDotThreshold)
			{
				double clampedDot = Math.Max(-1.0, Math.Min(1.0, best.Dot));
				double angleDeg = Math.Acos(clampedDot) * 180.0 / Math.PI;
				string diagnosticJson = BuildDiagnosticJson("ORIENTATION_MISMATCH", elem1, elem2, best, angleDeg);
				return Fail(
					commandData,
					ref message,
					$"Đầu nối không đối diện nhau (Góc lệch: {angleDeg:F0}°)",
					$"Đầu nối của '{GetElementName(elem2)}' đang quay cùng hướng hoặc vuông góc với '{GetElementName(elem1)}'.\n\nHướng dẫn xử lý: Vui lòng dùng phím Space hoặc lệnh Rotate Element để xoay Box gió sao cho miệng nối quay đối diện với FCU trước khi bấm Move Connect.",
					BuildCandidateDetails(best),
					diagnosticJson);
			}

			using (Transaction transaction = new Transaction(doc, "BIM TOOL - Move and Connect"))
			{
				transaction.Start();
				try
				{
					XYZ moveVec = best.Target.Origin - best.Source.Origin;
					if (hasPreSelection)
					{
						ElementTransformUtils.MoveElements(doc, preSelected, moveVec);
					}
					else
					{
						ElementTransformUtils.MoveElement(doc, elem2.Id, moveVec);
					}
				}
				catch (Exception ex)
				{
					Rollback(transaction);
					string diagnosticJson = BuildDiagnosticJson("MOVE_FAILED", elem1, elem2, best);
					return Fail(
						commandData,
						ref message,
						"Không thể di chuyển phần tử đến vị trí đầu nối",
						$"Move Connect không thể di chuyển '{GetElementName(elem2)}' đến '{GetElementName(elem1)}'.",
						BuildCandidateDetails(best) + "\n\nLỗi Revit: " + ex.Message,
						diagnosticJson,
						ex);
				}

				try
				{
					best.Source.ConnectTo(best.Target);
				}
				catch (Exception ex)
				{
					Rollback(transaction);
					bool flowDirectionsConflict = best.Target.Direction == best.Source.Direction
						&& best.Target.Direction != FlowDirectionType.Bidirectional;
					string instruction = flowDirectionsConflict
						? "Xung đột hướng dòng chảy (Flow Direction)"
						: "Revit không thể kết nối hai đầu nối đã chọn";
					string content = flowDirectionsConflict
						? $"Xung đột hướng dòng chảy (Flow Direction): Cả hai đầu nối đều là {best.Target.Direction}.\n\nHướng dẫn: Vào Family Editor của '{GetFamilyName(elem2)}', đổi Connector Flow Direction thành 'In' hoặc 'Bidirectional'."
						: $"Revit không thể kết nối đầu nối của '{GetElementName(elem2)}' với '{GetElementName(elem1)}'. Kiểm tra Shape, kích thước và hướng dòng chảy của hai đầu nối.";
					string diagnosticJson = BuildDiagnosticJson("CONNECT_FAILED", elem1, elem2, best);
					return Fail(
						commandData,
						ref message,
						instruction,
						content,
						BuildCandidateDetails(best) + "\n\nLỗi Revit: " + ex.Message,
						diagnosticJson,
						ex);
				}

				TransactionStatus commitStatus = transaction.Commit();
				if (commitStatus != TransactionStatus.Committed)
				{
					string diagnosticJson = BuildDiagnosticJson("COMMIT_FAILED", elem1, elem2, best);
					return Fail(
						commandData,
						ref message,
						"Revit không thể hoàn tất Move Connect",
						"Giao dịch Move Connect đã không được Revit cam kết.",
						BuildCandidateDetails(best),
						diagnosticJson);
				}
			}

			string successJson = BuildDiagnosticJson("CONNECTED", elem1, elem2, best);
			message = successJson;
			EmitCandidateTelemetry("succeeded", successJson);
			// DevCommandProxy emits the one terminal L1 envelope after this command returns.
			CommandDiagnostics.Write("MoveConnect", "detail", commandData, Result.Succeeded, message: successJson);
			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			message = BuildDiagnosticJson("CANCELLED", null, null, null);
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			string diagnosticJson = BuildDiagnosticJson("UNEXPECTED_ERROR", null, null, null);
			return Fail(
				commandData,
				ref message,
				"Move Connect gặp lỗi không mong muốn",
				ex.Message,
				ex.ToString(),
				diagnosticJson,
				ex);
		}
	}

	private static ConnectorCandidate FindBestCandidate(
		Dictionary<Domain, List<Connector>> targetByDomain,
		Dictionary<Domain, List<Connector>> sourceByDomain,
		ICollection<Domain> sharedDomains)
	{
		ConnectorCandidate best = null;
		foreach (Domain domain in sharedDomains)
		{
			foreach (Connector target in targetByDomain[domain])
			{
				foreach (Connector source in sourceByDomain[domain])
				{
					ConnectorCandidate candidate = ScoreCandidate(target, source);
					if (best == null || candidate.Score > best.Score || (candidate.Score == best.Score && candidate.Distance < best.Distance))
					{
						best = candidate;
					}
				}
			}
		}
		return best;
	}

	private static ConnectorCandidate ScoreCandidate(Connector target, Connector source)
	{
		double distance = target.Origin.DistanceTo(source.Origin);
		double dot = target.CoordinateSystem.BasisZ.DotProduct(source.CoordinateSystem.BasisZ);
		bool shapesMatch = target.Shape == source.Shape;
		bool dimensionsMatch = DimensionsMatch(target, source);
		bool flowCompatible = target.Direction != source.Direction
			|| target.Direction == FlowDirectionType.Bidirectional
			|| source.Direction == FlowDirectionType.Bidirectional;

		int score = 0;
		if (dot < FacingDotThreshold)
		{
			score += 2000;
		}
		else
		{
			score += (int)((-dot) * 500);
		}
		if (shapesMatch)
		{
			score += 500;
		}
		if (dimensionsMatch)
		{
			score += 500;
		}
		if (flowCompatible)
		{
			score += 300;
		}
		score -= (int)(distance * 10);

		return new ConnectorCandidate(target, source, distance, dot, shapesMatch, dimensionsMatch, flowCompatible, score);
	}

	private static bool DimensionsMatch(Connector target, Connector source)
	{
		if (target.Shape != source.Shape)
		{
			return false;
		}

		try
		{
			if (target.Shape == ConnectorProfileType.Rectangular)
			{
				bool directMatch = ApproximatelyEqual(target.Width, source.Width, RectangularToleranceFeet)
					&& ApproximatelyEqual(target.Height, source.Height, RectangularToleranceFeet);
				bool swappedMatch = ApproximatelyEqual(target.Width, source.Height, RectangularToleranceFeet)
					&& ApproximatelyEqual(target.Height, source.Width, RectangularToleranceFeet);
				return directMatch || swappedMatch;
			}
			if (target.Shape == ConnectorProfileType.Round)
			{
				return ApproximatelyEqual(target.Radius, source.Radius, RoundToleranceFeet);
			}
		}
		catch
		{
			return false;
		}

		return false;
	}

	private static bool ApproximatelyEqual(double first, double second, double tolerance)
	{
		return Math.Abs(first - second) <= tolerance;
	}

	private static Dictionary<Domain, List<Connector>> GroupByDomain(IEnumerable<Connector> connectors)
	{
		Dictionary<Domain, List<Connector>> result = new Dictionary<Domain, List<Connector>>();
		foreach (Connector connector in connectors)
		{
			if (!result.TryGetValue(connector.Domain, out List<Connector> group))
			{
				group = new List<Connector>();
				result.Add(connector.Domain, group);
			}
			group.Add(connector);
		}
		return result;
	}

	private static List<Domain> GetSharedDomains(
		Dictionary<Domain, List<Connector>> targetByDomain,
		Dictionary<Domain, List<Connector>> sourceByDomain)
	{
		List<Domain> result = new List<Domain>();
		foreach (Domain domain in targetByDomain.Keys)
		{
			if (sourceByDomain.ContainsKey(domain))
			{
				result.Add(domain);
			}
		}
		return result;
	}

	private static string DescribeDomains(Dictionary<Domain, List<Connector>> connectorsByDomain)
	{
		List<string> descriptions = new List<string>();
		foreach (KeyValuePair<Domain, List<Connector>> entry in connectorsByDomain)
		{
			descriptions.Add(entry.Key + " (" + entry.Value.Count + ")");
		}
		return descriptions.Count == 0 ? "Không có" : string.Join(", ", descriptions);
	}

	private static Result Fail(
		ExternalCommandData commandData,
		ref string message,
		string mainInstruction,
		string mainContent,
		string expandedContent,
		string diagnosticJson,
		Exception ex = null)
	{
		message = diagnosticJson;
		TaskDialog dialog = new TaskDialog("Move Connect")
		{
			MainInstruction = mainInstruction,
			MainContent = mainContent,
			ExpandedContent = expandedContent
		};
		dialog.Show();
		EmitCandidateTelemetry("failed", diagnosticJson);
		// DevCommandProxy emits the one terminal L1 envelope after this command returns.
		CommandDiagnostics.Write("MoveConnect", "detail_failed", commandData, Result.Failed, message: diagnosticJson, error: ex);
		return Result.Failed;
	}

	private static void EmitCandidateTelemetry(string outcome, string diagnosticJson)
	{
		try
		{
			JObject details = JObject.Parse(diagnosticJson);
			string reasonCode = (string)details["reason"] ?? "UNEXPECTED_ERROR";
			JObject target = details["target"] as JObject;
			JObject source = details["source"] as JObject;
			if (target != null) target.Remove("name");
			if (source != null) source.Remove("name");
			CommandDiagnostics.WriteL2Json("MoveConnect", outcome, reasonCode, details.ToString(Formatting.None));
		}
		catch { }
	}

	private static void Rollback(Transaction transaction)
	{
		try
		{
			transaction.RollBack();
		}
		catch
		{
			// Preserve the original operation error for the user and telemetry.
		}
	}

	private static string BuildDiagnosticJson(string reason, Element target, Element source, ConnectorCandidate candidate, double? angleDeg = null)
	{
		string json = "{"
			+ "\"reason\":\"" + CommandDiagnostics.Escape(reason) + "\""
			+ ",\"target\":{\"id\":\"" + CommandDiagnostics.Escape(GetElementId(target)) + "\",\"name\":\"" + CommandDiagnostics.Escape(GetElementName(target)) + "\"}"
			+ ",\"source\":{\"id\":\"" + CommandDiagnostics.Escape(GetElementId(source)) + "\",\"name\":\"" + CommandDiagnostics.Escape(GetElementName(source)) + "\"}";

		if (candidate != null)
		{
			json += ",\"candidate\":{\"domain\":\"" + CommandDiagnostics.Escape(candidate.Target.Domain.ToString())
				+ "\",\"distanceFt\":" + FormatDouble(candidate.Distance)
				+ ",\"dot\":" + FormatDouble(candidate.Dot)
				+ ",\"score\":" + candidate.Score.ToString(CultureInfo.InvariantCulture)
				+ ",\"shapeMatch\":" + candidate.ShapesMatch.ToString().ToLowerInvariant()
				+ ",\"dimensionMatch\":" + candidate.DimensionsMatch.ToString().ToLowerInvariant()
				+ ",\"flowCompatible\":" + candidate.FlowCompatible.ToString().ToLowerInvariant()
				+ "}";
		}
		if (angleDeg.HasValue)
		{
			json += ",\"angleDeg\":" + FormatDouble(angleDeg.Value);
		}
		return json + "}";
	}

	private static string BuildCandidateDetails(ConnectorCandidate candidate)
	{
		return "Target connector:\n"
			+ DescribeConnector(candidate.Target)
			+ "\n\nMoving connector:\n"
			+ DescribeConnector(candidate.Source)
			+ "\n\nScore=" + candidate.Score.ToString(CultureInfo.InvariantCulture)
			+ "; Distance=" + FormatDouble(candidate.Distance) + " ft"
			+ "; Dot=" + FormatDouble(candidate.Dot)
			+ "; ShapeMatch=" + candidate.ShapesMatch
			+ "; DimensionMatch=" + candidate.DimensionsMatch
			+ "; FlowCompatible=" + candidate.FlowCompatible;
	}

	private static string DescribeConnector(Connector connector)
	{
		return "Origin=" + FormatXyz(connector.Origin)
			+ "; BasisZ=" + FormatXyz(connector.CoordinateSystem.BasisZ)
			+ "; Shape=" + connector.Shape
			+ "; Size=" + GetConnectorSize(connector)
			+ "; Domain=" + connector.Domain
			+ "; Direction=" + connector.Direction;
	}

	private static string GetConnectorSize(Connector connector)
	{
		try
		{
			if (connector.Shape == ConnectorProfileType.Rectangular)
			{
				return FormatDouble(connector.Width) + " x " + FormatDouble(connector.Height) + " ft";
			}
			if (connector.Shape == ConnectorProfileType.Round)
			{
				return "R=" + FormatDouble(connector.Radius) + " ft";
			}
		}
		catch
		{
			return "unavailable";
		}
		return "n/a";
	}

	private static string FormatXyz(XYZ point)
	{
		return "(" + FormatDouble(point.X) + ", " + FormatDouble(point.Y) + ", " + FormatDouble(point.Z) + ")";
	}

	private static string FormatDouble(double value)
	{
		return value.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static string GetElementId(Element element)
	{
		return element == null ? "" : ElementIdHelper.GetIdValue(element.Id).ToString(CultureInfo.InvariantCulture);
	}

	private static string GetElementName(Element element)
	{
		if (element == null)
		{
			return "Unknown";
		}
		if (!string.IsNullOrWhiteSpace(element.Name))
		{
			return element.Name;
		}
		return element.Category?.Name ?? element.GetType().Name;
	}

	private static string GetFamilyName(Element element)
	{
		FamilyInstance familyInstance = element as FamilyInstance;
		if (familyInstance?.Symbol?.Family != null && !string.IsNullOrWhiteSpace(familyInstance.Symbol.Family.Name))
		{
			return familyInstance.Symbol.Family.Name;
		}
		return GetElementName(element);
	}

	private sealed class ConnectorCandidate
	{
		public ConnectorCandidate(
			Connector target,
			Connector source,
			double distance,
			double dot,
			bool shapesMatch,
			bool dimensionsMatch,
			bool flowCompatible,
			int score)
		{
			Target = target;
			Source = source;
			Distance = distance;
			Dot = dot;
			ShapesMatch = shapesMatch;
			DimensionsMatch = dimensionsMatch;
			FlowCompatible = flowCompatible;
			Score = score;
		}

		public Connector Target { get; }
		public Connector Source { get; }
		public double Distance { get; }
		public double Dot { get; }
		public bool ShapesMatch { get; }
		public bool DimensionsMatch { get; }
		public bool FlowCompatible { get; }
		public int Score { get; }
	}
}
