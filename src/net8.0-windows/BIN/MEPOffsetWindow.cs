using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace BIN;

public class MEPOffsetWindow : Window, IComponentConnector
{
	private Dictionary<string, BitmapImage> _imageCache = new Dictionary<string, BitmapImage>();

	internal RadioButton rbPipe;

	internal RadioButton rbDuct;

	internal RadioButton rbCableTray;

	internal ComboBox cbbOption;

	internal ComboBox cbbAngle;

	internal TextBox tbOffset;

	internal Image imgPreview;

	internal Button btOk;

	internal Button btCancel;

	private bool _contentLoaded;

	public int MEPType { get; set; }

	public string Option { get; set; }

	public string Angle { get; set; }

	public double Offset { get; set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public MEPOffsetWindow()
	{
		InitializeComponent();
		cbbOption.Items.Add("Cut Up");
		cbbOption.Items.Add("Cut Down");
		cbbOption.SelectedIndex = 0;
		cbbAngle.Items.Add("45");
		cbbAngle.Items.Add("90");
		cbbAngle.SelectedIndex = 0;
		tbOffset.Text = "500";
		UpdatePreviewImage();
	}

	private void MEPType_Changed(object sender, RoutedEventArgs e)
	{
		UpdatePreviewImage();
	}

	private void Selection_Changed(object sender, SelectionChangedEventArgs e)
	{
		UpdatePreviewImage();
	}

	private void UpdatePreviewImage()
	{
		if (cbbOption == null || cbbAngle == null)
		{
			return;
		}
		string option = cbbOption.SelectedItem?.ToString() ?? "Cut Up";
		string angle = cbbAngle.SelectedItem?.ToString() ?? "45";
		string imageName = "";
		if (option == "Cut Up" && angle == "45")
		{
			imageName = "CutElbowUp45Img";
		}
		else if (option == "Cut Up" && angle == "90")
		{
			imageName = "CutElbowUp90Img";
		}
		else if (option == "Cut Down" && angle == "45")
		{
			imageName = "CutElbowDown45Img";
		}
		else if (option == "Cut Down" && angle == "90")
		{
			imageName = "CutElbowDown90Img";
		}
		try
		{
			if (!_imageCache.ContainsKey(imageName))
			{
				string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				string[] possiblePaths = new string[3]
				{
					Path.Combine(assemblyDir, "MEP Offset - Photo", imageName + ".png"),
					Path.Combine(Directory.GetParent(assemblyDir)?.Parent?.FullName ?? "", "MEP Offset - Photo", imageName + ".png"),
					Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MEP Offset - Photo", imageName + ".png")
				};
				string imgPath = null;
				string[] array = possiblePaths;
				foreach (string p in array)
				{
					if (File.Exists(p))
					{
						imgPath = p;
						break;
					}
				}
				if (imgPath != null)
				{
					BitmapImage bi = new BitmapImage();
					using (FileStream fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read))
					{
						bi.BeginInit();
						bi.CacheOption = BitmapCacheOption.OnLoad;
						bi.StreamSource = fs;
						bi.EndInit();
					}
					_imageCache[imageName] = bi;
				}
			}
			imgPreview.Source = (_imageCache.ContainsKey(imageName) ? _imageCache[imageName] : null);
		}
		catch
		{
		}
	}

	private void btOk_Click(object sender, RoutedEventArgs e)
	{
		if (!double.TryParse(tbOffset.Text, out var offset))
		{
			MessageBox.Show("Vui lòng nhập giá trị offset hợp lệ!", "Thông báo");
			return;
		}
		Offset = offset;
		Option = cbbOption.SelectedItem?.ToString() ?? "Cut Up";
		Angle = cbbAngle.SelectedItem?.ToString() ?? "45";
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
		base.DialogResult = true;
		Close();
	}

	private void btCancel_Click(object sender, RoutedEventArgs e)
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
			Uri resourceLocater = new Uri("/BIN;component/30.%20mep%20offset/mepoffsetwindow.xaml", UriKind.Relative);
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
			rbPipe.Checked += MEPType_Changed;
			break;
		case 2:
			rbDuct = (RadioButton)target;
			rbDuct.Checked += MEPType_Changed;
			break;
		case 3:
			rbCableTray = (RadioButton)target;
			rbCableTray.Checked += MEPType_Changed;
			break;
		case 4:
			cbbOption = (ComboBox)target;
			cbbOption.SelectionChanged += Selection_Changed;
			break;
		case 5:
			cbbAngle = (ComboBox)target;
			cbbAngle.SelectionChanged += Selection_Changed;
			break;
		case 6:
			tbOffset = (TextBox)target;
			break;
		case 7:
			imgPreview = (Image)target;
			break;
		case 8:
			btOk = (Button)target;
			btOk.Click += btOk_Click;
			break;
		case 9:
			btCancel = (Button)target;
			btCancel.Click += btCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
