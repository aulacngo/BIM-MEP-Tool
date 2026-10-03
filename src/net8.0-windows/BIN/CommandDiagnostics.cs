using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
#if TELEMETRY_TEST
	private static string DirectoryPath => Path.Combine(Path.GetTempPath(), "BIM-Telemetry-Tests-" + System.Diagnostics.Process.GetCurrentProcess().Id);
#else
	private static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIM TOOL", "Diagnostics");
#endif
	private static string LogPath => Path.Combine(DirectoryPath, "commands.jsonl");
	private static string LegacyLogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIN TOOL", "Diagnostics", "commands.jsonl");
	// Named thread data shares primitive context with Assembly.Load(byte[]) hot-loaded copies.
	private static readonly LocalDataStoreSlot CommandSlot = Thread.GetNamedDataSlot("BIM.Tool.Telemetry.Invocation.V2");
	private static string SessionId => TelemetryBatchDispatcher.SessionId;
	internal static string[] CurrentInvocation => Thread.GetData(CommandSlot) as string[];

	public static IDisposable BeginCommand(string toolId)
	{
		return new InvocationScope(toolId);
	}

	private sealed class InvocationScope : IDisposable
	{
		private readonly string[] previous;
		internal InvocationScope(string toolId)
		{
			previous = CurrentInvocation;
			Thread.SetData(CommandSlot, new[] { Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), Limit(toolId, 128), "unobserved" });
		}
		public void Dispose() { Thread.SetData(CommandSlot, previous); }
	}

	internal static void ObserveTransaction(string state)
	{
		string[] current = CurrentInvocation;
		if (current != null) current[3] = state;
	}

	public static void Write(string command, string stage, ExternalCommandData data, Result? result = null,
		string message = null, Exception error = null, string assemblyPath = null, long durationMilliseconds = -1)
	{
		try
		{
			var snapshot = new CommandSnapshot();
			snapshot.Tool = Limit(command, 128);
			snapshot.Stage = Limit(stage, 64);
			snapshot.Outcome = NormalizeOutcome(result);
			snapshot.Message = Limit(message, 2048);
			snapshot.AssemblyPath = Limit(assemblyPath, 512);
			snapshot.Duration = durationMilliseconds;
			snapshot.Terminal = string.Equals(stage, "completed", StringComparison.OrdinalIgnoreCase) || string.Equals(stage, "failed", StringComparison.OrdinalIgnoreCase);
			string[] current = CurrentInvocation;
			snapshot.Correlation = current == null ? null : current[0];
			snapshot.ParentEventId = current == null ? null : current[1];
			snapshot.TransactionState = current == null ? "unobserved" : current[3];
			if (snapshot.Terminal && current != null) snapshot.EventId = current[1];
			// Error-only primitive metadata. Do not retain Exception/Data/TargetSite objects.
			if (error != null)
			{
				snapshot.ExceptionType = Limit(error.GetType().FullName, 256);
				snapshot.ExceptionMessage = Limit(error.Message, 512);
				MethodBase site = error.TargetSite;
				snapshot.Callsite = Limit(site == null ? null : (site.DeclaringType?.FullName + "." + site.Name), 512);
				// StackTrace materialization is confined to error capture, never the normal hook.
				snapshot.Stack = Limit(error.StackTrace, 2048);
			}
			try
			{
				UIDocument uidoc = data?.Application?.ActiveUIDocument;
				Document doc = uidoc?.Document;
				if (doc != null)
				{
					snapshot.Project = Limit(doc.Title, 256);
					int.TryParse(doc.Application.VersionNumber, out snapshot.RevitMajor);
					View view = doc.ActiveView;
					snapshot.View = Limit(view?.Name, 128);
					snapshot.ViewType = view == null ? "Unknown" : view.ViewType.ToString();
					// Only IDs here. Category sampling belongs to DocumentChanged's bounded capture.
					ICollection<ElementId> ids = uidoc.Selection.GetElementIds();
					snapshot.SelectionCount = ids.Count;
					int i = 0;
					foreach (ElementId id in ids)
					{
						if (i == 8) break;
						snapshot.SelectedIds[i++] = GetElementIdValue(id);
					}
					snapshot.SelectedSampleCount = i;
				}
			}
			catch { snapshot.ContextIncomplete = true; }
			TelemetryBatchDispatcher.TryEnqueue(snapshot);
			if (error != null)
			{
				// Independently admitted L2 event; both events have the same invocation/parent.
				WriteL2(command, "failed", "EXCEPTION", new
				{
					exception_type = snapshot.ExceptionType, exception_message = snapshot.ExceptionMessage,
					revit_callsite = snapshot.Callsite, stack_trace = snapshot.Stack,
					capture_stage = snapshot.Stage
				});
			}
		}
		catch { TelemetryBatchDispatcher.Drop(); }
	}

	/// <summary>Accepts a flat anonymous object of primitive values. Copies, caps and freezes it.</summary>
	public static void WriteL2(string toolId, string outcome, string reasonCode, object details)
	{
		try
		{
			var snapshot = DetailSnapshot.Create(toolId, outcome, reasonCode);
			snapshot.Fields = PrimitiveField.Capture(details, out snapshot.Truncated);
			TelemetryBatchDispatcher.TryEnqueue(snapshot);
		}
		catch { TelemetryBatchDispatcher.Drop(); }
	}

	public static void WriteL2Json(string toolId, string outcome, string reasonCode, string detailsJson)
	{
		try
		{
			var snapshot = DetailSnapshot.Create(toolId, outcome, reasonCode);
			// Never truncate JSON into malformed syntax. Mark oversized details explicitly.
			if (detailsJson != null && detailsJson.Length > 4096) snapshot.Truncated = true;
			else snapshot.DetailsJson = detailsJson;
			TelemetryBatchDispatcher.TryEnqueue(snapshot);
		}
		catch { TelemetryBatchDispatcher.Drop(); }
	}

	internal abstract class EventSnapshot : TelemetryWorkItem
	{
		internal readonly DateTime Occurred = DateTime.UtcNow;
		internal readonly long Sequence = TelemetryBatchDispatcher.NextSequence();
		internal string EventId = Guid.NewGuid().ToString("D");
		internal string Correlation, ParentEventId, Tool, Project;
		internal int CommonBytes => 512 + StringBytes(EventId) + StringBytes(Correlation) + StringBytes(ParentEventId) + StringBytes(Tool) + StringBytes(Project);
		internal JObject Envelope(string tier, string type)
		{
			return new JObject
			{
				["schema_version"] = "1.0", ["event_id"] = EventId, ["event_type"] = type,
				["tier"] = tier, ["occurred_at_utc"] = Occurred.ToString("O"),
				["correlation_id"] = Correlation, ["parent_event_id"] = tier == "L2" ? ParentEventId : null,
				["client"] = new JObject { ["session_id"] = SessionId, ["sequence"] = Sequence,
					["install_id_hash"] = WorkerIdentity.InstallHash, ["runtime"] = RuntimeName },
				["project"] = new JObject { ["project_id_hash"] = "sha256:" + Hash(Project) },
				["quality"] = new JObject { ["client_drop_count_since_start"] = TelemetryBatchDispatcher.DroppedCount,
					["unacknowledged_delivery_count_since_start"] = TelemetryBatchDispatcher.FailedDeliveryCount,
					["correlation_status"] = Correlation == null ? "unscoped" : "scoped" }
			};
		}
	}

	private static class WorkerIdentity
	{
		internal static readonly string InstallHash = "sha256:" + Hash(Environment.MachineName + "|" + Environment.UserName);
	}

	private sealed class CommandSnapshot : EventSnapshot
	{
		internal string Stage, Outcome, Message, AssemblyPath, ExceptionType, ExceptionMessage, Callsite, Stack;
		internal string View, ViewType, TransactionState;
		internal long Duration;
		internal int RevitMajor, SelectionCount, SelectedSampleCount;
		internal readonly long[] SelectedIds = new long[8];
		internal bool Terminal, ContextIncomplete;
		internal override int RetainedBytes => CommonBytes + 512 + StringBytes(Stage) + StringBytes(Outcome) + StringBytes(Message)
			+ StringBytes(AssemblyPath) + StringBytes(ExceptionType) + StringBytes(ExceptionMessage) + StringBytes(Callsite)
			+ StringBytes(Stack) + StringBytes(View) + StringBytes(ViewType) + StringBytes(TransactionState);
		internal override string SerializeOnWorker()
		{
			var ids = new JArray(SelectedIds.Take(SelectedSampleCount));
			var local = new JObject { ["time"] = Occurred.ToLocalTime().ToString("O"), ["command"] = Tool,
				["stage"] = Stage, ["result"] = Outcome, ["document"] = Project, ["view"] = View,
				["selectionIds"] = ids, ["message"] = Message, ["assembly"] = AssemblyPath,
				["exceptionType"] = ExceptionType, ["exception"] = ExceptionMessage, ["stackTrace"] = Stack,
				["revit_callsite"] = Callsite, ["correlation_id"] = Correlation, ["event_id"] = EventId };
			try
			{
				lock (Sync)
				{
					Directory.CreateDirectory(DirectoryPath);
					if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 4 * 1024 * 1024)
					{
						string previous = LogPath + ".1";
						if (File.Exists(previous)) File.Delete(previous);
						File.Move(LogPath, previous);
					}
					File.AppendAllText(LogPath, local.ToString(Formatting.None) + Environment.NewLine, Encoding.UTF8);
				}
			}
			catch { /* A local disk failure must not suppress network diagnostics. */ }
			if (!Terminal) return null;
			JObject json = Envelope("L1", "command.terminal");
			json["client"]["revit_major"] = RevitMajor;
			json["project"]["view_type"] = ViewType;
			json["command"] = new JObject { ["tool_id"] = NormalizeToolId(Tool), ["stage"] = "terminal",
				["outcome"] = Outcome, ["reason_code"] = ExtractReasonCode(Message, Outcome, ExceptionMessage),
				["duration_ms"] = Duration < 0 ? JValue.CreateNull() : new JValue(Duration) };
			json["context"] = new JObject { ["selection_count"] = SelectionCount,
				["selected_element_ids"] = ids, ["selection_signature"] = "sha256:" + Hash(string.Join(",", SelectedIds.Take(SelectedSampleCount))),
				["origin"] = Correlation == null ? "unknown" : "bin_tool", ["transaction_state"] = TransactionState,
				["transaction_state_evidence"] = "last_document_changed_in_command_scope",
				["exception_type"] = ExceptionType, ["revit_callsite"] = Callsite };
			json["quality"]["context_incomplete"] = ContextIncomplete;
			json["quality"]["selection_sampled"] = SelectionCount > SelectedSampleCount;
			return json.ToString(Formatting.None);
		}
	}

	private sealed class DetailSnapshot : EventSnapshot
	{
		internal string Outcome, Reason, DetailsJson;
		internal PrimitiveField[] Fields;
		internal bool Truncated;
		internal static DetailSnapshot Create(string tool, string outcome, string reason)
		{
			string[] current = CurrentInvocation;
			return new DetailSnapshot { Tool = Limit(tool, 128), Outcome = Limit(outcome, 32), Reason = Limit(reason, 128),
				Correlation = current == null ? null : current[0], ParentEventId = current == null ? null : current[1] };
		}
		// Capture has <=32 fields, <=4096 total string chars including keys; conservative reservation.
		internal override int RetainedBytes => CommonBytes + 12288 + StringBytes(DetailsJson);
		internal override string SerializeOnWorker()
		{
			JObject details = new JObject();
			if (!string.IsNullOrWhiteSpace(DetailsJson))
			{
				try { details = JObject.Parse(DetailsJson); }
				catch { details["parse_error"] = true; }
			}
			if (Fields != null) foreach (PrimitiveField field in Fields) details[field.Name] = field.Value == null ? JValue.CreateNull() : new JValue(field.Value);
			if (Tool == "MoveConnect")
			{
				(details["target"] as JObject)?.Remove("name");
				(details["source"] as JObject)?.Remove("name");
				if (string.IsNullOrWhiteSpace(Reason)) Reason = Limit((string)details["reason"], 128);
			}
			JObject json = Envelope("L2", "telemetry.detail");
			json["command"] = new JObject { ["tool_id"] = NormalizeToolId(Tool), ["stage"] = "detail", ["outcome"] = Outcome, ["reason_code"] = Reason };
			json["details"] = details;
			json["quality"]["details_truncated_or_unsupported"] = Truncated;
			return json.ToString(Formatting.None);
		}
	}

	private sealed class PrimitiveField
	{
		internal string Name;
		internal object Value;
		internal static PrimitiveField[] Capture(object source, out bool truncated)
		{
			truncated = false;
			if (source == null) return new PrimitiveField[0];
			Type type = source.GetType();
			// Inspect only compiler generated anonymous fields: no arbitrary getters, iterators,
			// closures, Revit API objects, or caller-owned collections can escape to the worker.
			if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) || !type.Name.Contains("AnonymousType"))
			{ truncated = true; return new PrimitiveField[0]; }
			var fields = new List<PrimitiveField>(32);
			int remaining = 4096;
			foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
			{
				if (fields.Count == 32 || remaining <= 0) { truncated = true; break; }
				string name = field.Name;
				int end = name.IndexOf('>');
				if (name.StartsWith("<") && end > 0) name = name.Substring(1, end - 1);
				name = Limit(name, Math.Min(64, remaining));
				remaining -= name.Length;
				object value = field.GetValue(source);
				if (value is string text)
				{
					string limited = Limit(text, Math.Min(2048, remaining));
					truncated |= text.Length != limited.Length;
					remaining -= limited.Length;
					value = limited;
				}
				else if (value != null && !(value is bool || value is byte || value is short || value is int || value is long || value is float || value is double || value is decimal))
				{ truncated = true; value = null; }
				fields.Add(new PrimitiveField { Name = name, Value = value });
			}
			return fields.ToArray();
		}
	}

	internal static string Limit(string value, int length) => value == null ? "" : value.Length <= length ? value : value.Substring(0, length);
	internal static string Hash(string value)
	{
		using (SHA256 sha = SHA256.Create())
		{
			byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
			var text = new StringBuilder(bytes.Length * 2);
			foreach (byte b in bytes) text.Append(b.ToString("x2"));
			return text.ToString();
		}
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
	private static string NormalizeOutcome(Result? result) => result == Result.Succeeded ? "succeeded" : result == Result.Failed ? "failed" : result == Result.Cancelled ? "cancelled" : "unknown";
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
	private static string ExtractReasonCode(string message, string outcome, string exceptionMessage)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(message) && message.TrimStart().StartsWith("{"))
			{
				JObject diagnostic = JObject.Parse(message);
				string reason = (string)diagnostic["reason_code"] ?? (string)diagnostic["reason"];
				if (!string.IsNullOrWhiteSpace(reason)) return Limit(reason.Trim().ToUpperInvariant(), 128);
			}
		}
		catch { }
		return outcome == "succeeded" ? "COMPLETED" : outcome == "cancelled" ? "CANCELLED" : string.IsNullOrWhiteSpace(exceptionMessage) ? "FAILED" : "UNEXPECTED_ERROR";
	}
	private static string GetReadableLogPath() => File.Exists(LogPath) || !File.Exists(LegacyLogPath) ? LogPath : LegacyLogPath;
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
