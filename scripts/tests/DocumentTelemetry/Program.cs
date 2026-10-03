using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using BIN;
using Newtonsoft.Json.Linq;

internal static class Program
{
	private static int passed;
	private static readonly List<JObject> Fixtures = new List<JObject>();
	private static object[] Shared => (object[])typeof(TelemetryBatchDispatcher).GetField("Shared", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
	private static int[] Counters => (int[])Shared[2];
	private static Queue<object[]> Queue => (Queue<object[]>)Shared[1];
	private static void Main(string[] args)
	{
		ApiThread.Check();
		((Timer)Shared[3]).Change(Timeout.Infinite, Timeout.Infinite);
		Counters[1] = 1; // Freeze the sender for deterministic admission/capture tests.
		DocumentTelemetryTracker.Start();
		Run("V2 document contract, bounded samples, rollback and origin", DocumentContract);
		Run("L1/L2 correlation, selection, exception, immutable primitive capture", Correlation);
		Run("hot-loaded assemblies share scope, session, sequence and one outbox", HotLoad);
		Run("drop oldest, byte cap, rejection, no UI wait, retained memory", Bounds);
		Run("single async sender, gzip, serialization isolation and slow/offline delivery", Sender);
		Run("synthetic callback latency distribution (not Revit proof)", Latency);
		Assert(ApiThread.Violations == 0, "No Revit API use on worker");
		if (args.Length > 0) File.WriteAllText(args[0], new JArray(Fixtures).ToString(Newtonsoft.Json.Formatting.None));
		Console.WriteLine($"PASS: {passed} scenarios; API thread violations={ApiThread.Violations}; network requests=0");
		DocumentTelemetryTracker.Stop();
	}
	private static void Run(string name, Action action) { Clear(); action(); passed++; Console.WriteLine("PASS " + name); }
	private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
	private static void Clear() { lock (Shared[0]) { Queue.Clear(); Counters[0] = 0; } }
	private static List<JObject> Drain()
	{
		var callbacks = new List<Func<string>>();
		lock (Shared[0]) { while (Queue.Count > 0) callbacks.Add((Func<string>)Queue.Dequeue()[1]); Counters[0] = 0; }
		return Task.Run(() => callbacks.Select(f => f()).Where(s => s != null).Select(JObject.Parse).ToList()).GetAwaiter().GetResult();
	}
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
	private static void DocumentContract()
	{
		Fire(null);
		Fire(Change(new Document { Family = true }, added: 1));
		Fire(Change());
		Fire(Change(modified: 1, name: "Pan"));
		Assert(Queue.Count == 0, "Family, empty, exact ephemeral events ignored");
		var big = Change(added: 40, modified: 40);
		Fire(big);
		Assert(big.Document.Lookups <= 5, "At most five lookups across all changed IDs");
		var data = Drain().Single();
		Assert((string)data["tier"] == "L1" && (string)data["event_type"] == "document.changed", "V2 contract");
		Assert((int)data["context"]["added_count"] == 40 && (int)data["context"]["modified_count"] == 40, "Exact counts");
		Assert((string)data["context"]["origin"] == "user_or_other_addin", "No guessed user origin");
		Assert(!(bool)data["quality"]["categories_complete"], "Sample disclosed");
		Fixtures.Add(data);
		var rollback = Change(); rollback.Kind = UndoOperation.TransactionRolledBack;
		Fire(rollback);
		Assert((string)Drain().Single()["context"]["transaction_state"] == "RolledBack", "Zero-ID rollback retained");
		var deleted = Change(deleted: 10);
		deleted.Kind = UndoOperation.TransactionUndone;
		Fire(deleted);
		var undo = Drain().Single();
		Assert((string)undo["context"]["transaction_state"] == "Undone" && deleted.Document.Lookups == 0, "Undo/deleted evidence");
		Fixtures.Add(undo);
		var inaccessible = Change(modified: 1);
		inaccessible.Document.ThrowElement = true;
		Fire(inaccessible);
		Assert((int)Drain().Single()["context"]["modified_count"] == 1, "Failed category lookup preserves event counts");
		var manyNames = Change(added: 1);
		manyNames.Names = Enumerable.Range(0, 1000).Select(i => new string('x', 1024)).ToList();
		Fire(manyNames);
		Assert(Drain().Single()["context"]["transactions"].Count() == 8, "Transaction metadata bounded");
		DocumentTelemetryTracker.Stop();
		Fire(Change(added: 1));
		Assert(Queue.Count == 0, "Stopped hook inert");
		DocumentTelemetryTracker.Start();
	}
	private static void Correlation()
	{
		var data = new ExternalCommandData();
		for (int i = 1; i <= 40; i++) data.Application.ActiveUIDocument.Selection.Ids.Add(new ElementId(i));
		string correlation;
		using (CommandDiagnostics.BeginCommand("MoveConnect"))
		{
			correlation = CommandDiagnostics.CurrentInvocation[0];
			using (CommandDiagnostics.BeginCommand("Nested")) Assert(CommandDiagnostics.CurrentInvocation[0] != correlation, "Nested invocation unique");
			Assert(CommandDiagnostics.CurrentInvocation[0] == correlation, "Outer scope restored");
			CommandDiagnostics.WriteL2("MoveConnect", "failed", "NO_CONNECTOR", new { count = 2, unsafe_object = data.Application.ActiveUIDocument.Document });
			var changed = Change(added: 3); Fire(changed);
			Exception error;
			try { ThrowTestError(); throw new Exception("unreachable"); } catch (InvalidOperationException e) { error = e; }
			CommandDiagnostics.Write("MoveConnect", "failed", data, Result.Failed, "missing connector", error, durationMilliseconds: 123);
		}
		Assert(CommandDiagnostics.CurrentInvocation == null, "Scope cleared even after command");
		data.Application.ActiveUIDocument.Document.DocumentTitle = "MUTATED AFTER CAPTURE";
		var events = Drain();
		Assert(events.Count == 4 && events.All(e => (string)e["correlation_id"] == correlation), "Same invocation on all detail/document/terminal events");
		JObject terminal = events.Single(e => (string)e["event_type"] == "command.terminal");
		Assert((int)terminal["context"]["selection_count"] == 40 && terminal["context"]["selected_element_ids"].Count() == 8, "Bounded selection sample");
		Assert((string)terminal["context"]["transaction_state"] == "Committed", "Failed command does not imply rollback");
		Assert(events.Where(e => (string)e["tier"] == "L2").All(e => (string)e["parent_event_id"] == (string)terminal["event_id"]), "L2 parent ID is exact terminal ID");
		Assert((bool)events[0]["quality"]["details_truncated_or_unsupported"], "Nonprimitive reference rejected");
		Assert(events.Any(e => e["details"]?["stack_trace"]?.ToString().Contains("ThrowTestError") == true), "Exception stack captured");
		Fixtures.AddRange(events);
		data.Application.ActiveUIDocument.Document.ThrowView = true;
		CommandDiagnostics.Write("MoveConnect", "failed", data, Result.Failed);
		Assert((bool)Drain().Single()["quality"]["context_incomplete"], "Broken optional API metadata does not erase terminal event");
		CommandDiagnostics.WriteL2Json("MoveConnect", "failed", "BAD_JSON", "{");
		Assert((bool)Drain().Single()["details"]["parse_error"], "Malformed L2 details isolated before batching");
		CommandDiagnostics.WriteL2Json("MoveConnect", "failed", null, "{\"reason\":\"NO_CONNECTOR\",\"source\":{\"name\":\"private\",\"id\":12}}");
		var sanitized = Drain().Single();
		Assert((string)sanitized["command"]["reason_code"] == "NO_CONNECTOR" && sanitized["details"]["source"]["name"] == null, "MoveConnect sanitizing/reason extraction moved to worker");
	}
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private static void ThrowTestError() => throw new InvalidOperationException("test exception");
	private static void HotLoad()
	{
		Assembly copy = Assembly.Load(File.ReadAllBytes(typeof(Program).Assembly.Location));
		Type diagnostics = copy.GetType("BIN.CommandDiagnostics");
		using (CommandDiagnostics.BeginCommand("MoveConnect"))
		{
			var foreignScope = (string[])diagnostics.GetProperty("CurrentInvocation", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
			Assert(foreignScope[0] == CommandDiagnostics.CurrentInvocation[0], "Primitive correlation crosses assembly boundary");
			diagnostics.GetMethod("WriteL2Json").Invoke(null, new object[] { "MoveConnect", "succeeded", "HOTLOAD", "{}" });
			Assert(Queue.Count == 1, "Hotloaded copy uses original queue");
			CommandDiagnostics.Write("MoveConnect", "completed", null, Result.Succeeded);
		}
		var events = Drain();
		Assert(events.Count == 2 && (string)events[0]["client"]["session_id"] == (string)events[1]["client"]["session_id"], "Shared session");
		Assert((long)events[0]["client"]["sequence"] < (long)events[1]["client"]["sequence"], "Shared monotonically increasing sequence");
		Fixtures.AddRange(events);
	}
	private static void Bounds()
	{
		for (int i = 0; i < 300; i++) TelemetryBatchDispatcher.TryEnqueue("{\"index\":" + i + "}");
		Assert(Queue.Count == 200, "200 snapshot hard cap");
		var rows = Drain();
		Assert((int)rows.First()["index"] == 100 && (int)rows.Last()["index"] == 299, "Oldest evicted, latest retained in order");
		long before = GC.GetTotalMemory(true);
		for (int i = 0; i < 1000; i++) TelemetryBatchDispatcher.TryEnqueue("{\"index\":" + i + ",\"x\":\"" + new string('x', 8000) + "\"}");
		long retained = GC.GetTotalMemory(true) - before;
		Assert(Queue.Count < 200 && TelemetryBatchDispatcher.QueuedBytes <= 2 * 1024 * 1024, "Byte limit evicts before count cap");
		Assert(retained < 3 * 1024 * 1024, "Synthetic retained managed memory under 3 MiB");
		Console.WriteLine($"Retained heap delta={retained} bytes; accounted outbox={TelemetryBatchDispatcher.QueuedBytes}; items={Queue.Count}");
		Clear();
		Assert(!TelemetryBatchDispatcher.TryEnqueue(new string('x', 8193)), "Oversized raw input rejected");
		using (var held = new ManualResetEventSlim())
		using (var release = new ManualResetEventSlim())
		{
			Task other = Task.Run(() => { lock (Shared[0]) { held.Set(); release.Wait(5000); } });
			Assert(held.Wait(5000), "Lock holder ready");
			var timer = Stopwatch.StartNew();
			Assert(!TelemetryBatchDispatcher.TryEnqueue("{}"), "Contended enqueue returns immediately");
			Assert(timer.ElapsedMilliseconds < 100, "No UI lock wait");
			release.Set(); other.GetAwaiter().GetResult();
		}
		Parallel.For(0, 8, p => { for (int i = 0; i < 1000; i++) TelemetryBatchDispatcher.TryEnqueue("{\"p\":" + p + "}"); });
		Assert(Queue.Count <= 200 && Counters[0] >= 0 && Counters[0] <= TelemetryBatchDispatcher.MaxQueuedBytes, "Concurrent admission remains bounded");
	}
	private static void Sender()
	{
		Counters[1] = 0;
		var release = new TaskCompletionSource<bool>();
		using (var entered = new ManualResetEventSlim())
		{
			TelemetryHttpTransport.Send = events => { entered.Set(); return release.Task; };
			for (int i = 0; i < 5; i++) TelemetryBatchDispatcher.TryEnqueue("{\"n\":" + i + ",\"text\":\"" + new string('a', 300) + "\"}");
			Assert(entered.Wait(5000), "Gzip batch reached background transport");
			int calls = TelemetryHttpTransport.Calls;
			for (int i = 0; i < 500; i++) TelemetryBatchDispatcher.TryEnqueue("{\"n\":" + i + "}");
			Assert(TelemetryHttpTransport.Calls == calls && Queue.Count == 200, "Slow network: one in flight, latest 200 queued");
			Clear();
			TelemetryHttpTransport.Send = _ => Task.FromResult(true);
			release.SetResult(false);
			Assert(SpinWait.SpinUntil(() => Volatile.Read(ref Counters[1]) == 0, 5000), "Failed transport releases consumer");
			Assert(TelemetryBatchDispatcher.FailedDeliveryCount >= 5, "Unacknowledged deliveries counted");
		}
		using (var received = new ManualResetEventSlim())
		{
			int rows = 0;
			TelemetryHttpTransport.Send = events => { rows += events.Count; received.Set(); return Task.FromResult(true); };
			Counters[1] = 1;
			TelemetryBatchDispatcher.TryEnqueue("{ malformed");
			for (int i = 0; i < 4; i++) TelemetryBatchDispatcher.TryEnqueue("{\"good\":" + i + "}");
			Counters[1] = 0;
			((Action)Shared[4])();
			Assert(received.Wait(5000), "Healthy items delivered after bad serializer");
			Assert(SpinWait.SpinUntil(() => Volatile.Read(ref Counters[1]) == 0, 5000), "Consumer idle");
			Assert(rows == 4 && TelemetryHttpTransport.MaximumActive == 1, "Only invalid item dropped; single sender");
		}
		Counters[1] = 1;
	}
	private static void Latency()
	{
		var change = Change(added: 30, modified: 30);
		for (int i = 0; i < 200; i++) Fire(change);
		Clear();
		var samples = new double[10000];
		for (int i = 0; i < samples.Length; i++)
		{
			long start = Stopwatch.GetTimestamp(); Fire(change);
			samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
		}
		Array.Sort(samples);
		Console.WriteLine($"STUB DocumentChanged ms: P50={samples[5000]:F4}; P95={samples[9500]:F4}; P99={samples[9900]:F4}; Max={samples[9999]:F4}");
		Assert(Queue.Count <= 200, "Burst still bounded");
		Clear();
	}
}
