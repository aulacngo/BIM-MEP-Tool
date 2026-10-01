using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace BIN;

/// <summary>
/// Process-local, best-effort telemetry outbox. It owns no Revit objects: callers
/// must capture primitive values before calling TryEnqueue.
/// </summary>
public static class TelemetryBatchDispatcher
{
	private const int Capacity = 500;
	private const int FlushThreshold = 10;
	private const int FlushIntervalMilliseconds = 5000;
	private const int GzipThresholdBytes = 1024;

	private static readonly ConcurrentQueue<string> Queue = new ConcurrentQueue<string>();
	private static readonly AutoResetEvent FlushSignal = new AutoResetEvent(false);
	private static readonly SemaphoreSlim AvailableSlots = new SemaphoreSlim(Capacity, Capacity);
	private static int queuedCount;
	private static int droppedCount;
	private static int workerStarted;

	public static int DroppedCount { get { return Volatile.Read(ref droppedCount); } }
	public static int QueuedCount { get { return Volatile.Read(ref queuedCount); } }

	/// <summary>
	/// Adds a pre-serialized event without waiting for file, compression, DNS, or
	/// network work. Wait(0) makes the capacity reservation without blocking and
	/// keeps the ConcurrentQueue at Capacity under concurrent callbacks.
	/// </summary>
	public static bool TryEnqueue(string jsonEvent)
	{
		if (string.IsNullOrWhiteSpace(jsonEvent)) return true;

		if (!AvailableSlots.Wait(0))
		{
			Interlocked.Increment(ref droppedCount);
			return false;
		}
		int newCount = Interlocked.Increment(ref queuedCount);

		try
		{
			Queue.Enqueue(jsonEvent);
			EnsureWorker();
			if (newCount >= FlushThreshold) FlushSignal.Set();
			return true;
		}
		catch
		{
			Interlocked.Decrement(ref queuedCount);
			AvailableSlots.Release();
			Interlocked.Increment(ref droppedCount);
			return false;
		}
	}

	private static void EnsureWorker()
	{
		if (Interlocked.CompareExchange(ref workerStarted, 1, 0) != 0) return;
		try
		{
			Thread worker = new Thread(WorkerLoop);
			worker.IsBackground = true;
			worker.Name = "BIN Telemetry Batch Dispatcher";
			worker.Start();
		}
		catch
		{
			Interlocked.Exchange(ref workerStarted, 0);
		}
	}

	private static void WorkerLoop()
	{
		while (true)
		{
			try
			{
				FlushSignal.WaitOne(FlushIntervalMilliseconds);
				FlushOnce();
			}
			catch
			{
				// Telemetry is strictly best effort. Keep the worker available for later events.
			}
		}
	}

	private static void FlushOnce()
	{
		List<string> batch = new List<string>(Capacity);
		string item;
		while (batch.Count < Capacity && Queue.TryDequeue(out item))
		{
			Interlocked.Decrement(ref queuedCount);
			AvailableSlots.Release();
			if (!string.IsNullOrWhiteSpace(item)) batch.Add(item);
		}
		if (batch.Count == 0) return;

		byte[] uncompressed = Encoding.UTF8.GetBytes("[" + string.Join(",", batch) + "]");
		bool isGzip = uncompressed.Length > GzipThresholdBytes;
		byte[] payload = isGzip ? Compress(uncompressed) : uncompressed;

		try
		{
			// This executes on the dedicated background worker, never a Revit API/UI thread.
			TelemetryHttpTransport.PostBatchAsync(payload, isGzip).GetAwaiter().GetResult();
		}
		catch
		{
			// Transport is silent by contract. Dropped delivery must not alter BIM work.
		}
	}

	private static byte[] Compress(byte[] payload)
	{
		using (MemoryStream output = new MemoryStream())
		{
			using (GZipStream gzip = new GZipStream(output, CompressionMode.Compress, true))
			{
				gzip.Write(payload, 0, payload.Length);
			}
			return output.ToArray();
		}
	}
}
