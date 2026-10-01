using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class ProgressBarView : Window, IDisposable, IComponentConnector
{
	private double maximum;

	private string title;

	internal System.Windows.Controls.ProgressBar progressBar;

	internal System.Windows.Controls.Label lb1;

	private bool _contentLoaded;

	public bool IsClosed { get; private set; }

	protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public ProgressBarView(string title, double maximum)
	{
		InitializeComponent();
		this.title = title;
		this.maximum = maximum;
		progressBar.Maximum = Math.Max(1.0, maximum);
		base.Title = title;
		Closed += (sender, args) => IsClosed = true;
	}

	public bool Update(double value = 1.0)
	{
		DoEvent();
		progressBar.Value += value;
		double percent = Math.Round(progressBar.Value / maximum * 100.0, 0);
		lb1.Content = percent + "%";
		base.Title = title + progressBar.Value + "/" + maximum;
		return IsClosed;
	}

	private void DoEvent()
	{
		System.Windows.Forms.Application.DoEvents();
		System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
	}

	public void Dispose()
	{
		if (!IsClosed)
		{
			Close();
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (_contentLoaded)
		{
			return;
		}

		_contentLoaded = true;
		Width = 420.0;
		Height = 120.0;
		ResizeMode = ResizeMode.NoResize;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		ShowInTaskbar = false;

		Grid grid = new Grid { Margin = new Thickness(16.0) };
		grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		progressBar = new System.Windows.Controls.ProgressBar
		{
			Minimum = 0.0,
			Height = 24.0,
			VerticalAlignment = VerticalAlignment.Center
		};
		lb1 = new System.Windows.Controls.Label
		{
			Content = "0%",
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			Padding = new Thickness(0.0, 6.0, 0.0, 0.0)
		};
		Grid.SetRow(progressBar, 0);
		Grid.SetRow(lb1, 1);
		grid.Children.Add(progressBar);
		grid.Children.Add(lb1);
		Content = grid;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			progressBar = (System.Windows.Controls.ProgressBar)target;
			break;
		case 2:
			lb1 = (System.Windows.Controls.Label)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
