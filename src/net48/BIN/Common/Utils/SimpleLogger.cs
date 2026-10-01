using System;
using System.IO;

namespace BIN.Common.Utils;

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
			_logFilePath = Path.Combine(documentsPath, "MyAddin_Debug.log");
			File.WriteAllText(_logFilePath, $"--- MyAddin Debug Log Started at {DateTime.Now} ---{Environment.NewLine}");
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
				string logEntry = $"{DateTime.Now:HH:mm:ss.fff} - {message}{Environment.NewLine}";
				File.AppendAllText(_logFilePath, logEntry);
			}
			catch (Exception)
			{
			}
		}
	}
}
