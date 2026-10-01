using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Autodesk.Revit.DB;

namespace BIN;

public class CopySheetWindow : Window, IComponentConnector
{
	private List<CopySheetItem> _fullList;

	internal TextBox txtSearch;

	internal ComboBox cbDuplicateMode;

	internal TextBox txtCopyCount;

	internal ListView lvSheets;

	internal GridViewColumn colSheetName;

	internal TextBlock txtStatus;

	private bool _contentLoaded;

	public ObservableCollection<CopySheetItem> DisplayList { get; set; }

	public List<ViewSheet> ResultSheets { get; private set; }

	public int SelectedModeIndex { get; private set; }

	public int CopyCount { get; private set; } = 1;

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public CopySheetWindow(List<ViewSheet> sheets)
	{
		InitializeComponent();
		_fullList = (from s in sheets
			select new CopySheetItem(s) into x
			orderby x.Number
			select x).ToList();
		DisplayList = new ObservableCollection<CopySheetItem>(_fullList);
		lvSheets.ItemsSource = DisplayList;
		base.Loaded += delegate
		{
			AutoSizeColumns();
		};
		lvSheets.SizeChanged += delegate
		{
			AutoSizeColumns();
		};
	}

	private void AutoSizeColumns()
	{
		double totalWidth = lvSheets.ActualWidth;
		if (!(totalWidth <= 0.0) && lvSheets.View is GridView gv)
		{
			double remainingWidth = totalWidth - gv.Columns[0].Width - gv.Columns[1].Width - 35.0;
			if (remainingWidth > 0.0)
			{
				colSheetName.Width = remainingWidth;
			}
		}
	}

	private void FilterChanged(object sender, TextChangedEventArgs e)
	{
		if (_fullList == null)
		{
			return;
		}
		string search = txtSearch.Text.ToLower();
		IEnumerable<CopySheetItem> filtered = _fullList.Where((CopySheetItem i) => string.IsNullOrEmpty(search) || i.Name.ToLower().Contains(search) || i.Number.ToLower().Contains(search));
		DisplayList.Clear();
		foreach (CopySheetItem item in filtered)
		{
			DisplayList.Add(item);
		}
		txtStatus.Text = $"Showing {DisplayList.Count} of {_fullList.Count} sheets";
	}

	private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
	{
		foreach (CopySheetItem i in DisplayList)
		{
			i.IsSelected = true;
		}
		lvSheets.Items.Refresh();
	}

	private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
	{
		foreach (CopySheetItem i in DisplayList)
		{
			i.IsSelected = false;
		}
		lvSheets.Items.Refresh();
	}

	private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
	{
		Regex regex = new Regex("[^0-9]+");
		e.Handled = regex.IsMatch(e.Text);
	}

	private void BtnCopy_Click(object sender, RoutedEventArgs e)
	{
		ResultSheets = (from x in _fullList
			where x.IsSelected
			select x.SheetRef).ToList();
		if (ResultSheets.Count == 0)
		{
			MessageBox.Show("Please select at least one sheet.", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (!int.TryParse(txtCopyCount.Text, out var count) || count <= 0)
		{
			MessageBox.Show("Please enter a valid number of copies (greater than 0).", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		CopyCount = count;
		SelectedModeIndex = cbDuplicateMode.SelectedIndex;
		base.DialogResult = true;
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/32.%20copy%20sheet/copysheetwindow.xaml", UriKind.Relative);
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
			txtSearch = (TextBox)target;
			txtSearch.TextChanged += FilterChanged;
			break;
		case 2:
			cbDuplicateMode = (ComboBox)target;
			break;
		case 3:
			txtCopyCount = (TextBox)target;
			txtCopyCount.PreviewTextInput += NumberValidationTextBox;
			break;
		case 4:
			lvSheets = (ListView)target;
			break;
		case 5:
			colSheetName = (GridViewColumn)target;
			break;
		case 6:
			((Button)target).Click += BtnSelectAll_Click;
			break;
		case 7:
			((Button)target).Click += BtnSelectNone_Click;
			break;
		case 8:
			((Button)target).Click += BtnCopy_Click;
			break;
		case 9:
			((Button)target).Click += BtnCancel_Click;
			break;
		case 10:
			txtStatus = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
