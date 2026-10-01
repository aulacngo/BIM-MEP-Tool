using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;

namespace BIN.Common.UI;

public class ProgressBarView : Window, IComponentConnector
{
	internal ProgressBar pbStatus;

	internal TextBlock lblPercent;

	internal TextBlock lblStatus;

	internal TextBlock lblCount;

	private bool _contentLoaded;

	public bool IsCanceled { get; private set; } = false;

	public ProgressBarView(string title = "Đang xử lý...")
	{
		InitializeComponent();
		base.Title = title;
	}

	public void Update(int current, int total, string message)
	{
		double percent = (double)current / (double)total * 100.0;
		pbStatus.Value = percent;
		lblPercent.Text = $"{percent:F0}%";
		lblStatus.Text = message;
		lblCount.Text = $"{current}/{total}";
		DoEvents();
	}

	private void Button_Cancel_Click(object sender, RoutedEventArgs e)
	{
		IsCanceled = true;
		lblStatus.Text = "Đang hủy...";
		DoEvents();
	}

	private void DoEvents()
	{
		base.Dispatcher.Invoke(DispatcherPriority.Background, (Action)delegate
		{
		});
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/39.%20select%20by%20parameter/common/ui/progressbarview.xaml", UriKind.Relative);
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
			pbStatus = (ProgressBar)target;
			break;
		case 2:
			lblPercent = (TextBlock)target;
			break;
		case 3:
			lblStatus = (TextBlock)target;
			break;
		case 4:
			lblCount = (TextBlock)target;
			break;
		case 5:
			((Button)target).Click += Button_Cancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
