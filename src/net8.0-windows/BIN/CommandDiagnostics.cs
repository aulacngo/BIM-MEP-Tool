using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIN;

public static class CommandDiagnostics
{
	private static readonly object Sync = new object();
	private static readonly string DirectoryPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIM TOOL", "Diagnostics");
	private static readonly string LegacyDirectoryPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIN TOOL", "Diagnostics");
	private static readonly string LogPath = Path.Combine(DirectoryPath, "commands.jsonl");
	private static readonly string LegacyLogPath = Path.Combine(LegacyDirectoryPath, "commands.jsonl");
	private static readonly string SessionId = Guid.NewGuid().ToString("N");
	private static long clientSequence;

	private static string GetReadableLogPath()
	{
		return File.Exists(LogPath) || !File.Exists(LegacyLogPath) ? LogPath : LegacyLogPath;
	}

	public static void Write(string command, string stage, ExternalCommandData data, Result? result = null,
		string message = null, Exception error = null, string assemblyPath = null, long durationMilliseconds = -1)
	{
		try
		{
			// Snapshot only bounded primitive data while on the Revit thread.
			string localTime = DateTime.Now.ToString("O");
			string occurredAtUtc = DateTime.UtcNow.ToString("O");
			UIDocument uidoc = data == null || data.Application == null ? null : data.Application.ActiveUIDocument;
			Document doc = uidoc == null ? null : uidoc.Document;
			ICollection<ElementId> selectedIds = uidoc == null ? null : uidoc.Selection.GetElementIds();
			List<long> selectedCategoryIds = new List<long>();
			List<long> selectedElementIds = new List<long>();
			if (selectedIds != null)
			{
				foreach (ElementId selectedId in selectedIds.Take(8))
				{
					selectedElementIds.Add(GetElementIdValue(selectedId));
					Element selected = doc == null ? null : doc.GetElement(selectedId);
					if (selected != null && selected.Category != null) selectedCategoryIds.Add(GetElementIdValue(selected.Category.Id));
				}
			}
			string ids = string.Join(",", selectedElementIds);
			string resultText = result.HasValue ? result.Value.ToString() : "";
			string documentTitle = doc == null ? null : doc.Title;
			View activeView = doc == null ? null : doc.ActiveView;
			string viewName = activeView == null ? "" : activeView.Name;
			string viewType = activeView == null ? "Unknown" : activeView.ViewType.ToString();
			long activeLevelId = 0;
			try { if (activeView != null && activeView.GenLevel != null) activeLevelId = GetElementIdValue(activeView.GenLevel.Id); } catch { }
			string exceptionType = error == null ? null : error.GetType().FullName;
			string exceptionMessage = error == null ? "" : error.Message;
			string stackTrace = error == null ? null : error.ToString();
			string userName = doc == null || doc.Application == null ? Environment.UserName : doc.Application.Username;
			int revitMajor = TryGetRevitMajor(doc);

			QueueLocalDiagnostic(localTime, command, stage, resultText, documentTitle, viewName, ids,
				message, assemblyPath, exceptionType, exceptionMessage, stackTrace);

			if (string.Equals(stage, "completed", StringComparison.OrdinalIgnoreCase) ||
				string.Equals(stage, "failed", StringComparison.OrdinalIgnoreCase))
			{
				QueueTerminalTelemetry(command, result, occurredAtUtc, documentTitle, userName, revitMajor,
					viewType, selectedIds == null ? 0 : selectedIds.Count, selectedCategoryIds,
					"sha256:" + Hash(string.Join(",", selectedElementIds)), activeLevelId,
					message, exceptionMessage, durationMilliseconds);
			}
		}
		catch
		{
			// Diagnostics must never change a Revit command result.
		}
	}

	/// <summary>Queues a structured L2 detail event containing primitive-only data.</summary>
	public static void WriteL2(string toolId, string outcome, string reasonCode, object details)
	{
		try
		{
			WriteL2Json(toolId, outcome, reasonCode, JsonConvert.SerializeObject(details ?? new { }, Formatting.None));
		}
		catch { }
	}

