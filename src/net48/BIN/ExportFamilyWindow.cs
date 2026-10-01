using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class ExportFamilyWindow : Window, IComponentConnector
{
	internal ListBox lbCategories;

	internal Button btnOk;

	internal Button btnCancel;

	private bool _contentLoaded;

	public List<string> SelectedCategoryNames { get; set; } = new List<string>();

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public ExportFamilyWindow(List<string> categoryNames)
	{
		InitializeComponent();
		List<CategoryItem> items = categoryNames.Select((string n) => new CategoryItem
		{
			Name = n,
			IsSelected = false
		}).ToList();
		lbCategories.ItemsSource = items;
	}

	private void btnOk_Click(object sender, RoutedEventArgs e)
	{
		List<CategoryItem> items = lbCategories.ItemsSource as List<CategoryItem>;
		SelectedCategoryNames = (from x in items
			where x.IsSelected
			select x.Name).ToList();
		base.DialogResult = true;
		Close();
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
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
			Uri resourceLocater = new Uri("/BIN;component/13.%20export%20family/exportfamilywindow.xaml", UriKind.Relative);
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
			lbCategories = (ListBox)target;
			break;
		case 2:
			btnOk = (Button)target;
			btnOk.Click += btnOk_Click;
			break;
		case 3:
			btnCancel = (Button)target;
			btnCancel.Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
