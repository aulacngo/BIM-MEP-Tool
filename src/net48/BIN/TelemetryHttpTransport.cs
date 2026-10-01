using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BIN;

public static class TelemetryHttpTransport
{
	private const string Endpoint = "https://mcp-revit-api.thuongdang531.workers.dev/";
	private const string ApiKey = "bin_revit_ingest_secret_2026";

	// Legacy callers now join the same bounded batch outbox as all other telemetry.
	public static void PostJsonAsync(string jsonPayload)
	{
		if (!string.IsNullOrWhiteSpace(jsonPayload)) TelemetryBatchDispatcher.TryEnqueue(jsonPayload);
	}

	// The factory executes away from the Revit API thread, then joins the bounded outbox.
	public static void PostJsonAsync(Func<string> jsonPayloadFactory)
	{
		if (jsonPayloadFactory == null) return;
		try
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try { PostJsonAsync(jsonPayloadFactory()); }
				catch { }
			});
		}
		catch { }
	}

	public static async Task PostBatchAsync(byte[] payload, bool isGzip)
	{
		if (payload == null || payload.Length == 0) return;
		try
		{
			try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
			try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; } catch { }
#pragma warning disable SYSLIB0014 // HttpWebRequest keeps net48 and net8 implementations identical.
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Endpoint);
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
			using (Stream stream = await request.GetRequestStreamAsync().ConfigureAwait(false))
			{
				await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
			}
			using (WebResponse response = await request.GetResponseAsync().ConfigureAwait(false)) { }
		}
		catch (WebException wex)
		{
			try { if (wex.Response != null) wex.Response.Dispose(); } catch { }
		}
		catch { }
	}
}