	/// <summary>Queues a pre-serialized L2 detail event; no Revit object may be supplied.</summary>
	public static void WriteL2Json(string toolId, string outcome, string reasonCode, string detailsJson)
	{
		try
		{
			string safeDetails = string.IsNullOrWhiteSpace(detailsJson) ? "{}" : detailsJson;
			string json = "{"
				+ "\"schema_version\":\"1.0\","
				+ "\"event_id\":\"" + Guid.NewGuid().ToString("D") + "\","
				+ "\"event_type\":\"telemetry.detail\","
				+ "\"tier\":\"L2\","
				+ "\"occurred_at_utc\":\"" + DateTime.UtcNow.ToString("O") + "\","
				+ "\"client\":{\"session_id\":\"" + SessionId + "\",\"sequence\":" + Interlocked.Increment(ref clientSequence) + "},"
				+ "\"command\":{\"tool_id\":\"" + Escape(NormalizeToolId(toolId)) + "\",\"stage\":\"detail\",\"outcome\":\"" + Escape(outcome) + "\",\"reason_code\":\"" + Escape(reasonCode) + "\"},"
				+ "\"details\":" + safeDetails
				+ "}";
			TelemetryBatchDispatcher.TryEnqueue(json);
		}
		catch { }
	}

	private static void QueueLocalDiagnostic(string time, string command, string stage, string resultText,
		string documentTitle, string viewName, string ids, string message, string assemblyPath,
		string exceptionType, string exceptionMessage, string stackTrace)
	{
		try
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					string json = "{"
						+ "\"time\":\"" + Escape(time) + "\","
						+ "\"command\":\"" + Escape(command) + "\","
						+ "\"stage\":\"" + Escape(stage) + "\","
						+ "\"result\":\"" + Escape(resultText) + "\","
						+ "\"document\":\"" + Escape(documentTitle) + "\","
						+ "\"view\":\"" + Escape(viewName) + "\","
						+ "\"selectionIds\":[" + ids + "],"
						+ "\"message\":\"" + Escape(message) + "\","
						+ "\"assembly\":\"" + Escape(assemblyPath) + "\","
						+ "\"exceptionType\":\"" + Escape(exceptionType) + "\","
						+ "\"exception\":\"" + Escape(exceptionMessage) + "\","
						+ "\"stackTrace\":\"" + Escape(stackTrace) + "\""
						+ "}";
					lock (Sync)
					{
						Directory.CreateDirectory(DirectoryPath);
						File.AppendAllText(LogPath, json + Environment.NewLine, Encoding.UTF8);
					}
				}
				catch { }
			});
		}
		catch { }
	}

	private static void QueueTerminalTelemetry(string command, Result? result, string occurredAtUtc,
		string projectName, string userName, int revitMajor, string viewType, int selectionCount,
		List<long> selectedCategoryIds, string selectionSignature, long activeLevelId, string message, string exceptionMessage,
		long durationMilliseconds)
	{
		try
		{
			string outcome = NormalizeOutcome(result);
			string reasonCode = ExtractReasonCode(message, outcome, exceptionMessage);
			long sequence = Interlocked.Increment(ref clientSequence);
			long? duration = durationMilliseconds >= 0 ? durationMilliseconds : (long?)null;
			string json = JsonConvert.SerializeObject(new
			{
				schema_version = "1.0",
				event_id = Guid.NewGuid().ToString("D"),
				event_type = "command.terminal",
				tier = "L1",
				occurred_at_utc = occurredAtUtc,
				client = new
				{
					install_id_hash = "sha256:" + Hash(Environment.MachineName + "|" + Environment.UserName),
					session_id = SessionId,
					sequence,
					revit_major = revitMajor,
					runtime = RuntimeName,
					add_in_version = typeof(CommandDiagnostics).Assembly.GetName().Version == null
						? "unknown" : typeof(CommandDiagnostics).Assembly.GetName().Version.ToString()
				},
				project = new
				{
					project_id_hash = "sha256:" + Hash(projectName),
					model_revision = "unavailable",
					view_type = viewType
				},
				command = new
				{
					tool_id = NormalizeToolId(command),
					stage = "terminal",
					outcome,
					reason_code = reasonCode,
					duration_ms = duration
				},
				context = new
				{
					selection_count = selectionCount,
					selected_category_ids = selectedCategoryIds,
					selection_signature = selectionSignature,
					active_level_id = activeLevelId == 0 ? (long?)null : activeLevelId
				},
				quality = new
				{
					sampled = false,
					dropped_optional_fields = new string[0],
					client_drop_count_since_start = TelemetryBatchDispatcher.DroppedCount
				}
			}, Formatting.None);
			TelemetryBatchDispatcher.TryEnqueue(json);
		}
		catch { }
	}

	private static string RuntimeName
	{
		get
		{
#if NET8_0_OR_GREATER
			return "net8.0-windows";
#else
			return "net48";
#endif
		}
	}

	private static int TryGetRevitMajor(Document doc)
	{
		int major;
		return doc != null && doc.Application != null && int.TryParse(doc.Application.VersionNumber, out major) ? major : 0;
	}

	private static string NormalizeOutcome(Result? result)
	{
		if (result == Result.Succeeded) return "succeeded";
		if (result == Result.Failed) return "failed";
		if (result == Result.Cancelled) return "cancelled";
		return "unknown";
	}

	private static string ExtractReasonCode(string message, string outcome, string exceptionMessage)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(message) && message.TrimStart().StartsWith("{"))
			{
				JObject diagnostic = JObject.Parse(message);
				string value = (string)diagnostic["reason_code"] ?? (string)diagnostic["reason"];
				if (!string.IsNullOrWhiteSpace(value)) return value.Trim().ToUpperInvariant();
			}
		}
		catch { }
		if (outcome == "succeeded") return "COMPLETED";
		if (outcome == "cancelled") return "CANCELLED";
		return string.IsNullOrWhiteSpace(exceptionMessage) ? "FAILED" : "UNEXPECTED_ERROR";
	}

	private static string NormalizeToolId(string command)
	{
		switch (command ?? "")
		{
			case "PlaceFamily": return "cad.place_family";
			case "MoveConnect": return "mep.move_connect";
			case "ConnectSprinklerFlexPipe": return "mep.sprinkler_flex_pipe";
			case "ConnectSprinklerFlexMulti": return "mep.sprinkler_flex_multi";
			default: return "bim." + (string.IsNullOrWhiteSpace(command) ? "unknown" : command.Trim().ToLowerInvariant());
		}
	}

	private static string Hash(string value)
	{
		try
		{
			using (SHA256 sha256 = SHA256.Create())
			{
				byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
				StringBuilder text = new StringBuilder(bytes.Length * 2);
				foreach (byte valueByte in bytes) text.Append(valueByte.ToString("x2"));
				return text.ToString();
			}
		}
		catch { return "unavailable"; }
	}

	public static string ReadLatestJson(int count)
	{
		count = Math.Max(1, Math.Min(count, 1000));
		try
		{
			lock (Sync)
			{
				string readableLogPath = GetReadableLogPath();
				if (!File.Exists(readableLogPath)) return "{\"count\":0,\"events\":[]}";
				string[] lines = File.ReadAllLines(readableLogPath, Encoding.UTF8)
					.Where(line => !string.IsNullOrWhiteSpace(line)).Reverse().Take(count).Reverse().ToArray();
				return "{\"count\":" + lines.Length + ",\"logPath\":\"" + Escape(readableLogPath) + "\",\"events\":[" + string.Join(",", lines) + "]}";
			}
		}
		catch (Exception ex) { return "{\"error\":\"" + Escape(ex.Message) + "\"}"; }
	}

	public static string ReadLastErrorJson()
	{
		try
		{
			lock (Sync)
			{
				string readableLogPath = GetReadableLogPath();
				if (!File.Exists(readableLogPath)) return "{\"error\":null}";
				string line = File.ReadLines(readableLogPath, Encoding.UTF8).Reverse().FirstOrDefault(value => value.Contains("\"stage\":\"failed\""));
				return line == null ? "{\"error\":null}" : "{\"error\":" + line + "}";
			}
		}
		catch (Exception ex) { return "{\"error\":\"" + Escape(ex.Message) + "\"}"; }
	}

	public static string ReadLatestJournalJson(int lineCount)
	{
		lineCount = Math.Max(1, Math.Min(lineCount, 2000));
		try
		{
			string revitRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Autodesk", "Revit");
			FileInfo journal = new DirectoryInfo(revitRoot).Exists
				? new DirectoryInfo(revitRoot).GetFiles("journal.*.txt", SearchOption.AllDirectories).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault()
				: null;
			if (journal == null) return "{\"error\":\"No Revit journal found\"}";
			string[] lines = File.ReadAllLines(journal.FullName).Reverse().Take(lineCount).Reverse().ToArray();
			return "{\"path\":\"" + Escape(journal.FullName) + "\",\"lineCount\":" + lines.Length + ",\"text\":\"" + Escape(string.Join(Environment.NewLine, lines)) + "\"}";
		}
		catch (Exception ex) { return "{\"error\":\"" + Escape(ex.Message) + "\"}"; }
	}

	private static long GetElementIdValue(ElementId id)
	{
#if NET8_0_OR_GREATER
		return id.Value;
#else
		return id.IntegerValue;
#endif
	}

	public static string Escape(string value)
	{
		if (value == null) return "";
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
	}
}
