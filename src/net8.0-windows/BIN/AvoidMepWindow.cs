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

public class AvoidMepWindow : Window, IComponentConnector
{
	internal TextBox txtDistance;

	internal TextBox txtPointsCount;

	internal ComboBox cboDirection;

	internal Button btnOk;

	internal Button btnCancel;

	private bool _contentLoaded;

	public double Distance { get; private set; } = 100.0;

	public int PointsCount { get; private set; } = 1;

	public bool IsAvoidUp { get; private set; } = true;

	public AvoidMepWindow()
	{
		InitializeComponent();
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		double dVal;
		bool dOk = double.TryParse(txtDistance.Text, out dVal);
		int pVal;
		bool pOk = int.TryParse(txtPointsCount.Text, out pVal);
		if (dOk && pOk)
		{
			Distance = dVal;
			PointsCount = pVal;
			IsAvoidUp = cboDirection.SelectedIndex == 0;
			base.DialogResult = true;
			Close();
		}
		else
		{
			MessageBox.Show("Vui lòng nhập các con số hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
	{
		Regex regex = new Regex("[^0-9.]+");
		e.Handled = regex.IsMatch(e.Text);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/58.%20flex%20duct%20avoid%20mep/avoidmepwindow.xaml", UriKind.Relative);
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
			txtDistance = (TextBox)target;
			txtDistance.PreviewTextInput += NumberValidationTextBox;
			break;
		case 2:
			txtPointsCount = (TextBox)target;
			txtPointsCount.PreviewTextInput += NumberValidationTextBox;
			break;
		case 3:
			cboDirection = (ComboBox)target;
			break;
		case 4:
			btnOk = (Button)target;
			btnOk.Click += BtnOk_Click;
			break;
		case 5:
			btnCancel = (Button)target;
			btnCancel.Click += BtnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
