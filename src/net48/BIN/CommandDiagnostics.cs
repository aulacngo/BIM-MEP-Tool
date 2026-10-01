using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;

namespace BIN;

public static class CommandDiagnostics
{
	private static readonly object Sync = new object();
	private static readonly string DirectoryPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIM TOOL", "Diagnostics");
	private static readonly string LegacyDirectoryPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIN TOOL", "Diagnostics");
	private static readonly string LogPath = Path.Combine(DirectoryPath, "commands.jsonl");
	private static readonly string LegacyLogPath = Path.Combine(LegacyDirectoryPath, "commands.jsonl");

	private static string GetReadableLogPath()
	{
		return File.Exists(LogPath) || !File.Exists(LegacyLogPath) ? LogPath : LegacyLogPath;
	}

	public static void Write(string command, string stage, ExternalCommandData data, Result? result = null, string message = null, Exception error = null, string assemblyPath = null)
	{
		try
		{
			// Snapshot Revit and execution state on the calling thread; workers use only strings.
			string time = DateTime.Now.ToString("O");
			UIDocument uidoc = data?.Application?.ActiveUIDocument;
			Document doc = uidoc?.Document;
			ICollection<ElementId> selectedIds = uidoc?.Selection?.GetElementIds();
			string ids = selectedIds == null ? "" : string.Join(",", selectedIds.Select(id => id.IntegerValue));
			string resultText = result?.ToString() ?? "";
			string documentTitle = doc?.Title;
			string viewName = doc?.ActiveView?.Name ?? "";
			string exceptionType = error?.GetType().FullName;
			string exceptionMessage = error?.Message ?? "";
			string stackTrace = error?.ToString();
			string userName = doc?.Application?.Username ?? Environment.UserName;

			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					string json = "{" +
						$"\"time\":\"{time}\"," +
						$"\"command\":\"{Escape(command)}\"," +
						$"\"stage\":\"{Escape(stage)}\"," +
						$"\"result\":\"{Escape(resultText)}\"," +
						$"\"document\":\"{Escape(documentTitle)}\"," +
						$"\"view\":\"{Escape(viewName)}\"," +
						$"\"selectionIds\":[{ids}]," +
						$"\"message\":\"{Escape(message)}\"," +
						$"\"assembly\":\"{Escape(assemblyPath)}\"," +
						$"\"exceptionType\":\"{Escape(exceptionType)}\"," +
						$"\"exception\":\"{Escape(exceptionMessage)}\"," +
						$"\"stackTrace\":\"{Escape(stackTrace)}\"" +
						"}";
					lock (Sync)
					{
						Directory.CreateDirectory(DirectoryPath);
						File.AppendAllText(LogPath, json + Environment.NewLine, Encoding.UTF8);
					}
				}
				catch { }
			});

			if (stage == "completed" || stage == "failed")
			{
				SendTelemetryAsync(command, stage, documentTitle ?? "Unknown Project", userName, viewName, resultText, message, exceptionMessage);
			}
		}
		catch { }
	}

	private static void SendTelemetryAsync(string command, string stage, string projectName, string userName, string viewName, string resultText, string message, string exceptionMessage)
	{
		try
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					string json = JsonConvert.SerializeObject(new
					{
						project_name = projectName,
						user_name = userName,
						command_name = command ?? "",
						details = new
						{
							stage,
							result = resultText,
							view = viewName,
							message = message ?? "",
							exception = exceptionMessage
						}
					});
					TelemetryHttpTransport.PostJsonAsync(json);
				}
				catch { }
			});
		}
		catch { }
	}

	public static string ReadLatestJson(int count)
	{
		count = Math.Max(1, Math.Min(count, 1000));
		try
		{
			lock (Sync)
			{
				string readableLogPath = GetReadableLogPath();
				if (!File.Exists(readableLogPath)) return "{\"count\":0,\"events\":[]}";
				string[] lines = File.ReadAllLines(readableLogPath, Encoding.UTF8).Where(line => !string.IsNullOrWhiteSpace(line)).Reverse().Take(count).Reverse().ToArray();
				return $"{{\"count\":{lines.Length},\"logPath\":\"{Escape(readableLogPath)}\",\"events\":[{string.Join(",", lines)}]}}";
			}
		}
		catch (Exception ex) { return $"{{\"error\":\"{Escape(ex.Message)}\"}}"; }
	}

	public static string ReadLastErrorJson()
	{
		try
		{
			lock (Sync)
			{
				string readableLogPath = GetReadableLogPath();
				if (!File.Exists(readableLogPath)) return "{\"error\":null}";
				string line = File.ReadLines(readableLogPath, Encoding.UTF8).Reverse().FirstOrDefault(value => value.Contains("\"stage\":\"failed\""));
				return line == null ? "{\"error\":null}" : "{\"error\":" + line + "}";
			}
		}
		catch (Exception ex) { return $"{{\"error\":\"{Escape(ex.Message)}\"}}"; }
	}

	public static string ReadLatestJournalJson(int lineCount)
	{
		lineCount = Math.Max(1, Math.Min(lineCount, 2000));
		try
		{
			string revitRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Autodesk", "Revit");
			FileInfo journal = new DirectoryInfo(revitRoot).Exists ? new DirectoryInfo(revitRoot).GetFiles("journal.*.txt", SearchOption.AllDirectories).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault() : null;
			if (journal == null) return "{\"error\":\"No Revit journal found\"}";
			string[] lines = File.ReadAllLines(journal.FullName).Reverse().Take(lineCount).Reverse().ToArray();
			return $"{{\"path\":\"{Escape(journal.FullName)}\",\"lineCount\":{lines.Length},\"text\":\"{Escape(string.Join(Environment.NewLine, lines))}\"}}";
		}
		catch (Exception ex) { return $"{{\"error\":\"{Escape(ex.Message)}\"}}"; }
	}

	public static string Escape(string value)
	{
		if (value == null) return "";
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
	}
}
