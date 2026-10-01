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

public class CopyViewWindow : Window, IComponentConnector
{
	private List<ViewItem> _fullList;

	internal TextBox txtSearch;

	internal ComboBox cbViewType;

	internal ListView lvViews;

	internal GridViewColumn colViewName;

	internal TextBox txtNumCopies;

	private bool _contentLoaded;

	public ObservableCollection<ViewItem> DisplayList { get; set; }

	public List<View> ResultViews { get; private set; }

	public int NumCopies { get; private set; } = 1;

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public CopyViewWindow(List<View> views)
	{
		InitializeComponent();
		_fullList = views.Select((View v) => new ViewItem(v)).ToList();
		DisplayList = new ObservableCollection<ViewItem>(_fullList);
		lvViews.ItemsSource = DisplayList;
		List<string> types = (from t in _fullList.Select((ViewItem x) => x.ViewType).Distinct()
			orderby t
			select t).ToList();
		types.Insert(0, "All Views");
		cbViewType.ItemsSource = types;
		cbViewType.SelectedIndex = 0;
		base.Loaded += delegate
		{
			AutoSizeColumns();
		};
		lvViews.SizeChanged += delegate
		{
			AutoSizeColumns();
		};
	}

	private void AutoSizeColumns()
	{
		double totalWidth = lvViews.ActualWidth;
		if (!(totalWidth <= 0.0) && lvViews.View is GridView gv)
		{
			double remainingWidth = totalWidth - gv.Columns[0].Width - gv.Columns[1].Width - 25.0;
			if (remainingWidth > 0.0)
			{
				colViewName.Width = remainingWidth;
			}
		}
	}

	private void FilterChanged(object sender, EventArgs e)
	{
		if (_fullList == null)
		{
			return;
		}
		string search = txtSearch.Text.ToLower();
		string type = cbViewType.SelectedItem?.ToString();
		IEnumerable<ViewItem> filtered = _fullList.Where((ViewItem i) => (type == "All Views" || i.ViewType == type) && (string.IsNullOrEmpty(search) || i.Name.ToLower().Contains(search)));
		DisplayList.Clear();
		foreach (ViewItem item in filtered)
		{
			DisplayList.Add(item);
		}
	}

	private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
	{
		foreach (ViewItem i in DisplayList)
		{
			i.IsSelected = true;
		}
		lvViews.Items.Refresh();
	}

	private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
	{
		foreach (ViewItem i in DisplayList)
		{
			i.IsSelected = false;
		}
		lvViews.Items.Refresh();
	}

	private void BtnCopy_Click(object sender, RoutedEventArgs e)
	{
		if (int.TryParse(txtNumCopies.Text, out var num) && num > 0)
		{
			if (num > 100)
			{
				MessageBox.Show("Số bản sao quá lớn (tối đa 100).", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			NumCopies = num;
			ResultViews = (from x in _fullList
				where x.IsSelected
				select x.ViewRef).ToList();
			if (ResultViews.Count == 0)
			{
				MessageBox.Show("Vui lòng chọn ít nhất một View để nhân bản.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			}
			else
			{
				base.DialogResult = true;
			}
		}
		else
		{
			MessageBox.Show("Vui lòng nhập số nguyên dương hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
	{
		Regex regex = new Regex("[^0-9]+");
		e.Handled = regex.IsMatch(e.Text);
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
			Uri resourceLocater = new Uri("/BIN;component/22.%20copy%20view/copyviewwindow.xaml", UriKind.Relative);
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
			cbViewType = (ComboBox)target;
			cbViewType.SelectionChanged += FilterChanged;
			break;
		case 3:
			lvViews = (ListView)target;
			break;
		case 4:
			colViewName = (GridViewColumn)target;
			break;
		case 5:
			((Button)target).Click += BtnSelectAll_Click;
			break;
		case 6:
			((Button)target).Click += BtnSelectNone_Click;
			break;
		case 7:
			((Button)target).Click += BtnCopy_Click;
			break;
		case 8:
			((Button)target).Click += BtnCancel_Click;
			break;
		case 9:
			txtNumCopies = (TextBox)target;
			txtNumCopies.PreviewTextInput += NumberValidationTextBox;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
