using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BIN;

// No production network transport is linked into this executable.
public static class TelemetryHttpTransport
{
	internal static Func<JArray, Task<bool>> Send = _ => Task.FromResult(true);
	internal static int Calls, Active, MaximumActive;
	public static async Task<bool> PostBatchAsync(byte[] payload, bool gzip)
	{
		Interlocked.Increment(ref Calls);
		int active = Interlocked.Increment(ref Active);
		MaximumActive = Math.Max(MaximumActive, active);
		try
		{
			if (Thread.CurrentThread.ManagedThreadId == Autodesk.Revit.DB.ApiThread.Main) throw new Exception("HTTP on UI thread");
			using (var memory = new MemoryStream(payload))
			using (Stream stream = gzip ? (Stream)new GZipStream(memory, CompressionMode.Decompress) : memory)
			using (var reader = new StreamReader(stream, Encoding.UTF8))
				return await Send(JArray.Parse(reader.ReadToEnd())).ConfigureAwait(false);
		}
		finally { Interlocked.Decrement(ref Active); }
	}
}
