using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class TurnDrainagePipeWindow : Window, IComponentConnector
{
	internal TextBox txtOffsetElbow;

	internal TextBox txtOffsetPipe;

	internal RadioButton rbUp;

	internal RadioButton rbDown;

	internal RadioButton rbLeft;

	internal RadioButton rbRight;

	private bool _contentLoaded;

	public double OffsetElbowValue { get; private set; }

	public double OffsetPipeValue { get; private set; }

	public string SelectedDirection { get; private set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public TurnDrainagePipeWindow()
	{
		InitializeComponent();
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (double.TryParse(txtOffsetElbow.Text, out var elbow) && double.TryParse(txtOffsetPipe.Text, out var pipe))
		{
			OffsetElbowValue = elbow;
			OffsetPipeValue = pipe;
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
		else
		{
			MessageBox.Show("Vui lòng nhập số hợp lệ vào các ô Offset.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/14.%20turn%20drainage%20pipe/turndrainagepipewindow.xaml", UriKind.Relative);
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
			txtOffsetElbow = (TextBox)target;
			break;
		case 2:
			txtOffsetPipe = (TextBox)target;
			break;
		case 3:
			rbUp = (RadioButton)target;
			break;
		case 4:
			rbDown = (RadioButton)target;
			break;
		case 5:
			rbLeft = (RadioButton)target;
			break;
		case 6:
			rbRight = (RadioButton)target;
			break;
		case 7:
			((Button)target).Click += BtnOk_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
