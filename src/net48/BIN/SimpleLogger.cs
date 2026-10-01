#define TRACE
using System;
using System.Diagnostics;
using System.IO;

namespace BIN;

public static class SimpleLogger
{
	private static readonly string _logFilePath;

	private static readonly object _lock;

	static SimpleLogger()
	{
		_lock = new object();
		try
		{
			string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			_logFilePath = Path.Combine(documentsPath, "BIN_Debug.log");
			if (File.Exists(_logFilePath))
			{
				return;
			}
			using FileStream fs = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
			using StreamWriter sw = new StreamWriter(fs);
			sw.WriteLine($"--- BIN Debug Log Started at {DateTime.Now} ---");
		}
		catch (Exception)
		{
			_logFilePath = null;
		}
	}

	public static void Log(string message)
	{
		if (_logFilePath == null)
		{
			return;
		}
		lock (_lock)
		{
			try
			{
				using FileStream fs = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
				using StreamWriter sw = new StreamWriter(fs);
				sw.WriteLine($"{DateTime.Now:HH:mm:ss.fff} - {message}");
			}
			catch (Exception)
			{
				Trace.WriteLine("[BIN] " + message);
			}
		}
	}
}
