using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class SplitDuctWindow : Window, IComponentConnector
{
	internal TextBox tbDistance;

	internal Button btOk;

	internal Button btCancel;

	private bool _contentLoaded;

	public double Distance { get; set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public SplitDuctWindow()
	{
		InitializeComponent();
		tbDistance.Text = "1120";
	}

	private void btOk_Click(object sender, RoutedEventArgs e)
	{
		string value = tbDistance.Text;
		if (double.TryParse(value, out var giatri))
		{
			Distance = giatri;
		}
		else
		{
			MessageBox.Show("Enter a number!", "Message");
		}
		base.DialogResult = true;
	}

	private void btCancel_Click(object sender, RoutedEventArgs e)
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
			Uri resourceLocater = new Uri("/BIN;component/11.%20split%20duct/splitductwindow.xaml", UriKind.Relative);
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
			tbDistance = (TextBox)target;
			break;
		case 2:
			btOk = (Button)target;
			btOk.Click += btOk_Click;
			break;
		case 3:
			btCancel = (Button)target;
			btCancel.Click += btCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
