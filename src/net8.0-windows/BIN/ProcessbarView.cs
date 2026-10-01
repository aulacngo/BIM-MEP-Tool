using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace BIN;

public class ProcessbarView : Window, IDisposable, IComponentConnector
{
	private readonly int _max;

	private int _current = 0;

	internal TextBlock txtStatus;

	internal Grid ProgressContainer;

	internal Rectangle FillBar;

	internal Rectangle HatchBar;

	internal System.Windows.Controls.Button btnCancel;

	private bool _contentLoaded;

	public bool IsCanceled { get; private set; } = false;

	protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public ProcessbarView(int maxValue)
	{
		InitializeComponent();
		_max = maxValue;
		UpdateDisplay();
	}

	public void Step()
	{
		if (!IsCanceled)
		{
			_current++;
			if (_current > _max)
			{
				_current = _max;
			}
			double percent = (double)_current / (double)_max;
			FillBar.Width = ProgressContainer.ActualWidth * percent;
			byte green = (byte)(100.0 + percent * 155.0);
			FillBar.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, green, 0));
			txtStatus.Text = $"Processing... {_current}/{_max}   ({percent * 100.0:0}%)";
			DoEvents();
		}
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
	{
		IsCanceled = true;
		btnCancel.Content = "Canceled";
		btnCancel.IsEnabled = false;
	}

	private static void DoEvents()
	{
		System.Windows.Application wpfApp = System.Windows.Application.Current;
		if (wpfApp != null && wpfApp.Dispatcher != null)
		{
			wpfApp.Dispatcher.Invoke(DispatcherPriority.Background, (Action)delegate
			{
			});
		}
		else
		{
			System.Windows.Forms.Application.DoEvents();
		}
	}

	private void UpdateDisplay()
	{
		txtStatus.Text = $"Processing... 0/{_max}   (0%)";
	}

	public void Dispose()
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
			Uri resourceLocater = new Uri("/BIN;component/13.%20export%20family/processbarview.xaml", UriKind.Relative);
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
			txtStatus = (TextBlock)target;
			break;
		case 2:
			ProgressContainer = (Grid)target;
			break;
		case 3:
			FillBar = (Rectangle)target;
			break;
		case 4:
			HatchBar = (Rectangle)target;
			break;
		case 5:
			btnCancel = (System.Windows.Controls.Button)target;
			btnCancel.Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
