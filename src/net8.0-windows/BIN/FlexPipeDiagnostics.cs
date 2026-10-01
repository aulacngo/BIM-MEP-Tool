using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BIN;

public static class FlexPipeDiagnostics
{
	private static readonly object FileLock = new object();
	private static readonly string DiagnosticFolder = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"BIM TOOL", "Diagnostics");
	private static readonly string LegacyDiagnosticFolder = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"BIN TOOL", "Diagnostics");
	private static readonly string EnabledPath = Path.Combine(DiagnosticFolder, "sprinkler-flex-pipe.enabled");
	private static readonly string LegacyEnabledPath = Path.Combine(LegacyDiagnosticFolder, "sprinkler-flex-pipe.enabled");
	public static readonly string LogPath = Path.Combine(DiagnosticFolder, "sprinkler-flex-pipe.jsonl");
	private static readonly string LegacyLogPath = Path.Combine(LegacyDiagnosticFolder, "sprinkler-flex-pipe.jsonl");

	public static bool IsEnabled => File.Exists(EnabledPath) || File.Exists(LegacyEnabledPath);

	public static void Start()
	{
		lock (FileLock)
		{
			Directory.CreateDirectory(DiagnosticFolder);
			File.WriteAllText(LogPath, "", Encoding.UTF8);
			File.WriteAllText(EnabledPath, DateTime.UtcNow.ToString("O"), Encoding.UTF8);
		}
		Write("diagnostic", "started", "Diagnostic session started");
	}

	public static void Stop()
	{
		Write("diagnostic", "stopped", "Diagnostic session stopped");
		lock (FileLock)
		{
			if (File.Exists(EnabledPath)) File.Delete(EnabledPath);
			if (File.Exists(LegacyEnabledPath)) File.Delete(LegacyEnabledPath);
		}
	}

	public static void Write(string stage, string status, string message,
		int? pipeId = null, int? sprinklerId = null, string details = null)
	{
		try
		{
			if (!IsEnabled) return;
			string json = "{" +
				$"\"time\":\"{Escape(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffK"))}\"," +
				$"\"stage\":\"{Escape(stage)}\"," +
				$"\"status\":\"{Escape(status)}\"," +
				$"\"message\":\"{Escape(message)}\"," +
				$"\"pipeId\":{(pipeId.HasValue ? pipeId.Value.ToString() : "null")}," +
				$"\"sprinklerId\":{(sprinklerId.HasValue ? sprinklerId.Value.ToString() : "null")}," +
				$"\"details\":\"{Escape(details)}\"" +
				"}";
			lock (FileLock)
			{
				Directory.CreateDirectory(DiagnosticFolder);
				File.AppendAllText(LogPath, json + Environment.NewLine, Encoding.UTF8);
			}
		}
		catch
		{
			// Diagnostics must never interrupt a Revit transaction.
		}
	}

	public static string ReadLatestJson(int maxLines = 200)
	{
		try
		{
			string[] lines;
			string readableLogPath;
			lock (FileLock)
			{
				readableLogPath = File.Exists(LogPath) || !File.Exists(LegacyLogPath) ? LogPath : LegacyLogPath;
				lines = File.Exists(readableLogPath) ? File.ReadAllLines(readableLogPath, Encoding.UTF8) : Array.Empty<string>();
			}
			IEnumerable<string> latest = lines.Where(x => !string.IsNullOrWhiteSpace(x))
				.Skip(Math.Max(0, lines.Length - Math.Max(1, Math.Min(maxLines, 2000))));
			return $"{{\"enabled\":{IsEnabled.ToString().ToLowerInvariant()}," +
				$"\"path\":\"{Escape(readableLogPath)}\",\"events\":[{string.Join(",", latest)}]}}";
		}
		catch (Exception ex)
		{
			return $"{{\"enabled\":{IsEnabled.ToString().ToLowerInvariant()},\"error\":\"{Escape(ex.Message)}\"}}";
		}
	}

	private static string Escape(string value)
	{
		if (value == null) return "";
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"")
			.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
	}
}
