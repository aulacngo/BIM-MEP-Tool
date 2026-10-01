using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class ToggleInsulationWindow : Window, IComponentConnector
{
	internal CheckBox chkPipe;

	internal CheckBox chkDuct;

	internal RadioButton rbShow;

	internal RadioButton rbTransparent;

	internal RadioButton rbHide;

	private bool _contentLoaded;

	public int SelectedOption { get; set; }

	public bool IsPipeInsulation { get; set; }

	public bool IsDuctInsulation { get; set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public ToggleInsulationWindow()
	{
		InitializeComponent();
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		IsPipeInsulation = chkPipe.IsChecked == true;
		IsDuctInsulation = chkDuct.IsChecked == true;
		if (!IsPipeInsulation && !IsDuctInsulation)
		{
			MessageBox.Show("Vui lòng chọn ít nhất một loại Insulation!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (rbShow.IsChecked == true)
		{
			SelectedOption = 0;
		}
		else if (rbTransparent.IsChecked == true)
		{
			SelectedOption = 1;
		}
		else if (rbHide.IsChecked == true)
		{
			SelectedOption = 2;
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
			Uri resourceLocater = new Uri("/BIN;component/26.%20toggle%20insulation/toggleinsulationwindow.xaml", UriKind.Relative);
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
			chkPipe = (CheckBox)target;
			break;
		case 2:
			chkDuct = (CheckBox)target;
			break;
		case 3:
			rbShow = (RadioButton)target;
			break;
		case 4:
			rbTransparent = (RadioButton)target;
			break;
		case 5:
			rbHide = (RadioButton)target;
			break;
		case 6:
			((Button)target).Click += BtnOk_Click;
			break;
		case 7:
			((Button)target).Click += BtnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
