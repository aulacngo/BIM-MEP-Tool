using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class TurnMEPWindow : Window, IComponentConnector
{
	internal RadioButton rbPipe;

	internal RadioButton rbDuct;

	internal RadioButton rbCableTray;

	internal TextBox txtOffset;

	internal RadioButton rb45;

	internal RadioButton rb90;

	internal RadioButton rbUp;

	internal RadioButton rbDown;

	internal RadioButton rbLeft;

	internal RadioButton rbRight;

	private bool _contentLoaded;

	public int MEPType { get; set; }

	public double OffsetValue { get; set; }

	public double SelectedAngle { get; set; }

	public string SelectedDirection { get; set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public TurnMEPWindow()
	{
		InitializeComponent();
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (!double.TryParse(txtOffset.Text, out var offset))
		{
			MessageBox.Show("Vui lòng nhập giá trị offset là số!", "Thông báo");
			return;
		}
		OffsetValue = offset;
		if (rbPipe.IsChecked == true)
		{
			MEPType = 0;
		}
		else if (rbDuct.IsChecked == true)
		{
			MEPType = 1;
		}
		else if (rbCableTray.IsChecked == true)
		{
			MEPType = 2;
		}
		SelectedAngle = ((rb45.IsChecked == true) ? 45 : 90);
		if (rbUp.IsChecked == true)
		{
			SelectedDirection = "UP";
		}
		else if (rbDown.IsChecked == true)
		{
			SelectedDirection = "DOWN";
		}
		else if (rbLeft.IsChecked == true)
		{
			SelectedDirection = "LEFT";
		}
		else if (rbRight.IsChecked == true)
		{
			SelectedDirection = "RIGHT";
		}
		base.DialogResult = true;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/29.%20turn%20mep/turnmepwindow.xaml", UriKind.Relative);
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
			rbPipe = (RadioButton)target;
			break;
		case 2:
			rbDuct = (RadioButton)target;
			break;
		case 3:
			rbCableTray = (RadioButton)target;
			break;
		case 4:
			txtOffset = (TextBox)target;
			break;
		case 5:
			rb45 = (RadioButton)target;
			break;
		case 6:
			rb90 = (RadioButton)target;
			break;
		case 7:
			rbUp = (RadioButton)target;
			break;
		case 8:
			rbDown = (RadioButton)target;
			break;
		case 9:
			rbLeft = (RadioButton)target;
			break;
		case 10:
			rbRight = (RadioButton)target;
			break;
		case 11:
			((Button)target).Click += BtnOk_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
