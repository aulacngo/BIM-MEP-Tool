using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace BIN;

public class DrainagePipeTypePickerWindow : Window, IComponentConnector
{
	internal ListBox lstType;

	internal Image imgType1;

	internal Image imgType2;

	internal Image imgType3;

	internal Image imgType4;

	internal Image imgType5;

	internal Image imgType6;

	internal TextBox txtOffsetElbow;

	internal TextBox txtOffsetY;

	internal TextBox txtOffsetH;

	internal TextBox txtOffsetZ;

	private bool _contentLoaded;

	public int SelectedTypeIndex { get; private set; } = 0;

	public double? OffsetElbowMm { get; private set; }

	public double? OffsetYMm { get; private set; }

	public double? OffsetHMm { get; private set; }

	public double? OffsetZMm { get; private set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public DrainagePipeTypePickerWindow()
	{
		InitializeComponent();
		LoadImages();
		if (lstType != null)
		{
			UpdateDefaultValues(lstType.SelectedIndex);
		}
	}

	private void LoadImages()
	{
		string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string folder = Path.Combine(assemblyFolder, "Draw Drainage Pipe - Photo");
		SetImageSource(imgType1, Path.Combine(folder, "Type1.png"));
		SetImageSource(imgType2, Path.Combine(folder, "Type2.png"));
		SetImageSource(imgType3, Path.Combine(folder, "Type3.png"));
		SetImageSource(imgType4, Path.Combine(folder, "Type4.png"));
		SetImageSource(imgType5, Path.Combine(folder, "Type5.png"));
		SetImageSource(imgType6, Path.Combine(folder, "Type6.png"));
	}

	private void SetImageSource(Image imgControl, string path)
	{
		try
		{
			if (imgControl != null && File.Exists(path))
			{
				imgControl.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
			}
		}
		catch
		{
		}
	}

	private void lstType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (lstType != null && txtOffsetElbow != null)
		{
			UpdateDefaultValues(lstType.SelectedIndex);
		}
	}

	private void UpdateDefaultValues(int index)
	{
		txtOffsetElbow.Text = "";
		txtOffsetY.Text = "";
		txtOffsetH.Text = "";
		txtOffsetZ.Text = "";
		switch (index)
		{
		case 0:
			txtOffsetElbow.Text = "100";
			txtOffsetY.Text = "150";
			break;
		case 1:
			txtOffsetElbow.Text = "120";
			txtOffsetY.Text = "120";
			break;
		case 2:
			txtOffsetElbow.Text = "120";
			txtOffsetY.Text = "600";
			txtOffsetH.Text = "300";
			break;
		case 3:
			txtOffsetElbow.Text = "120";
			txtOffsetY.Text = "150";
			txtOffsetZ.Text = "150";
			break;
		case 4:
			txtOffsetElbow.Text = "120";
			txtOffsetY.Text = "240";
			break;
		case 5:
			txtOffsetY.Text = "180";
			break;
		}
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		SelectedTypeIndex = lstType.SelectedIndex;
		OffsetElbowMm = ParseNullableDouble(txtOffsetElbow.Text);
		OffsetYMm = ParseNullableDouble(txtOffsetY.Text);
		OffsetHMm = ParseNullableDouble(txtOffsetH.Text);
		OffsetZMm = ParseNullableDouble(txtOffsetZ.Text);
		base.DialogResult = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	private static double? ParseNullableDouble(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			return null;
		}
		s = s.Trim().Replace(',', '.');
		if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
		{
			return v;
		}
		return null;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/15.%20draw%20drainage%20pipe/drainagepipetypepickerwindow.xaml", UriKind.Relative);
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
			lstType = (ListBox)target;
			lstType.SelectionChanged += lstType_SelectionChanged;
			break;
		case 2:
			imgType1 = (Image)target;
			break;
		case 3:
			imgType2 = (Image)target;
			break;
		case 4:
			imgType3 = (Image)target;
			break;
		case 5:
			imgType4 = (Image)target;
			break;
		case 6:
			imgType5 = (Image)target;
			break;
		case 7:
			imgType6 = (Image)target;
			break;
		case 8:
			txtOffsetElbow = (TextBox)target;
			break;
		case 9:
			txtOffsetY = (TextBox)target;
			break;
		case 10:
			txtOffsetH = (TextBox)target;
			break;
		case 11:
			txtOffsetZ = (TextBox)target;
			break;
		case 12:
			((Button)target).Click += Ok_Click;
			break;
		case 13:
			((Button)target).Click += Cancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
