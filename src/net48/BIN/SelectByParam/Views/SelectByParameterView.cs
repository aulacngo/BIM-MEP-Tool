using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using BIN.SelectByParam.ViewModels;

namespace BIN.SelectByParam.Views;

public class SelectByParameterView : Window, IComponentConnector
{
	private SelectByParameterViewModel _viewModel;

	internal ListBox ValueListBox;

	private bool _contentLoaded;

	public SelectByParameterView(SelectByParameterViewModel viewModel)
	{
		base.Resources.Add("EnumToBoolConverter", new EnumToBoolConverter());
		base.Resources.Add("InverseBoolConverter", new InverseBoolConverter());
		base.Resources.Add("NullToBoolConverter", new NullToBoolConverter());
		InitializeComponent();
		_viewModel = viewModel;
		base.DataContext = _viewModel;
		base.Loaded += OnWindowLoaded;
	}

	private void OnWindowLoaded(object sender, RoutedEventArgs e)
	{
		_viewModel?.InitialLoad();
	}

	private void ValueListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_viewModel == null)
		{
			return;
		}
		_viewModel.SelectedValues.Clear();
		foreach (object item in ValueListBox.SelectedItems)
		{
			_viewModel.SelectedValues.Add(item.ToString());
		}
		_viewModel.UpdateMatchingCount();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/39.%20select%20by%20parameter/view/selectbyparameterview.xaml", UriKind.Relative);
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
			ValueListBox = (ListBox)target;
			ValueListBox.SelectionChanged += ValueListBox_SelectionChanged;
		}
		else
		{
			_contentLoaded = true;
		}
	}
}
