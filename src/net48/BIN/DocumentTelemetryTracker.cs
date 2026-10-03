using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BIN;

public static class DocumentTelemetryTracker
{
	private const int SampleSize = 5;
	private const int MaxTransactionNames = 8;
	private static int started;
	private static readonly ConditionalWeakTable<Document, DocumentIdentity> Identities = new ConditionalWeakTable<Document, DocumentIdentity>();
	private static readonly HashSet<string> Ephemeral = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Pan", "Zoom", "Zoom In", "Zoom Out", "Zoom to Fit", "Zoom All to Fit", "Activate View", "Set Active View",
		"Selection", "Highlight Elements", "Temporary Hide/Isolate", "Reset Temporary Hide/Isolate",
		"Temporary View Properties", "Enable Temporary View Properties", "Disable Temporary View Properties"
	};

	public static void Start() { TelemetryBatchDispatcher.Initialize(); Volatile.Write(ref started, 1); }
	public static void Stop() { Volatile.Write(ref started, 0); }

	public static void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
	{
		if (Volatile.Read(ref started) == 0 || e == null) return;
		long begin = Stopwatch.GetTimestamp();
		try
		{
			Document doc = e.GetDocument();
			if (doc == null || !doc.IsValidObject || doc.IsFamilyDocument) return;
			string operation = e.Operation.ToString();
			string state = TransactionState(operation);
			CommandDiagnostics.ObserveTransaction(state);
			ICollection<ElementId> added = e.GetAddedElementIds();
			ICollection<ElementId> deleted = e.GetDeletedElementIds();
			ICollection<ElementId> modified = e.GetModifiedElementIds();
			// A rollback can contain no changed IDs and still carries valuable evidence.
			if (added.Count == 0 && deleted.Count == 0 && modified.Count == 0 && state != "RolledBack") return;
			IList<string> names = e.GetTransactionNames();
			var transactions = new List<string>(MaxTransactionNames);
			bool meaningful = names.Count == 0 || names.Count > MaxTransactionNames || state != "Committed";
			foreach (string name in names)
			{
				if (transactions.Count >= MaxTransactionNames) break;
				string safe = CommandDiagnostics.Limit(name, 128).Trim();
				if (!Ephemeral.Contains(safe)) meaningful = true;
				transactions.Add(safe);
			}
			if (!meaningful) return;
			string[] invocation = CommandDiagnostics.CurrentInvocation;
			var snapshot = new Snapshot
			{
				Project = CommandDiagnostics.Limit(doc.Title, 256),
				DocumentKey = Identities.GetValue(doc, _ => new DocumentIdentity()).Key,
				Operation = operation, State = state, Tool = "DocumentChanged:" + operation,
				AddedCount = added.Count, DeletedCount = deleted.Count, ModifiedCount = modified.Count,
				Transactions = transactions.ToArray(), TransactionsTruncated = names.Count > MaxTransactionNames,
				Correlation = invocation == null ? null : invocation[0],
				Origin = invocation == null ? "user_or_other_addin" : "bin_tool",
				OriginTool = invocation == null ? null : invocation[2]
			};
			// Only a tiny sample of API reads. Stop starting optional reads after 0.1 ms.
			// Individual API calls and scheduler pauses are not preemptible: measure in Revit.
			bool onlyViews = true;
			Sample(doc, added, snapshot, begin, ref onlyViews);
			Sample(doc, modified, snapshot, begin, ref onlyViews);
			if (added.Count == 0 && deleted.Count == 0 && snapshot.Inspected == modified.Count && onlyViews && state == "Committed") return;
			snapshot.CaptureTicks = Stopwatch.GetTimestamp() - begin;
			TelemetryBatchDispatcher.TryEnqueue(snapshot);
		}
		catch { TelemetryBatchDispatcher.Drop(); }
	}

	internal static string TransactionState(string operation)
	{
		switch (operation)
		{
			case "TransactionCommitted": return "Committed";
			case "TransactionRolledBack":
			case "TransactionGroupRolledBack": return "RolledBack";
			case "TransactionUndone": return "Undone";
			case "TransactionRedone": return "Redone";
			default: return "Unknown";
		}
	}

	private static void Sample(Document doc, ICollection<ElementId> ids, Snapshot snapshot, long begin, ref bool onlyViews)
	{
		foreach (ElementId id in ids)
		{
			if (snapshot.Inspected >= SampleSize || Stopwatch.GetTimestamp() - begin > Stopwatch.Frequency / 10000) break;
			snapshot.Inspected++;
			try
			{
				Element element = doc.GetElement(id);
				if (!(element is View)) onlyViews = false;
				Category category = element?.Category;
				if (category == null) continue;
				int at = snapshot.CategoryCount;
				snapshot.CategoryNames[at] = CommandDiagnostics.Limit(category.Name, 64);
#if NET8_0_OR_GREATER
				snapshot.CategoryIds[at] = category.Id.Value;
#else
				snapshot.CategoryIds[at] = category.Id.IntegerValue;
#endif
				snapshot.CategoryCount++;
			}
			catch { onlyViews = false; }
		}
	}

	private sealed class DocumentIdentity { internal readonly string Key = Guid.NewGuid().ToString("N"); }

	internal sealed class Snapshot : CommandDiagnostics.EventSnapshot
	{
		internal string DocumentKey, Operation, State, Origin, OriginTool;
		internal int AddedCount, DeletedCount, ModifiedCount, Inspected, CategoryCount;
		internal long CaptureTicks;
		internal string[] Transactions;
		internal bool TransactionsTruncated;
		internal readonly string[] CategoryNames = new string[SampleSize];
		internal readonly long[] CategoryIds = new long[SampleSize];
		// Upper bound for the fixed primitive arrays and their capped strings.
		internal override int RetainedBytes => CommonBytes + 4096;
		internal override string SerializeOnWorker()
		{
			var categories = new JArray();
			var ids = new HashSet<long>();
			for (int i = 0; i < CategoryCount; i++)
				if (ids.Add(CategoryIds[i])) categories.Add(new JObject { ["id"] = CategoryIds[i], ["name"] = CategoryNames[i] });
			JObject json = Envelope("L1", "document.changed");
			json["project"]["document_session_id"] = DocumentKey;
			json["command"] = new JObject { ["tool_id"] = Tool, ["stage"] = "document_changed", ["outcome"] = "observed", ["reason_code"] = State };
			json["context"] = new JObject
			{
				["origin"] = Origin, ["origin_tool_id"] = OriginTool,
				["origin_evidence"] = Correlation == null ? "no_bin_command_scope" : "active_bin_command_scope",
				["operation"] = Operation, ["transaction_state"] = State, ["transactions"] = new JArray(Transactions),
				["added_count"] = AddedCount, ["deleted_count"] = DeletedCount, ["modified_count"] = ModifiedCount,
				["categories"] = categories, ["event_count"] = 1
			};
			json["quality"]["categories_sampled"] = true;
			json["quality"]["inspected_count"] = Inspected;
			json["quality"]["categories_complete"] = DeletedCount == 0 && Inspected == AddedCount + ModifiedCount && CategoryCount == Inspected;
			json["quality"]["deleted_categories_unavailable"] = DeletedCount > 0;
			json["quality"]["transactions_truncated"] = TransactionsTruncated;
			json["quality"]["capture_ms"] = CaptureTicks * 1000.0 / Stopwatch.Frequency;
			return json.ToString(Formatting.None);
		}
	}
}
