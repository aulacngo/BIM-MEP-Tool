using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BIN;

// Implementations must own bounded primitive snapshots, never API objects or delegates.
internal abstract class TelemetryWorkItem
{
	internal abstract int RetainedBytes { get; }
	internal abstract string SerializeOnWorker();
	internal static int StringBytes(string value) => value == null ? 0 : 32 + value.Length * 2;
}

/// <summary>One bounded outbox for network events AND local diagnostics.</summary>
public static class TelemetryBatchDispatcher
{
	public const int Capacity = 200;
	public const int MaxQueuedBytes = 2 * 1024 * 1024;
	internal const int MaxEventCharacters = 8192;
	private const int BatchSize = 5;
	// BCL-only shared state is usable by Assembly.Load(byte[]) copies. Otherwise each
	// hot-load would create another permanently rooted timer and independent outbox.
	private static readonly object[] Shared = GetSharedState();
	private static object Sync => Shared[0];
	private static Queue<object[]> Queue => (Queue<object[]>)Shared[1];
	// bytes, worker-active, admission-drops, unacknowledged-deliveries
	private static int[] Counters => (int[])Shared[2];
	internal static string SessionId => (string)Shared[5];
	internal static long NextSequence() => Interlocked.Increment(ref ((long[])Shared[6])[0]);
	public static int DroppedCount => Volatile.Read(ref Counters[2]);
	public static int FailedDeliveryCount => Volatile.Read(ref Counters[3]);
	public static int QueuedCount { get { lock (Sync) return Queue.Count; } }
	public static int QueuedBytes => Volatile.Read(ref Counters[0]);
	internal static void Initialize() { GC.KeepAlive(Shared); }

	private static object[] GetSharedState()
	{
		const string key = "BIM.Tool.Telemetry.Outbox.V2";
		lock (AppDomain.CurrentDomain)
		{
			if (AppDomain.CurrentDomain.GetData(key) is object[] existing) return existing;
			var state = new object[] { new object(), new Queue<object[]>(Capacity), new int[4], null,
				(Action)ScheduleCore, Guid.NewGuid().ToString("N"), new long[1] };
			state[3] = new Timer(_ => ScheduleCore(), null, 5000, 5000);
			AppDomain.CurrentDomain.SetData(key, state);
			return state;
		}
	}

	public static bool TryEnqueue(string jsonEvent)
	{
		if (jsonEvent != null && jsonEvent.Length > MaxEventCharacters) { Drop(); return false; }
		if (string.IsNullOrWhiteSpace(jsonEvent)) return true;
		return TryEnqueue(new JsonItem(jsonEvent));
	}

	internal static bool TryEnqueue(TelemetryWorkItem item)
	{
		if (item == null) return false;
		int size = item.RetainedBytes + 128; // entry array, boxed size and method delegate
		if (size <= 0 || size > MaxQueuedBytes) { Drop(); return false; }
		// Never wait behind a descheduled producer/consumer on the Revit thread.
		if (!Monitor.TryEnter(Sync)) { Drop(); return false; }
		int count;
		try
		{
			while (Queue.Count >= Capacity || Counters[0] + size > MaxQueuedBytes)
			{
				Counters[0] -= (int)Queue.Dequeue()[0];
				Drop(); // Capacity overload always evicts the oldest snapshot.
			}
			// Only this internal method creates delegates, targeting a bounded primitive DTO.
			Queue.Enqueue(new object[] { size, (Func<string>)item.SerializeOnWorker });
			Counters[0] += size;
			count = Queue.Count;
		}
		finally { Monitor.Exit(Sync); }
		if (count >= BatchSize) Schedule();
		return true;
	}

	internal static void Drop() => Interlocked.Increment(ref Counters[2]);

	private static void Schedule() => ((Action)Shared[4])();

	private static void ScheduleCore()
	{
		if (Interlocked.CompareExchange(ref Counters[1], 1, 0) != 0) return;
		try
		{
			if (!ThreadPool.QueueUserWorkItem(async _ => await DrainAsync().ConfigureAwait(false)))
				Interlocked.Exchange(ref Counters[1], 0);
		}
		catch { Interlocked.Exchange(ref Counters[1], 0); }
	}

	private static object[] Take()
	{
		lock (Sync)
		{
			if (Queue.Count == 0) return null;
			object[] item = Queue.Dequeue();
			Counters[0] -= (int)item[0];
			return item;
		}
	}

	private static async Task DrainAsync()
	{
		try
		{
			// At most 5 capped JSON strings + one snapshot are in flight, even offline.
			List<string> batch = new List<string>(BatchSize);
			for (int i = 0; i < BatchSize; i++)
			{
				object[] item = Take();
				if (item == null) break;
				try
				{
					string json = ((Func<string>)item[1])();
					if (string.IsNullOrEmpty(json)) continue; // Local-only entry.
					if (json.Length > MaxEventCharacters) { Drop(); continue; }
					batch.Add(json);
				}
				catch { Drop(); } // One bad serializer cannot discard its neighbours.
			}
			if (batch.Count == 0) return;
			byte[] bytes = Encoding.UTF8.GetBytes("[" + string.Join(",", batch) + "]");
			bool gzip = bytes.Length > 1024;
			byte[] payload = gzip ? Compress(bytes) : bytes;
			if (!await TelemetryHttpTransport.PostBatchAsync(payload, gzip).ConfigureAwait(false))
				Interlocked.Add(ref Counters[3], batch.Count);
		}
		catch { /* Telemetry must not escape into Revit. */ }
		finally
		{
			Interlocked.Exchange(ref Counters[1], 0);
			// The timer also drains a tail smaller than BatchSize. No lost wake-up.
			if (QueuedCount >= BatchSize) Schedule();
		}
	}

	private static byte[] Compress(byte[] bytes)
	{
		using (var output = new MemoryStream())
		{
			using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
				gzip.Write(bytes, 0, bytes.Length);
			return output.ToArray();
		}
	}

	private sealed class JsonItem : TelemetryWorkItem
	{
		private readonly string json;
		internal JsonItem(string value) { json = value; }
		internal override int RetainedBytes => 64 + StringBytes(json);
		internal override string SerializeOnWorker()
		{
			// Parse each legacy input separately; malformed JSON cannot poison the array.
			return Newtonsoft.Json.Linq.JObject.Parse(json).ToString(Newtonsoft.Json.Formatting.None);
		}
	}
}
