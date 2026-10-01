using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class RotateFamilyInstanceWindow : Window, IComponentConnector
{
	internal TextBlock txtFamilyInfo;

	internal RadioButton rbAllInView;

	internal RadioButton rbSelected;

	internal TextBox txtAngle;

	internal TextBlock txtCount;

	private bool _contentLoaded;

	public double AngleValue { get; private set; }

	public bool IsSelectMode { get; private set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public RotateFamilyInstanceWindow(string familyName, string typeName, int count)
	{
		InitializeComponent();
		txtFamilyInfo.Text = familyName + " : " + typeName;
		txtCount.Text = $"Số lượng trong View: {count}";
		txtAngle.Focus();
		txtAngle.SelectAll();
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (double.TryParse(txtAngle.Text, out var angle))
		{
			AngleValue = angle;
			IsSelectMode = rbSelected.IsChecked == true;
			base.DialogResult = true;
			Close();
		}
		else
		{
			MessageBox.Show("Vui lòng nhập số hợp lệ!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/17.%20rotate%20family%20instance/rotatefamilyinstancewindow.xaml", UriKind.Relative);
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
			txtFamilyInfo = (TextBlock)target;
			break;
		case 2:
			rbAllInView = (RadioButton)target;
			break;
		case 3:
			rbSelected = (RadioButton)target;
			break;
		case 4:
			txtAngle = (TextBox)target;
			break;
		case 5:
			txtCount = (TextBlock)target;
			break;
		case 6:
			((Button)target).Click += BtnOk_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
