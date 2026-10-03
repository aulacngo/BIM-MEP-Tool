using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BIN;

public static class TelemetryHttpTransport
{
	private const string Endpoint = "https://mcp-revit-api.thuongdang531.workers.dev/api/telemetry/batch";
	private const string ApiKey = "bin_revit_ingest_secret_2026";

	// Legacy callers now join the same bounded batch outbox as all other telemetry.
	public static void PostJsonAsync(string jsonPayload)
	{
		if (!string.IsNullOrWhiteSpace(jsonPayload)) TelemetryBatchDispatcher.TryEnqueue(jsonPayload);
	}

	public static Task<bool> PostBatchAsync(byte[] payload, bool isGzip) => PostBatchToEndpointAsync(payload, isGzip, Endpoint);

	internal static async Task<bool> PostBatchToEndpointAsync(byte[] payload, bool isGzip, string endpoint)
	{
		if (payload == null || payload.Length == 0) return true;
		try
		{
			try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
			try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; } catch { }
#pragma warning disable SYSLIB0014 // HttpWebRequest keeps net48 and net8 implementations identical.
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
#pragma warning restore SYSLIB0014
			request.Headers["X-API-Key"] = ApiKey;
			if (isGzip) request.Headers["Content-Encoding"] = "gzip";
			try
			{
				request.Proxy = WebRequest.DefaultWebProxy;
				if (request.Proxy != null) request.Proxy.Credentials = CredentialCache.DefaultCredentials;
			}
			catch { }
			request.Method = "POST";
			request.ContentType = "application/json; charset=utf-8";
			request.UserAgent = "BIN-Revit-Tool/1.0";
			request.Timeout = 3000;
			request.ReadWriteTimeout = 3000;
			request.ContentLength = payload.Length;
			// Timeout/ReadWriteTimeout do not bound the async API: abort the request on deadline.
			using (var deadline = new Timer(_ => { try { request.Abort(); } catch { } }, null, 5000, Timeout.Infinite))
			{
				using (Stream stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
				{
					await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
				}
				using (WebResponse response = await request.GetResponseAsync().ConfigureAwait(false)) { }
			}
			return true;
		}
		catch (WebException wex)
		{
			try { if (wex.Response != null) wex.Response.Dispose(); } catch { }
		}
		catch { }
		return false;
	}
}
