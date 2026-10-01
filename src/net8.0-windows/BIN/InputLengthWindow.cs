using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class InputLengthWindow : Window, IComponentConnector
{
	internal TextBox txtInput;

	internal Button btnOk;

	internal Button btnCancel;

	private bool _contentLoaded;

	public double ResultLength { get; private set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public InputLengthWindow()
	{
		InitializeComponent();
		txtInput.Focus();
		txtInput.SelectAll();
	}

	private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
	{
		Regex regex = new Regex("[^0-9]+");
		e.Handled = regex.IsMatch(e.Text);
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (double.TryParse(txtInput.Text, out var val) && val > 0.0)
		{
			ResultLength = val;
			base.DialogResult = true;
			Close();
		}
		else
		{
			MessageBox.Show("Vui lòng nhập giá trị số lớn hơn 0!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
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
			Uri resourceLocater = new Uri("/BIN;component/02.%20create%202%20elbows%2045/inputlengthwindow.xaml", UriKind.Relative);
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
			txtInput = (TextBox)target;
			txtInput.PreviewTextInput += NumberValidationTextBox;
			break;
		case 2:
			btnOk = (Button)target;
			btnOk.Click += BtnOk_Click;
			break;
		case 3:
			btnCancel = (Button)target;
			btnCancel.Click += BtnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
