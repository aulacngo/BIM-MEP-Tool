using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using BIN;

internal static class Program
{
	private static async Task Main()
	{
		var probe = new TcpListener(IPAddress.Loopback, 0);
		probe.Start();
		int port = ((IPEndPoint)probe.LocalEndpoint).Port;
		probe.Stop();
		string endpoint = "http://127.0.0.1:" + port + "/";
		using (var listener = new HttpListener())
		{
			listener.Prefixes.Add(endpoint);
			listener.Start();
			var payload = Encoding.UTF8.GetBytes("[{\"test\":true}]");
			Task<HttpListenerContext> pending = listener.GetContextAsync();
			Task<bool> request = TelemetryHttpTransport.PostBatchToEndpointAsync(payload, false, endpoint);
			HttpListenerContext context = await pending;
			using (var reader = new StreamReader(context.Request.InputStream))
				Assert(await reader.ReadToEndAsync() == Encoding.UTF8.GetString(payload), "Exact request body");
			Assert(!string.IsNullOrEmpty(context.Request.Headers["X-API-Key"]), "Authentication header supplied");
			context.Response.StatusCode = 202;
			context.Response.Close();
			Assert(await request, "202 acknowledged");

			pending = listener.GetContextAsync();
			request = TelemetryHttpTransport.PostBatchToEndpointAsync(payload, true, endpoint);
			context = await pending;
			Assert(context.Request.Headers["Content-Encoding"] == "gzip", "Gzip header supplied");
			context.Response.StatusCode = 503;
			context.Response.Close();
			Assert(!await request, "503 reported as unacknowledged, response disposed");

			pending = listener.GetContextAsync();
			var timer = Stopwatch.StartNew();
			request = TelemetryHttpTransport.PostBatchToEndpointAsync(payload, false, endpoint);
			context = await pending; // Deliberately withhold HTTP response headers.
			Task completed = await Task.WhenAny(request, Task.Delay(8000));
			Assert(completed == request && !await request, "Stalled async request aborted");
			// net8 may honor request.Timeout (3 s) before the explicit 5 s abort fires.
			Assert(timer.ElapsedMilliseconds >= 1000 && timer.ElapsedMilliseconds < 6500, "Bounded total deadline");
			try { context.Response.Close(); } catch { }
			Console.WriteLine("PASS: success, HTTP failure, gzip header and stalled response deadline=" + timer.ElapsedMilliseconds + " ms; loopback only");
		}
	}
	private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
}

namespace BIN
{
	public static class TelemetryBatchDispatcher
	{
		public static bool TryEnqueue(string value) => true;
	}
}
