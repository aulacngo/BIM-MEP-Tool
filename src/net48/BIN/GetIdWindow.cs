using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BIN;

public class GetIdWindow : Window
{
	private TextBox _textBox;

	public GetIdWindow(string idInfo)
	{
		base.Title = "Get ID - BIM Tool";
		base.Width = 480.0;
		base.SizeToContent = SizeToContent.Height;
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		base.ResizeMode = ResizeMode.NoResize;
		base.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 36));
		base.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 212, byte.MaxValue));
		base.BorderThickness = new Thickness(1.0);
		Grid outerGrid = new Grid
		{
			Margin = new Thickness(16.0),
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition
				{
					Height = GridLength.Auto
				}
			}
		};
		StackPanel header = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		TextBlock headerLabel = new TextBlock
		{
			Text = "\ud83d\udd0d  GET ID",
			FontSize = 16.0,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 212, byte.MaxValue)),
			VerticalAlignment = VerticalAlignment.Center
		};
		header.Children.Add(headerLabel);
		Grid.SetRow(header, 0);
		outerGrid.Children.Add(header);
		Border border = new Border
		{
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 48)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 90)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(4.0),
			Padding = new Thickness(10.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		_textBox = new TextBox
		{
			Text = idInfo,
			IsReadOnly = true,
			AcceptsReturn = true,
			TextWrapping = TextWrapping.Wrap,
			Background = Brushes.Transparent,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 240, byte.MaxValue)),
			BorderThickness = new Thickness(0.0),
			FontFamily = new FontFamily("Consolas"),
			FontSize = 13.0,
			SelectionBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 150, 200))
		};
		border.Child = _textBox;
		Grid.SetRow(border, 1);
		outerGrid.Children.Add(border);
		StackPanel btnPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button copyBtn = CreateButton("\ud83d\udccb  Copy ID", System.Windows.Media.Color.FromRgb(0, 140, 200), System.Windows.Media.Color.FromRgb(0, 100, 160));
		copyBtn.Click += delegate
		{
			string text = _textBox.Text;
			string text2 = ExtractMainId(text);
			Clipboard.SetText(text2);
			copyBtn.Content = "✔  Copied!";
			copyBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 160, 80));
		};
		Button copyAllBtn = CreateButton("\ud83d\udcc4  Copy All", System.Windows.Media.Color.FromRgb(70, 70, 100), System.Windows.Media.Color.FromRgb(50, 50, 80));
		copyAllBtn.Click += delegate
		{
			Clipboard.SetText(_textBox.Text);
			copyAllBtn.Content = "✔  Copied!";
			copyAllBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 160, 80));
		};
		Button closeBtn = CreateButton("✕  Close", System.Windows.Media.Color.FromRgb(90, 30, 30), System.Windows.Media.Color.FromRgb(70, 20, 20));
		closeBtn.Click += delegate
		{
			Close();
		};
		btnPanel.Children.Add(copyBtn);
		btnPanel.Children.Add(copyAllBtn);
		btnPanel.Children.Add(closeBtn);
		Grid.SetRow(btnPanel, 2);
		outerGrid.Children.Add(btnPanel);
		base.Content = outerGrid;
		base.Loaded += delegate
		{
			_textBox.SelectAll();
		};
		base.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}
			if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
			{
				string text3 = ExtractMainId(_textBox.Text);
				Clipboard.SetText(text3);
			}
		};
	}

	private string ExtractMainId(string fullText)
	{
		string[] array = fullText.Split('\n');
		foreach (string line in array)
		{
			if (line.Contains("Element ID"))
			{
				string[] parts = line.Split(':');
				if (parts.Length >= 2)
				{
					return parts[1].Trim();
				}
			}
		}
		return fullText;
	}

	private Button CreateButton(string text, Color bg, Color hoverBg)
	{
		Button btn = new Button
		{
			Content = text,
			Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
			Padding = new Thickness(14.0, 6.0, 14.0, 6.0),
			Background = new SolidColorBrush(bg),
			Foreground = Brushes.White,
			BorderThickness = new Thickness(0.0),
			FontSize = 12.0,
			Cursor = Cursors.Hand
		};
		btn.MouseEnter += delegate
		{
			btn.Background = new SolidColorBrush(hoverBg);
		};
		btn.MouseLeave += delegate
		{
			btn.Background = new SolidColorBrush(bg);
		};
		return btn;
	}
}
