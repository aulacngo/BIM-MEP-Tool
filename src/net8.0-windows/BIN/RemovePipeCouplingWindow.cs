using System;
using System.CodeDom.Compiler;
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

public class RemovePipeCouplingWindow : Window, IComponentConnector
{
	internal Image imgGuide;

	internal Button btnSelect;

	internal Button btnCancel;

	private bool _contentLoaded;

	public bool IsContinue { get; private set; } = false;

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public RemovePipeCouplingWindow()
	{
		InitializeComponent();
		LoadImage();
	}

	private void LoadImage()
	{
		try
		{
			string assemblyPath = Assembly.GetExecutingAssembly().Location;
			string assemblyDir = Path.GetDirectoryName(assemblyPath);
			string imagePath1 = Path.Combine(assemblyDir, "Sprinkler - Photo", "Remove Pipe Coupling.png");
			string imagePath2 = Path.Combine(Directory.GetParent(assemblyDir).Parent.FullName, "Sprinkler - Photo", "Remove Pipe Coupling.png");
			string finalPath = (File.Exists(imagePath1) ? imagePath1 : (File.Exists(imagePath2) ? imagePath2 : null));
			if (finalPath != null)
			{
				BitmapImage bitmap = new BitmapImage();
				bitmap.BeginInit();
				bitmap.UriSource = new Uri(finalPath, UriKind.Absolute);
				bitmap.CacheOption = BitmapCacheOption.OnLoad;
				bitmap.EndInit();
				imgGuide.Source = bitmap;
			}
		}
		catch
		{
		}
	}

	private void btnSelect_Click(object sender, RoutedEventArgs e)
	{
		IsContinue = true;
		Close();
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
	{
		IsContinue = false;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/18.%20remove%20pipe%20coupling/removepipecouplingwindow.xaml", UriKind.Relative);
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
			imgGuide = (Image)target;
			break;
		case 2:
			btnSelect = (Button)target;
			btnSelect.Click += btnSelect_Click;
			break;
		case 3:
			btnCancel = (Button)target;
			btnCancel.Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
