using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BIN;

public class ConnectSprinklerToPipeWindow : Window
{
	private readonly int[] _typesWithoutZOffset = new int[] { 3, 6 };

	public int SelectedType { get; private set; } = 1;
	public double ZOffset { get; private set; } = 200.0;
	public double PipeDiameter { get; private set; } = 25.0;
	public bool IsOK { get; private set; } = false;

	private RadioButton _rbP1;
	private RadioButton _rbP2;
	private RadioButton _rbP3;
	private RadioButton _rbP4;
	private RadioButton _rbU1;
	private RadioButton _rbU2;
	private RadioButton _rbU3;

	private ComboBox _cboPipeSize;
	private TextBox _txtZOffset;
	private TextBlock _lblZOffsetNote;
	private TextBlock _lblDesc;
	private Canvas _canvasPreview;

	public ConnectSprinklerToPipeWindow()
	{
		InitializeUI();
		SelectType(1);
	}

	private void InitializeUI()
	{
		Title = "BIM TOOL - Connect Sprinkler To Pipe";
		Width = 740;
		Height = 540;
		MinWidth = 680;
		MinHeight = 480;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));
		FontFamily = new FontFamily("Segoe UI");
		FontSize = 13;

		Grid mainGrid = new Grid();
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });

		// 1. Header
		Border header = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(27, 42, 74)),
			Padding = new Thickness(18, 0, 18, 0)
		};
		Grid headerGrid = new Grid();
		TextBlock titleText = new TextBlock
		{
			Text = "KET NOI SPRINKLER VAO ONG (CONNECT SPRINKLER TO PIPE)",
			Foreground = Brushes.White,
			FontWeight = FontWeights.SemiBold,
			FontSize = 15,
			VerticalAlignment = VerticalAlignment.Center
		};
		headerGrid.Children.Add(titleText);
		header.Child = headerGrid;
		Grid.SetRow(header, 0);
		mainGrid.Children.Add(header);

		// 2. Content (2 columns: left types list, right preview & params)
		Grid contentGrid = new Grid { Margin = new Thickness(16) };
		contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
		contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
		contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		// Left: Options
		Border leftBorder = new Border
		{
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(6),
			Padding = new Thickness(14)
		};
		ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
		StackPanel spOptions = new StackPanel();

		// Group Pendant
		Border grpPendant = CreateGroupHeader("PENDANT SPRINKLER (Huong xuong)", "#0284C7");
		spOptions.Children.Add(grpPendant);

		_rbP1 = CreateRadio("Type 1: Ong dung + 2 Co + Ong ngang", 1);
		_rbP1.IsChecked = true;
		_rbP2 = CreateRadio("Type 2: Ong dung vao dau phun (Co Z-Offset)", 2);
		_rbP3 = CreateRadio("Type 3: Ong dung thang (Khong Z-Offset)", 3);
		_rbP4 = CreateRadio("Type 4: Ong dung chech 45 do", 4);

		spOptions.Children.Add(_rbP1);
		spOptions.Children.Add(_rbP2);
		spOptions.Children.Add(_rbP3);
		spOptions.Children.Add(_rbP4);

		// Group Upright
		Border grpUpright = CreateGroupHeader("UPRIGHT SPRINKLER (Huong len)", "#16A34A");
		grpUpright.Margin = new Thickness(0, 14, 0, 8);
		spOptions.Children.Add(grpUpright);

		_rbU1 = CreateRadio("Type 1: Ong dung len + 2 Co + Ong ngang", 5);
		_rbU2 = CreateRadio("Type 2: Ong dung len (Khong Z-Offset)", 6);
		_rbU3 = CreateRadio("Type 3: Ong dung len (Co Z-Offset)", 7);

		spOptions.Children.Add(_rbU1);
		spOptions.Children.Add(_rbU2);
		spOptions.Children.Add(_rbU3);

		scroll.Content = spOptions;
		leftBorder.Child = scroll;
		Grid.SetColumn(leftBorder, 0);
		contentGrid.Children.Add(leftBorder);

		// Right: Preview & Settings
		StackPanel spRight = new StackPanel();

		// Preview Box
		Border previewBorder = new Border
		{
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(6),
			Height = 220,
			Padding = new Thickness(10)
		};
		Grid prevGrid = new Grid();
		prevGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		prevGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });

		_canvasPreview = new Canvas { ClipToBounds = true };
		Grid.SetRow(_canvasPreview, 0);
		prevGrid.Children.Add(_canvasPreview);

		_lblDesc = new TextBlock
		{
			Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
			FontSize = 12,
			FontStyle = FontStyles.Italic,
			TextAlignment = TextAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetRow(_lblDesc, 1);
		prevGrid.Children.Add(_lblDesc);

		previewBorder.Child = prevGrid;
		spRight.Children.Add(previewBorder);

		// Settings Box
		Border settingsBorder = new Border
		{
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(6),
			Margin = new Thickness(0, 12, 0, 0),
			Padding = new Thickness(14)
		};
		Grid settingsGrid = new Grid();
		settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
		settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
		settingsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

		settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
		settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		settingsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

		// Pipe size
		TextBlock lblPipe = new TextBlock { Text = "Duong kinh ong:", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Medium };
		Grid.SetRow(lblPipe, 0);
		Grid.SetColumn(lblPipe, 0);
		settingsGrid.Children.Add(lblPipe);

		_cboPipeSize = new ComboBox
		{
			Height = 28,
			VerticalContentAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 2, 0, 2)
		};
		_cboPipeSize.Items.Add(new ComboBoxItem { Content = "DN25 (1\")", IsSelected = true });
		_cboPipeSize.Items.Add(new ComboBoxItem { Content = "DN32 (1-1/4\")" });
		Grid.SetRow(_cboPipeSize, 0);
		Grid.SetColumn(_cboPipeSize, 1);
		settingsGrid.Children.Add(_cboPipeSize);

		// Z-Offset
		TextBlock lblZ = new TextBlock { Text = "Khoang cach Z-Offset:", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Medium };
		Grid.SetRow(lblZ, 1);
		Grid.SetColumn(lblZ, 0);
		settingsGrid.Children.Add(lblZ);

		_txtZOffset = new TextBox
		{
			Text = "200",
			Height = 28,
			VerticalContentAlignment = VerticalAlignment.Center,
			Padding = new Thickness(6, 0, 6, 0),
			Margin = new Thickness(0, 2, 0, 2)
		};
		Grid.SetRow(_txtZOffset, 1);
		Grid.SetColumn(_txtZOffset, 1);
		settingsGrid.Children.Add(_txtZOffset);

		TextBlock lblMm = new TextBlock { Text = "mm", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0), Foreground = Brushes.Gray };
		Grid.SetRow(lblMm, 1);
		Grid.SetColumn(lblMm, 2);
		settingsGrid.Children.Add(lblMm);

		_lblZOffsetNote = new TextBlock
		{
			Text = "Khoang cach tu Sprinkler den ong nhanh",
			FontSize = 11,
			Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
			VerticalAlignment = VerticalAlignment.Center
		};
		Grid.SetRow(_lblZOffsetNote, 2);
		Grid.SetColumn(_lblZOffsetNote, 1);
		Grid.SetColumnSpan(_lblZOffsetNote, 2);
		settingsGrid.Children.Add(_lblZOffsetNote);

		settingsBorder.Child = settingsGrid;
		spRight.Children.Add(settingsBorder);

		Grid.SetColumn(spRight, 2);
		contentGrid.Children.Add(spRight);

		Grid.SetRow(contentGrid, 1);
		mainGrid.Children.Add(contentGrid);

		// 3. Footer Buttons
		Border footer = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
			BorderThickness = new Thickness(0, 1, 0, 0),
			Padding = new Thickness(16, 10, 16, 10)
		};
		StackPanel spButtons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};

		Button btnOK = new Button
		{
			Content = "Thuc hien (OK)",
			Width = 120,
			Height = 34,
			Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
			Foreground = Brushes.White,
			FontWeight = FontWeights.SemiBold,
			BorderThickness = new Thickness(0),
			Cursor = Cursors.Hand,
			Margin = new Thickness(0, 0, 10, 0)
		};
		btnOK.Click += BtnOK_Click;

		Button btnCancel = new Button
		{
			Content = "Huy (Cancel)",
			Width = 100,
			Height = 34,
			Background = Brushes.White,
			Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
			BorderThickness = new Thickness(1),
			Cursor = Cursors.Hand
		};
		btnCancel.Click += (s, e) => { IsOK = false; DialogResult = false; };

		spButtons.Children.Add(btnOK);
		spButtons.Children.Add(btnCancel);
		footer.Child = spButtons;
		Grid.SetRow(footer, 2);
		mainGrid.Children.Add(footer);

		Content = mainGrid;

		KeyDown += (s, e) =>
		{
			if (e.Key == Key.Escape) { IsOK = false; DialogResult = false; }
			else if (e.Key == Key.Enter) { BtnOK_Click(s, e); }
		};
	}

	private Border CreateGroupHeader(string text, string hexColor)
	{
		Border b = new Border
		{
			Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)),
			CornerRadius = new CornerRadius(4),
			Padding = new Thickness(8, 4, 8, 4),
			Margin = new Thickness(0, 0, 0, 8)
		};
		b.Child = new TextBlock
		{
			Text = text,
			Foreground = Brushes.White,
			FontWeight = FontWeights.Bold,
			FontSize = 11
		};
		return b;
	}

	private RadioButton CreateRadio(string text, int type)
	{
		RadioButton rb = new RadioButton
		{
			Content = text,
			Tag = type,
			Margin = new Thickness(4, 4, 4, 4),
			Cursor = Cursors.Hand,
			GroupName = "SprinklerConnectionTypes"
		};
		rb.Checked += (s, e) => SelectType(type);
		return rb;
	}

	private void SelectType(int type)
	{
		SelectedType = type;
		bool noZ = Array.IndexOf(_typesWithoutZOffset, type) >= 0;

		if (_txtZOffset != null)
		{
			_txtZOffset.IsEnabled = !noZ;
			_txtZOffset.Opacity = noZ ? 0.4 : 1.0;
		}
		if (_lblZOffsetNote != null)
		{
			_lblZOffsetNote.Text = noZ ? "Kieu nay noi truc tiep, khong can Z-Offset." : "Khoang cach tu Sprinkler den ong nhanh.";
			_lblZOffsetNote.Foreground = noZ ? new SolidColorBrush(Color.FromRgb(225, 29, 72)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));
		}

		UpdatePreview(type);
	}

	private void UpdatePreview(int type)
	{
		if (_canvasPreview == null) return;
		_canvasPreview.Children.Clear();

		double w = 320;
		double h = 180;

		// Draw background grid lines
		for (double x = 20; x < w; x += 30)
		{
			_canvasPreview.Children.Add(new Line
			{
				X1 = x, Y1 = 0, X2 = x, Y2 = h,
				Stroke = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)),
				StrokeThickness = 1
			});
		}
		for (double y = 20; y < h; y += 30)
		{
			_canvasPreview.Children.Add(new Line
			{
				X1 = 0, Y1 = y, X2 = w, Y2 = y,
				Stroke = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)),
				StrokeThickness = 1
			});
		}

		// Draw Schematic by Type
		Brush pipeBrush = new SolidColorBrush(Color.FromRgb(30, 58, 138));
		Brush branchBrush = new SolidColorBrush(Color.FromRgb(2, 132, 199));
		Brush sprBrush = new SolidColorBrush(Color.FromRgb(225, 29, 72));

		// Main pipe (horizontal top)
		double mainPipeY = (type >= 5) ? 130 : 40;
		Rectangle mainPipe = new Rectangle
		{
			Width = 280, Height = 12,
			Fill = pipeBrush,
			RadiusX = 2, RadiusY = 2
		};
		Canvas.SetLeft(mainPipe, 20.0);
		Canvas.SetTop(mainPipe, mainPipeY);
		_canvasPreview.Children.Add(mainPipe);

		// Label Main Pipe
		TextBlock lblMain = new TextBlock
		{
			Text = "Main Pipe",
			FontSize = 10,
			FontWeight = FontWeights.Bold,
			Foreground = pipeBrush
		};
		Canvas.SetLeft(lblMain, 25.0);
		Canvas.SetTop(lblMain, mainPipeY - 16.0);
		_canvasPreview.Children.Add(lblMain);

		double sprX = 220;
		double pipeConnX = 100;

		switch (type)
		{
			case 1: // Pendant 1: Down + Elbow + Horizontal + Elbow + Down into Sprinkler
				_lblDesc.Text = "Ong dung xuong -> 2 Co 90 do -> Ong ngang -> Vao dau phun";
				DrawPipeSegment(pipeConnX, mainPipeY + 12, pipeConnX, 90, branchBrush, 6);
				DrawPipeSegment(pipeConnX, 90, sprX, 90, branchBrush, 6);
				DrawPipeSegment(sprX, 90, sprX, 135, branchBrush, 6);
				DrawSprinklerHead(sprX, 135, true, sprBrush);
				break;

			case 2: // Pendant 2: Direct drop with Z offset
				_lblDesc.Text = "Ong dung thang truc tiep tu ong chinh xuong dau phun";
				DrawPipeSegment(pipeConnX, mainPipeY + 12, pipeConnX, 135, branchBrush, 6);
				DrawSprinklerHead(pipeConnX, 135, true, sprBrush);
				break;

			case 3: // Pendant 3: Direct without Z offset
				_lblDesc.Text = "Ong dung thang ket noi truc tiep vao dau phun (Khong can Z-Offset)";
				DrawPipeSegment(pipeConnX, mainPipeY + 12, pipeConnX, 135, branchBrush, 6);
				DrawSprinklerHead(pipeConnX, 135, true, sprBrush);
				break;

			case 4: // Pendant 4: 45 degree angle
				_lblDesc.Text = "Ong nhanh dung chech goc 45 do qua 2 Co";
				DrawPipeSegment(pipeConnX, mainPipeY + 12, pipeConnX + 30, 80, branchBrush, 6);
				DrawPipeSegment(pipeConnX + 30, 80, sprX, 135, branchBrush, 6);
				DrawSprinklerHead(sprX, 135, true, sprBrush);
				break;

			case 5: // Upright 1: Up + 2 Elbows + Horizontal + Up into Sprinkler
				_lblDesc.Text = "Ong dung len -> 2 Co 90 do -> Ong ngang -> Vao dau phun huong len";
				DrawPipeSegment(pipeConnX, mainPipeY, pipeConnX, 80, branchBrush, 6);
				DrawPipeSegment(pipeConnX, 80, sprX, 80, branchBrush, 6);
				DrawPipeSegment(sprX, 80, sprX, 40, branchBrush, 6);
				DrawSprinklerHead(sprX, 40, false, sprBrush);
				break;

			case 6: // Upright 2: Direct Up
				_lblDesc.Text = "Ong dung len truc tiep vao dau phun (Khong Z-Offset)";
				DrawPipeSegment(pipeConnX, mainPipeY, pipeConnX, 40, branchBrush, 6);
				DrawSprinklerHead(pipeConnX, 40, false, sprBrush);
				break;

			case 7: // Upright 3: Direct Up with Z-Offset
				_lblDesc.Text = "Ong dung len truc tiep co cai dat khoang cach Z-Offset";
				DrawPipeSegment(pipeConnX, mainPipeY, pipeConnX, 40, branchBrush, 6);
				DrawSprinklerHead(pipeConnX, 40, false, sprBrush);
				break;
		}
	}

	private void DrawPipeSegment(double x1, double y1, double x2, double y2, Brush stroke, double thickness)
	{
		_canvasPreview.Children.Add(new Line
		{
			X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
			Stroke = stroke,
			StrokeThickness = thickness,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round
		});
	}

	private void DrawSprinklerHead(double x, double y, bool isPendant, Brush brush)
	{
		double dir = isPendant ? 1 : -1;

		Ellipse el = new Ellipse
		{
			Width = 8, Height = 8,
			Fill = brush
		};
		Canvas.SetLeft(el, x - 4);
		Canvas.SetTop(el, y - 4);
		_canvasPreview.Children.Add(el);

		_canvasPreview.Children.Add(new Line
		{
			X1 = x - 12, Y1 = y + dir * 10,
			X2 = x + 12, Y2 = y + dir * 10,
			Stroke = brush,
			StrokeThickness = 2.5
		});

		_canvasPreview.Children.Add(new Line
		{
			X1 = x, Y1 = y,
			X2 = x, Y2 = y + dir * 10,
			Stroke = brush,
			StrokeThickness = 2
		});

		TextBlock lblSpr = new TextBlock
		{
			Text = isPendant ? "Pendant" : "Upright",
			FontSize = 10,
			FontWeight = FontWeights.Bold,
			Foreground = brush
		};
		Canvas.SetLeft(lblSpr, x + 16);
		Canvas.SetTop(lblSpr, y - 6);
		_canvasPreview.Children.Add(lblSpr);
	}

	private void BtnOK_Click(object sender, RoutedEventArgs e)
	{
		bool noZ = Array.IndexOf(_typesWithoutZOffset, SelectedType) >= 0;
		if (!noZ)
		{
			if (!double.TryParse(_txtZOffset.Text.Trim(), out double val) || val <= 0)
			{
				MessageBox.Show("Vui long nhap gia tri so hop le (> 0) cho Z-Offset!", "Thong bao", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			ZOffset = val;
		}
		else
		{
			ZOffset = 0.0;
		}

		if (_cboPipeSize.SelectedIndex == 1)
		{
			PipeDiameter = 32.0;
		}
		else
		{
			PipeDiameter = 25.0;
		}

		IsOK = true;
		DialogResult = true;
	}
}
