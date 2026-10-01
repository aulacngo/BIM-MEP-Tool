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
	private const int MaxTransactionNames = 16;
	private static readonly object LifecycleSync = new object();
	private static readonly ConditionalWeakTable<Document, DocumentIdentity> DocumentIdentities = new ConditionalWeakTable<Document, DocumentIdentity>();
	private static Dispatcher _dispatcher;

	// Exact names only: broad matches such as "View", "Internal" or "Hide" can
	// discard real BIM work. Unknown/localized transaction names remain eligible.
	private static readonly HashSet<string> EphemeralTransactions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"Pan", "Zoom", "Zoom In", "Zoom Out", "Zoom to Fit", "Zoom All to Fit",
		"Activate View", "Set Active View", "Selection", "Highlight Elements",
		"Temporary Hide/Isolate", "Reset Temporary Hide/Isolate",
		"Temporary View Properties", "Enable Temporary View Properties", "Disable Temporary View Properties"
	};

	public static void Start()
	{
		try
		{
			lock (LifecycleSync)
			{
				if (_dispatcher == null) Volatile.Write(ref _dispatcher, new Dispatcher(SendTelemetry));
			}
		}
		catch { }
	}

	public static void Stop()
	{
		try
		{
			lock (LifecycleSync) { Interlocked.Exchange(ref _dispatcher, null)?.Dispose(); }
		}
		catch { }
	}

	public static void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
	{
		try
		{
			Dispatcher dispatcher = Volatile.Read(ref _dispatcher);
			if (dispatcher == null || e == null) return;
			Document doc = e.GetDocument();
			if (doc == null || !doc.IsValidObject || doc.IsFamilyDocument) return;

			ICollection<ElementId> added = e.GetAddedElementIds();
			ICollection<ElementId> deleted = e.GetDeletedElementIds();
			ICollection<ElementId> modified = e.GetModifiedElementIds();
			if (added.Count == 0 && deleted.Count == 0 && modified.Count == 0) return;

			var transactions = new List<string>();
			bool meaningfulTransaction = false;
			foreach (string name in e.GetTransactionNames())
			{
				if (string.IsNullOrWhiteSpace(name)) continue;
				string trimmed = name.Trim();
				if (!EphemeralTransactions.Contains(trimmed)) meaningfulTransaction = true;
				AddDistinct(transactions, Limit(trimmed, 1024), MaxTransactionNames);
			}
			if (!meaningfulTransaction) return;

			var categories = new List<string>();
			int inspected = 0;
			bool onlyViews = true;
			SampleElements(doc, added, categories, ref inspected, ref onlyViews);
			SampleElements(doc, modified, categories, ref inspected, ref onlyViews);
			// Skip view-only modifications only when the ENTIRE set was inspected.
			// A five-element sample must never hide a larger mixed model change.
			if (added.Count == 0 && deleted.Count == 0 && inspected == modified.Count && onlyViews) return;

			string viewName = "";
			string userName = Environment.UserName;
			try { viewName = doc.ActiveView?.Name ?? ""; } catch { }
			try { userName = doc.Application.Username ?? userName; } catch { }
			var snapshot = new Snapshot
			{
				DocumentKey = DocumentIdentities.GetValue(doc, CreateDocumentIdentity).Key,
				ProjectName = Limit(doc.Title, 1024),
				UserName = Limit(userName, 1024),
				ViewName = Limit(viewName, 1024),
				Operation = e.Operation.ToString(),
				AddedCount = added.Count,
				DeletedCount = deleted.Count,
				ModifiedCount = modified.Count,
				EventCount = 1,
				Transactions = transactions,
				Categories = categories
			};
			// No Document, Element, ElementId, event args or API-backed enumerable
			// crosses this boundary. All Revit API reads above run on its main thread.
			dispatcher.Add(snapshot);
		}
		catch { }
	}

	private static DocumentIdentity CreateDocumentIdentity(Document document) => new DocumentIdentity();

	private sealed class DocumentIdentity
	{
		internal readonly string Key = Guid.NewGuid().ToString("N");
	}

	private static void SampleElements(Document doc, ICollection<ElementId> ids, List<string> categories, ref int inspected, ref bool onlyViews)
	{
		foreach (ElementId id in ids)
		{
			if (inspected >= SampleSize) break;
			inspected++;
			try
			{
				Element element = doc.GetElement(id);
				if (!(element is View)) onlyViews = false;
				string category = element?.Category?.Name;
				if (!string.IsNullOrWhiteSpace(category)) AddDistinct(categories, Limit(category, 256), SampleSize);
			}
			catch { onlyViews = false; }
		}
	}

	private static string Limit(string value, int length) => value == null ? "" : value.Length <= length ? value : value.Substring(0, length);

	private static void AddDistinct(List<string> values, string value, int limit)
	{
		if (values.Count < limit && !values.Contains(value)) values.Add(value);
	}

	// Primitive-only batches. Counts are accumulated change occurrences, not
	// unique elements across events; repeated edits to one element count again.
	internal sealed class Snapshot
	{
		internal string DocumentKey;
		internal string ProjectName;
		internal string UserName;
		internal string ViewName;
		internal string Operation;
		internal long AddedCount;
		internal long DeletedCount;
		internal long ModifiedCount;
		internal long EventCount;
		internal List<string> Transactions;
		internal List<string> Categories;

		internal bool CanMerge(Snapshot other) => DocumentKey == other.DocumentKey &&
			ProjectName == other.ProjectName && UserName == other.UserName &&
			ViewName == other.ViewName && Operation == other.Operation;

		internal void Merge(Snapshot other)
		{
			AddedCount += other.AddedCount;
			DeletedCount += other.DeletedCount;
			ModifiedCount += other.ModifiedCount;
			EventCount += other.EventCount;
			foreach (string name in other.Transactions) AddDistinct(Transactions, name, MaxTransactionNames);
			foreach (string category in other.Categories) AddDistinct(Categories, category, SampleSize);
		}

		internal string ToJson()
		{
			// Mixed batches retain all three counts; deletion takes label priority.
			// Explicit JSON tokens avoid the legacy bundled Newtonsoft reflection
			// serializer's System.Security.Permissions dependency on .NET 8.
			return new JObject
			{
				["project_name"] = ProjectName,
				["user_name"] = UserName,
				["command_name"] = "DocumentChanged: " + (DeletedCount > 0 ? "Delete" : AddedCount > 0 ? "Add" : "Modify"),
				["details"] = new JObject
				{
					["transaction"] = string.Join(", ", Transactions),
					["deleted_count"] = DeletedCount,
					["added_count"] = AddedCount,
					["modified_count"] = ModifiedCount,
					["categories"] = new JArray(Categories),
					["view"] = ViewName,
					["operation"] = Operation,
					["event_count"] = EventCount
				}
			}.ToString(Formatting.None);
		}
	}

	// One process-local dispatcher with a one-second minimum enqueue interval.
	// Pending batches are capped and merged before handoff to the async transport.
	// Overload drops new batch keys; failures are best-effort/no retry.
	internal sealed class Dispatcher : IDisposable
	{
		private const int MaxPendingBatches = 32;
		private readonly object _sync = new object();
		private readonly List<Snapshot> _pending = new List<Snapshot>();
		private readonly Action<Snapshot> _send;
		private readonly Timer _timer;
		private long _lastDispatch;
		private bool _inFlight;
		private bool _stopped;

		internal Dispatcher(Action<Snapshot> send)
		{
			_send = send;
			_timer = new Timer(OnTimer, null, 1000, 1000);
		}

		internal void Add(Snapshot snapshot)
		{
			lock (_sync)
			{
				if (_stopped) return;
				foreach (Snapshot pending in _pending)
				{
					if (!pending.CanMerge(snapshot)) continue;
					pending.Merge(snapshot);
					return;
				}
				if (_pending.Count < MaxPendingBatches) _pending.Add(snapshot);
			}
		}

		private void OnTimer(object state)
		{
			try
			{
				Snapshot batch;
				lock (_sync)
				{
					if (_stopped || _inFlight || _pending.Count == 0) return;
					if (Stopwatch.GetTimestamp() - _lastDispatch < Stopwatch.Frequency) return;
					batch = _pending[0];
					_pending.RemoveAt(0);
					_inFlight = true;
				}
				bool queued = false;
				try { queued = ThreadPool.QueueUserWorkItem(_ => Dispatch(batch)); }
				finally
				{
					if (!queued) { lock (_sync) { _inFlight = false; } }
				}
			}
			catch { }
		}

		private void Dispatch(Snapshot batch)
		{
			try
			{
				lock (_sync)
				{
					if (_stopped) return;
					_lastDispatch = Stopwatch.GetTimestamp();
				}
				_send(batch);
			}
			catch { }
			finally { lock (_sync) { _inFlight = false; } }
		}

		public void Dispose()
		{
			lock (_sync)
			{
				_stopped = true;
				_pending.Clear();
			}
			// Do not wait for HTTP at Revit shutdown. Pending batches are discarded;
			// work already handed to the async transport may still finish.
			_timer.Dispose();
		}
	}

	private static void SendTelemetry(Snapshot snapshot)
	{
		try
		{
			TelemetryHttpTransport.PostJsonAsync(snapshot.ToJson());
		}
		catch { }
	}
}
