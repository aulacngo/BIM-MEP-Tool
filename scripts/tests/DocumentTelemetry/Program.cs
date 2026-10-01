using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using BIN;
using Newtonsoft.Json.Linq;

internal static class Program
{
	private static int _passed;
	private static void Main()
	{
		ApiThread.Check();
		Run("family, empty and ephemeral events", Filters);
		Run("five-lookups limit and conservative view filtering", Sampling);
		Run("delete metadata, API failures and JSON escaping", Metadata);
		Run("burst accumulation and document/view/undo separation", Batching);
		Run("pending work and metadata remain bounded", Bounds);
		Run("single sender, real rate limit, failure recovery and shutdown", Dispatch);
		Run("idempotent startup and stopped handler", Lifecycle);
		Assert(ApiThread.Violations == 0, "No Revit API calls from workers");
		Console.WriteLine($"PASS: {_passed} scenarios; no network requests; API thread violations: {ApiThread.Violations}");
	}

	private static void Run(string name, Action action)
	{
		action();
		_passed++;
		Console.WriteLine("PASS " + name);
	}

	private static void Assert(bool condition, string message)
	{
		if (!condition) throw new InvalidOperationException(message);
	}

	private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

	private static DocumentTelemetryTracker.Dispatcher Capture()
	{
		var dispatcher = new DocumentTelemetryTracker.Dispatcher(_ => throw new InvalidOperationException("Unexpected send in capture mode"));
		((Timer)Field(dispatcher.GetType(), "_timer").GetValue(dispatcher)).Change(Timeout.Infinite, Timeout.Infinite);
		Field(typeof(DocumentTelemetryTracker), "_dispatcher").SetValue(null, dispatcher);
		return dispatcher;
	}

	private static List<DocumentTelemetryTracker.Snapshot> Pending(DocumentTelemetryTracker.Dispatcher dispatcher)
		=> (List<DocumentTelemetryTracker.Snapshot>)Field(dispatcher.GetType(), "_pending").GetValue(dispatcher);

	private static DocumentChangedEventArgs Change(Document doc = null, int added = 0, int deleted = 0, int modified = 0, string name = "Draw pipes")
	{
		var e = new DocumentChangedEventArgs { Document = doc ?? new Document(), Names = new List<string> { name } };
		int id = 1;
		for (int i = 0; i < added; i++, id++) { e.Added.Add(new ElementId(id)); e.Document.Elements[id] = new Element(); }
		for (int i = 0; i < modified; i++, id++) { e.Modified.Add(new ElementId(id)); e.Document.Elements[id] = new Element(); }
		for (int i = 0; i < deleted; i++, id++) e.Deleted.Add(new ElementId(id));
		return e;
	}

	private static void Fire(DocumentChangedEventArgs e) => DocumentTelemetryTracker.OnDocumentChanged(null, e);

	private static void Filters()
	{
		using (var dispatcher = Capture())
		{
			Fire(null);
			Fire(Change(new Document { Family = true }, added: 1));
			Fire(Change());
			Fire(Change(deleted: 1, name: "  "));
			Fire(Change(modified: 1, name: "Reset Temporary Hide/Isolate"));
			Fire(Change(modified: 1, name: " pan "));
			Assert(Pending(dispatcher).Count == 0, "Noise must be ignored");
			Fire(Change(added: 1, name: "Create View"));
			Fire(Change(modified: 1, name: "Internal pipe parameter"));
			var mixed = Change(added: 1, name: "Zoom");
			mixed.Names.Add("Place family");
			Fire(mixed);
			Assert(Pending(dispatcher).Count == 3, "Broad name filters must not drop meaningful work");
		}
	}

	private static void Sampling()
	{
		using (var dispatcher = Capture())
		{
			var large = Change(added: 20, modified: 20);
			Fire(large);
			Assert(large.Document.Lookups == 5, "Five lookups TOTAL across added and modified");
			var views = Change(modified: 5, name: "View state");
			foreach (ElementId id in views.Modified) views.Document.Elements[id.Value] = new View();
			Fire(views);
			Assert(Pending(dispatcher).Count == 1, "Known view-only modification must be ignored");
			var mixed = Change(modified: 6);
			foreach (ElementId id in mixed.Modified.Take(5)) mixed.Document.Elements[id.Value] = new View();
			Fire(mixed);
			Assert(Pending(dispatcher).Count == 2 && mixed.Document.Lookups == 5, "Partial sample must not reject model edits");
			var failed = Change(modified: 1);
			failed.Document.ThrowElement = true;
			Fire(failed);
			Assert(Pending(dispatcher).Count == 3, "Failed category lookup must preserve counts");
		}
	}

	private static void Metadata()
	{
		using (var dispatcher = Capture())
		{
			var e = Change(deleted: 5, name: "Delete \"pipes\"\nLevel 1");
			e.Document.DocumentTitle = "Dự án \"A\"";
			e.Document.App.User = null;
			e.Document.ThrowView = true;
			Fire(e);
			JObject json = JObject.Parse(Pending(dispatcher).Single().ToJson());
			Assert((string)json["project_name"] == e.Document.DocumentTitle, "JSON preserves Unicode and escapes");
			Assert((string)json["user_name"] == Environment.UserName, "Username fallback");
			Assert((string)json["command_name"] == "DocumentChanged: Delete", "Delete label");
			Assert((int)json["details"]["deleted_count"] == 5, "Deleted count");
			Assert((string)json["details"]["view"] == "", "ActiveView failure is isolated");
			Assert(!json["details"]["categories"].Any() && e.Document.Lookups == 0, "No lookup of deleted elements");
		}
	}

