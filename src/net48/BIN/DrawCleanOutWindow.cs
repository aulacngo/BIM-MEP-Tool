using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class DrawCleanOutWindow : Window, IComponentConnector
{
	internal RadioButton rbCeiling;

	internal RadioButton rbFloor;

	internal StackPanel spOffsetElbow;

	internal TextBox txtOffsetElbow;

	private bool _contentLoaded;

	public int SelectedType { get; set; }

	public double OffsetElbow { get; set; } = 0.49212598425196846;

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public DrawCleanOutWindow()
	{
		InitializeComponent();
	}

	private void rbCeiling_Checked(object sender, RoutedEventArgs e)
	{
		if (spOffsetElbow != null)
		{
			spOffsetElbow.Visibility = System.Windows.Visibility.Collapsed;
		}
	}

	private void rbFloor_Checked(object sender, RoutedEventArgs e)
	{
		if (spOffsetElbow != null)
		{
			spOffsetElbow.Visibility = System.Windows.Visibility.Visible;
		}
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (rbCeiling.IsChecked == true)
		{
			SelectedType = 0;
		}
		else if (rbFloor.IsChecked == true)
		{
			SelectedType = 1;
		}
		if (double.TryParse(txtOffsetElbow.Text, out var offsetMm) && offsetMm > 0.0)
		{
			OffsetElbow = offsetMm / 304.8;
		}
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
			Uri resourceLocater = new Uri("/BIN;component/27.%20draw%20clean%20out/drawcleanoutwindow.xaml", UriKind.Relative);
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
			rbCeiling = (RadioButton)target;
			rbCeiling.Checked += rbCeiling_Checked;
			break;
		case 2:
			rbFloor = (RadioButton)target;
			rbFloor.Checked += rbFloor_Checked;
			break;
		case 3:
			spOffsetElbow = (StackPanel)target;
			break;
		case 4:
			txtOffsetElbow = (TextBox)target;
			break;
		case 5:
			((Button)target).Click += BtnOk_Click;
			break;
		case 6:
			((Button)target).Click += BtnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
