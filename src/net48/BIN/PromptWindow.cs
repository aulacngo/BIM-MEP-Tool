using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class PromptWindow : Window, IComponentConnector
{
	internal TextBox txtLength;

	private bool _contentLoaded;

	public double TargetLengthMm { get; set; } = 60.0;

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public PromptWindow()
	{
		InitializeComponent();
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (double.TryParse(txtLength.Text, out var val))
		{
			TargetLengthMm = val;
			base.DialogResult = true;
		}
		else
		{
			MessageBox.Show("Vui lòng nhập số hợp lệ.");
		}
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/06.%20sprinkler%20shortener%20vertical%20pipe/promptwindow.xaml", UriKind.Relative);
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
			txtLength = (TextBox)target;
			break;
		case 2:
			((Button)target).Click += BtnOk_Click;
			break;
		case 3:
			((Button)target).Click += BtnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
