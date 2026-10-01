using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace BIN;

public static class TelemetryHttpTransport
{
	public static void PostJsonAsync(string jsonPayload)
	{
		if (string.IsNullOrWhiteSpace(jsonPayload)) return;
		PostJsonAsync(() => jsonPayload);
	}

	// The payload factory runs on the worker so callers can snapshot Revit values on
	// the API thread and defer JSON serialization together with the HTTP request.
	public static void PostJsonAsync(Func<string> jsonPayloadFactory)
	{
		if (jsonPayloadFactory == null) return;
		try
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					string jsonPayload = jsonPayloadFactory();
					if (string.IsNullOrWhiteSpace(jsonPayload)) return;
					byte[] payload = Encoding.UTF8.GetBytes(jsonPayload);
					try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
					// Enable TLS 1.3 separately so unsupported runtimes retain TLS 1.2.
					try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288; } catch { }
#pragma warning disable SYSLIB0014 // HttpWebRequest keeps both target implementations compatible with .NET Framework.
					HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://mcp-revit-api.thuongdang531.workers.dev/");
#pragma warning restore SYSLIB0014
					request.Headers["X-API-Key"] = "bin_revit_ingest_secret_2026";
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
					using (Stream stream = request.GetRequestStream())
					{
						stream.Write(payload, 0, payload.Length);
					}
					using (WebResponse response = request.GetResponse()) { }
				}
				catch (WebException wex)
				{
					try { wex.Response?.Dispose(); } catch { }
				}
				catch { }
			});
		}
		catch { }
	}
}
