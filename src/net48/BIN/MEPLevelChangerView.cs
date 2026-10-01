using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class MEPLevelChangerView : Window, IComponentConnector, IStyleConnector
{
	internal DataGrid CategoryGrid;

	private bool _contentLoaded;

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public MEPLevelChangerView(object viewModel)
	{
		InitializeComponent();
		Title = "BIM TOOL – MEP Level Changer";
		base.DataContext = viewModel;
	}

	private void CategoryCheckBox_Click(object sender, RoutedEventArgs e)
	{
		CheckBox checkBox = sender as CheckBox;
		if (!(checkBox?.DataContext is MEPCategoryItem currentItem) || !CategoryGrid.SelectedItems.Contains(currentItem))
		{
			return;
		}
		bool newState = checkBox.IsChecked == true;
		foreach (object item in CategoryGrid.SelectedItems)
		{
			if (item is MEPCategoryItem catItem)
			{
				catItem.IsChecked = newState;
			}
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/23.%20change%20reference%20level/meplevelchangerview.xaml", UriKind.Relative);
			global::BIN.EmbeddedBamlLoader.LoadComponent(this, resourceLocater);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 1)
		{
			CategoryGrid = (DataGrid)target;
		}
		else
		{
			_contentLoaded = true;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		if (connectionId == 2)
		{
			((CheckBox)target).Click += CategoryCheckBox_Click;
		}
	}
}
