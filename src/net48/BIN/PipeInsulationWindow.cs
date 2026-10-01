using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class PipeInsulationWindow : Window, IComponentConnector, IStyleConnector
{
	private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIN_PipeInsulation");

	private static readonly string SettingsFile = Path.Combine(SettingsDir, "thickness_table.json");

	internal ComboBox cmbInsulationType;

	internal TextBox tbPipeSize;

	internal TextBox tbInsThickness;

	internal Button btnAddRow;

	internal DataGrid dgThicknessTable;

	internal CheckBox chkRemoveExisting;

	internal RadioButton rbAllPipes;

	internal RadioButton rbSelectedPipes;

	private bool _contentLoaded;

	public ObservableCollection<ThicknessEntry> ThicknessEntries { get; set; }

	public InsulationTypeItem SelectedInsulationType { get; set; }

	public bool RemoveExisting { get; set; }

	public bool ApplyToAll { get; set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public PipeInsulationWindow(List<InsulationTypeItem> insulationTypes)
	{
		InitializeComponent();
		ThicknessEntries = new ObservableCollection<ThicknessEntry>();
		if (!LoadThicknessTable())
		{
			LoadDefaultThicknessTable();
		}
		dgThicknessTable.ItemsSource = ThicknessEntries;
		cmbInsulationType.ItemsSource = insulationTypes;
		if (insulationTypes.Count > 0)
		{
			cmbInsulationType.SelectedIndex = 0;
		}
	}

	private void LoadDefaultThicknessTable()
	{
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 15.0,
			InsulationThicknessMM = 25.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 20.0,
			InsulationThicknessMM = 25.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 25.0,
			InsulationThicknessMM = 25.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 32.0,
			InsulationThicknessMM = 30.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 40.0,
			InsulationThicknessMM = 30.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 50.0,
			InsulationThicknessMM = 30.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 65.0,
			InsulationThicknessMM = 40.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 80.0,
			InsulationThicknessMM = 40.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 100.0,
			InsulationThicknessMM = 40.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 125.0,
			InsulationThicknessMM = 50.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 150.0,
			InsulationThicknessMM = 50.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 200.0,
			InsulationThicknessMM = 50.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 250.0,
			InsulationThicknessMM = 60.0
		});
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = 300.0,
			InsulationThicknessMM = 60.0
		});
	}

	private bool LoadThicknessTable()
	{
		try
		{
			if (!File.Exists(SettingsFile))
			{
				return false;
			}
			string json = File.ReadAllText(SettingsFile);
			List<ThicknessEntry> entries = ParseThicknessJson(json);
			if (entries == null || entries.Count == 0)
			{
				return false;
			}
			foreach (ThicknessEntry entry in entries)
			{
				ThicknessEntries.Add(entry);
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public void SaveThicknessTable()
	{
		try
		{
			if (!Directory.Exists(SettingsDir))
			{
				Directory.CreateDirectory(SettingsDir);
			}
			List<string> lines = new List<string>();
			lines.Add("[");
			for (int i = 0; i < ThicknessEntries.Count; i++)
			{
				ThicknessEntry e = ThicknessEntries[i];
				string comma = ((i < ThicknessEntries.Count - 1) ? "," : "");
				lines.Add($"  {{\"PipeSizeMM\":{e.PipeSizeMM},\"InsulationThicknessMM\":{e.InsulationThicknessMM}}}{comma}");
			}
			lines.Add("]");
			File.WriteAllText(SettingsFile, string.Join(Environment.NewLine, lines));
		}
		catch
		{
		}
	}

	private List<ThicknessEntry> ParseThicknessJson(string json)
	{
		List<ThicknessEntry> result = new List<ThicknessEntry>();
		json = json.Trim();
		if (!json.StartsWith("[") || !json.EndsWith("]"))
		{
			return null;
		}
		int depth = 0;
		int objStart = -1;
		for (int i = 0; i < json.Length; i++)
		{
			if (json[i] == '{')
			{
				if (depth == 0)
				{
					objStart = i;
				}
				depth++;
			}
			else
			{
				if (json[i] != '}')
				{
					continue;
				}
				depth--;
				if (depth == 0 && objStart >= 0)
				{
					string objStr = json.Substring(objStart, i - objStart + 1);
					ThicknessEntry entry = ParseSingleEntry(objStr);
					if (entry != null)
					{
						result.Add(entry);
					}
					objStart = -1;
				}
			}
		}
		return result;
	}

	private ThicknessEntry ParseSingleEntry(string obj)
	{
		try
		{
			double pipeSize = 0.0;
			double thickness = 0.0;
			int idx = obj.IndexOf("\"PipeSizeMM\"");
			if (idx >= 0)
			{
				int colonIdx = obj.IndexOf(':', idx);
				if (colonIdx >= 0)
				{
					string numStr = ExtractNumber(obj, colonIdx + 1);
					double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out pipeSize);
				}
			}
			idx = obj.IndexOf("\"InsulationThicknessMM\"");
			if (idx >= 0)
			{
				int colonIdx2 = obj.IndexOf(':', idx);
				if (colonIdx2 >= 0)
				{
					string numStr2 = ExtractNumber(obj, colonIdx2 + 1);
					double.TryParse(numStr2, NumberStyles.Any, CultureInfo.InvariantCulture, out thickness);
				}
			}
			if (pipeSize > 0.0)
			{
				return new ThicknessEntry
				{
					PipeSizeMM = pipeSize,
					InsulationThicknessMM = thickness
				};
			}
		}
		catch
		{
		}
		return null;
	}

	private string ExtractNumber(string s, int startIdx)
	{
		int start = -1;
		int end = -1;
		for (int i = startIdx; i < s.Length; i++)
		{
			char c = s[i];
			if ((c >= '0' && c <= '9') || c == '.' || c == '-')
			{
				if (start < 0)
				{
					start = i;
				}
				end = i;
			}
			else if (start >= 0)
			{
				break;
			}
		}
		if (start >= 0 && end >= start)
		{
			return s.Substring(start, end - start + 1);
		}
		return "";
	}

	private void BtnAddRow_Click(object sender, RoutedEventArgs e)
	{
		if (!double.TryParse(tbPipeSize.Text, out var pipeSize) || pipeSize <= 0.0)
		{
			MessageBox.Show("Vui lòng nhập Pipe Size hợp lệ (số dương, mm)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (!double.TryParse(tbInsThickness.Text, out var thickness) || thickness <= 0.0)
		{
			MessageBox.Show("Vui lòng nhập Insulation Thickness hợp lệ (số dương, mm)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		foreach (ThicknessEntry entry in ThicknessEntries)
		{
			if (Math.Abs(entry.PipeSizeMM - pipeSize) < 0.001)
			{
				entry.InsulationThicknessMM = thickness;
				tbPipeSize.Text = "";
				tbInsThickness.Text = "";
				return;
			}
		}
		ThicknessEntries.Add(new ThicknessEntry
		{
			PipeSizeMM = pipeSize,
			InsulationThicknessMM = thickness
		});
		List<ThicknessEntry> sorted = new List<ThicknessEntry>(ThicknessEntries);
		sorted.Sort((ThicknessEntry a, ThicknessEntry b) => a.PipeSizeMM.CompareTo(b.PipeSizeMM));
		ThicknessEntries.Clear();
		foreach (ThicknessEntry item in sorted)
		{
			ThicknessEntries.Add(item);
		}
		tbPipeSize.Text = "";
		tbInsThickness.Text = "";
	}

	private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
	{
		if (((sender is Button btn) ? btn.DataContext : null) is ThicknessEntry entry)
		{
			ThicknessEntries.Remove(entry);
		}
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (cmbInsulationType.SelectedItem == null)
		{
			MessageBox.Show("Vui lòng chọn Insulation Type!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (ThicknessEntries.Count == 0)
		{
			MessageBox.Show("Vui lòng nhập ít nhất một dòng trong bảng độ dày!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		SelectedInsulationType = cmbInsulationType.SelectedItem as InsulationTypeItem;
		RemoveExisting = chkRemoveExisting.IsChecked == true;
		ApplyToAll = rbAllPipes.IsChecked == true;
		SaveThicknessTable();
		base.DialogResult = true;
		Close();
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/35.%20pipe%20insulation/pipeinsulationwindow.xaml", UriKind.Relative);
			global::BIN.EmbeddedBamlLoader.LoadComponent(this, resourceLocater);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			cmbInsulationType = (ComboBox)target;
			break;
		case 2:
			tbPipeSize = (TextBox)target;
			break;
		case 3:
			tbInsThickness = (TextBox)target;
			break;
		case 4:
			btnAddRow = (Button)target;
			btnAddRow.Click += BtnAddRow_Click;
			break;
		case 5:
			dgThicknessTable = (DataGrid)target;
			break;
		case 7:
			chkRemoveExisting = (CheckBox)target;
			break;
		case 8:
			rbAllPipes = (RadioButton)target;
			break;
		case 9:
			rbSelectedPipes = (RadioButton)target;
			break;
		case 10:
			((Button)target).Click += BtnOk_Click;
			break;
		case 11:
			((Button)target).Click += BtnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 6)
		{
			((Button)target).Click += BtnDeleteRow_Click;
		}
	}
}