	private static void Batching()
	{
		using (var dispatcher = Capture())
		{
			var doc = new Document();
			for (int i = 0; i < 100; i++) Fire(Change(doc, added: 1, deleted: 2, modified: 3));
			var batch = Pending(dispatcher).Single();
			Assert(batch.AddedCount == 100 && batch.DeletedCount == 200 && batch.ModifiedCount == 300 && batch.EventCount == 100, "No lost counts in bursts");
			Fire(Change(added: 1)); // Same title, separate document instance.
			doc.CurrentView.ViewName = "Level 2";
			Fire(Change(doc, added: 1));
			var undo = Change(doc, deleted: 1);
			undo.Kind = UndoOperation.TransactionUndone;
			Fire(undo);
			Assert(Pending(dispatcher).Count == 4, "Separate document, view and undo contexts");
		}
	}

	private static void Bounds()
	{
		using (var dispatcher = Capture())
		{
			var doc = new Document();
			for (int i = 0; i < 100; i++)
			{
				var e = Change(doc, added: 1, name: i + new string('x', 2000));
				e.Document.Elements[1].ElementCategory.CategoryName = "Category " + i;
				Fire(e);
			}
			var batch = Pending(dispatcher).Single();
			Assert(batch.Transactions.Count == 16 && batch.Transactions.All(n => n.Length <= 1024) && batch.Categories.Count == 5, "Bounded metadata");
			for (int i = 0; i < 100; i++) Fire(Change(added: 1));
			Assert(Pending(dispatcher).Count == 32, "Bounded pending contexts");
			Fire(Change(doc, deleted: 1));
			Assert(batch.DeletedCount == 1, "Existing batches still merge at capacity");
		}
	}

	private static DocumentTelemetryTracker.Snapshot Batch(string key) => new DocumentTelemetryTracker.Snapshot
	{
		DocumentKey = key, ProjectName = key, UserName = "test", ViewName = "Level 1", Operation = "TransactionCommitted",
		AddedCount = 1, EventCount = 1, Categories = new List<string> { "Pipes" }, Transactions = new List<string> { "Draw pipes" }
	};

	private static void Dispatch()
	{
		using (var firstStarted = new ManualResetEventSlim())
		using (var releaseFirst = new ManualResetEventSlim())
		using (var secondFinished = new ManualResetEventSlim())
		using (var thirdFinished = new ManualResetEventSlim())
		{
			int calls = 0, active = 0, maximumActive = 0;
			long firstTick = 0, secondTick = 0, thirdTick = 0;
			var dispatcher = new DocumentTelemetryTracker.Dispatcher(batch =>
			{
				int count = Interlocked.Increment(ref calls);
				maximumActive = Math.Max(maximumActive, Interlocked.Increment(ref active));
				try
				{
					Assert(Thread.CurrentThread.ManagedThreadId != ApiThread.Main, "Send must run on a worker");
					JObject.Parse(batch.ToJson());
					if (count == 1)
					{
						firstTick = Stopwatch.GetTimestamp();
						firstStarted.Set();
						if (!releaseFirst.Wait(10000)) throw new TimeoutException();
						throw new InvalidOperationException("Simulated network failure");
					}
					if (count == 2)
					{
						Assert(batch.EventCount == 1000 && batch.AddedCount == 1000, "Accumulate changes during slow HTTP");
						secondTick = Stopwatch.GetTimestamp();
						secondFinished.Set();
					}
					if (count == 3) { thirdTick = Stopwatch.GetTimestamp(); thirdFinished.Set(); }
				}
				finally { Interlocked.Decrement(ref active); }
			});
			try
			{
				dispatcher.Add(Batch("A"));
				Assert(firstStarted.Wait(5000), "First dispatch timeout");
				var watch = Stopwatch.StartNew();
				for (int i = 0; i < 1000; i++) dispatcher.Add(Batch("B"));
				Assert(watch.ElapsedMilliseconds < 1000, "Adding during slow HTTP must not wait for HTTP");
				Assert(!secondFinished.Wait(1200), "Slow first sender must prevent overlapping sends");
				releaseFirst.Set();
				Assert(secondFinished.Wait(5000), "A failed sender must not wedge the queue");
				dispatcher.Add(Batch("C"));
				Assert(thirdFinished.Wait(5000), "Third dispatch timeout");
				Assert(maximumActive == 1 && secondTick - firstTick >= Stopwatch.Frequency && thirdTick - secondTick >= Stopwatch.Frequency, "Single sender and minimum one-second interval");
				dispatcher.Add(Batch("D"));
				dispatcher.Dispose();
				dispatcher.Add(Batch("E"));
				Assert(!SpinWait.SpinUntil(() => Volatile.Read(ref calls) > 3, 1200), "No queued or later sends after shutdown");
				Assert(Pending(dispatcher).Count == 0, "Shutdown clears pending work");
			}
			finally { releaseFirst.Set(); dispatcher.Dispose(); DocumentTelemetryTracker.Stop(); }
		}
	}

	private static void Lifecycle()
	{
		DocumentTelemetryTracker.Start();
		object first = Field(typeof(DocumentTelemetryTracker), "_dispatcher").GetValue(null);
		Assert(first != null, "Startup creates dispatcher");
		DocumentTelemetryTracker.Start();
		Assert(ReferenceEquals(first, Field(typeof(DocumentTelemetryTracker), "_dispatcher").GetValue(null)), "Repeated startup keeps one sender");
		DocumentTelemetryTracker.Stop();
		DocumentTelemetryTracker.Stop();
		var e = Change(added: 1);
		Fire(e);
		Assert(e.Document.Lookups == 0 && Field(typeof(DocumentTelemetryTracker), "_dispatcher").GetValue(null) == null, "Stopped handler is inert");
	}
}
