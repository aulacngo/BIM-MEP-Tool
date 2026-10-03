using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitTransform = Autodesk.Revit.DB.Transform;

namespace BIN;

public class AvoidClashWindow : Window
{
	private Document _doc;
	private Element _runningMepElem;
	private Element _obstacleElem;
	private RevitTransform _obstacleTransform;

	private BypassShapeMode _shapeMode = BypassShapeMode.U45;
	private bool _applying;
	private BypassDirection _direction = BypassDirection.Down;
	private double _clearanceMm = 50.0;

	private System.Windows.Controls.TextBox _txtClearance;
	private TextBlock _lblStatus;
	private Button _btnUp;
	private Button _btnDown;
	private Button _btnLeft;
	private Button _btnRight;
	private Button _btn45;
	private Button _btn90;
	private Button _btnZ45;
	private CheckBox _allowDisconnect;
	private TextBlock _lblModeNote;
	private bool _endConnected;
	private string _endDescription;
	private Canvas _canvas;

	public AvoidClashWindow(UIDocument uidoc, Element runningMepElem, Element obstacleElem, RevitTransform obstacleTransform = null)
	{
		_doc = uidoc.Document;
		_runningMepElem = runningMepElem;
		_obstacleElem = obstacleElem;
		_obstacleTransform = obstacleTransform ?? RevitTransform.Identity;

		MEPCurve curve = runningMepElem as MEPCurve;
        Autodesk.Revit.DB.Line line = (curve?.Location as LocationCurve)?.Curve as Autodesk.Revit.DB.Line;
        if (line != null)
        {
            XYZ end = line.GetEndPoint(1);
            Connector endConnector = curve.ConnectorManager.Connectors.Cast<Connector>()
                .Where(c => c.ConnectorType == ConnectorType.End).OrderBy(c => c.Origin.DistanceTo(end)).FirstOrDefault();
            _endConnected = endConnector != null && endConnector.IsConnected;
            _endDescription = "Đầu 2: (" + Math.Round(end.X * 304.8) + ", " + Math.Round(end.Y * 304.8) + ", "
                + Math.Round(end.Z * 304.8) + " mm)";
        }
        Title = "BIM TOOL - AVOID CLASH | PIPE + DUCT";
		Width = 480;
		SizeToContent = SizeToContent.Height;
		MaxHeight = SystemParameters.WorkArea.Height * 0.95;
		WindowStartupLocation = WindowStartupLocation.CenterOwner;
		ResizeMode = ResizeMode.NoResize;
		Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));

		BuildUI();
		PreviewKeyDown += AvoidClashWindow_PreviewKeyDown;
	}

	private void BuildUI()
	{
		System.Windows.Controls.Grid root = new System.Windows.Controls.Grid();
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) }); // Diagram
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer / Buttons

		// 1. HEADER
		Border header = new Border
		{
			Background = new LinearGradientBrush(
				System.Windows.Media.Color.FromRgb(15, 23, 42),
				System.Windows.Media.Color.FromRgb(30, 58, 138),
				45.0
			),
			Padding = new Thickness(16, 14, 16, 14)
		};
		StackPanel headerStack = new StackPanel();
		TextBlock txtTitle = new TextBlock
		{
			Text = "AVOID CLASH · PIPE + DUCT · U45 / U90 / Z45",
			Foreground = System.Windows.Media.Brushes.White,
			FontSize = 14,
			FontWeight = FontWeights.Bold
		};
		TextBlock txtSubtitle = new TextBlock
		{
			Text = "Ống nước · Ống gió tròn · Ống gió chữ nhật",
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
			FontSize = 11,
			Margin = new Thickness(0, 4, 0, 0)
		};
		TextBlock txtObstacle = new TextBlock
		{
			Text = "Vat can: " + (_obstacleElem?.Category?.Name ?? "Khong ro") + " - " + (_obstacleElem?.Name ?? "Khong ro"),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 219, 254)),
			FontSize = 10,
			Margin = new Thickness(0, 3, 0, 0),
			TextTrimming = TextTrimming.CharacterEllipsis
		};
		headerStack.Children.Add(txtTitle);
		headerStack.Children.Add(txtSubtitle);
		headerStack.Children.Add(txtObstacle);
		header.Child = headerStack;
		System.Windows.Controls.Grid.SetRow(header, 0);
		root.Children.Add(header);

		// 2. VECTOR DIAGRAM
		Border diagramBorder = new Border
		{
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
			Margin = new Thickness(14, 10, 14, 0),
			CornerRadius = new CornerRadius(8),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)),
			BorderThickness = new Thickness(1)
		};
		_canvas = new Canvas { Width = 440, Height = 170, ClipToBounds = true };
		UpdateDiagram();
		diagramBorder.Child = new Viewbox { Stretch = Stretch.Uniform, Child = _canvas };
		System.Windows.Controls.Grid.SetRow(diagramBorder, 1);
		root.Children.Add(diagramBorder);

		// 3. CONTROLS BODY
		StackPanel body = new StackPanel { Margin = new Thickness(14, 12, 14, 10) };

		// Angle Selection
		TextBlock lblAngle = new TextBlock
		{
			Text = "HÌNH DẠNG BYPASS:",
			FontWeight = FontWeights.SemiBold,
			FontSize = 11,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105))
		};
		body.Children.Add(lblAngle);

		System.Windows.Controls.Grid angleGrid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 4, 0, 10) };
		angleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		angleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
		angleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		_btn45 = CreateToggleButton("Cầu U 45° Thủy Lực", true);
		_btn45.Click += (s, e) => SelectShape(BypassShapeMode.U45);
		System.Windows.Controls.Grid.SetColumn(_btn45, 0);
		angleGrid.Children.Add(_btn45);

		_btn90 = CreateToggleButton("Cầu U 90° Vuông Góc", false);
		_btn90.Click += (s, e) => SelectShape(BypassShapeMode.U90);
		System.Windows.Controls.Grid.SetColumn(_btn90, 2);
		angleGrid.Children.Add(_btn90);

		body.Children.Add(angleGrid);
        _btnZ45 = CreateToggleButton("Bẻ Chữ Z 45° (Đổi cao độ) · 2 co 45°", false);
        _btnZ45.Click += (s, e) => SelectShape(BypassShapeMode.Z45);
        body.Children.Add(_btnZ45);
        _lblModeNote = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 6, 0, 6) };
        body.Children.Add(_lblModeNote);
        _allowDisconnect = new CheckBox { Content = "Cho phép ngắt đầu cuối đang nối (Z45)", FontSize = 11,
            Foreground = System.Windows.Media.Brushes.DarkOrange, Margin = new Thickness(0, 0, 0, 8) };
        body.Children.Add(_allowDisconnect);

		// Direction Selection
		TextBlock lblDir = new TextBlock
		{
			Text = "HƯỚNG NÉ (SPACE đảo Lên/Xuống):",
			FontWeight = FontWeights.SemiBold,
			FontSize = 11,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105))
		};
		body.Children.Add(lblDir);

		System.Windows.Controls.Grid dirGrid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 4, 0, 10) };
		dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
		dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
		dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
		dirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		_btnUp = CreateToggleButton("Lên ↑", false);
		_btnUp.Click += (s, e) => SelectDirection(BypassDirection.Up);
		System.Windows.Controls.Grid.SetColumn(_btnUp, 0);
		dirGrid.Children.Add(_btnUp);

		_btnDown = CreateToggleButton("Xuống ↓", true);
		_btnDown.Click += (s, e) => SelectDirection(BypassDirection.Down);
		System.Windows.Controls.Grid.SetColumn(_btnDown, 2);
		dirGrid.Children.Add(_btnDown);

		_btnLeft = CreateToggleButton("Trái ←", false);
		_btnLeft.Click += (s, e) => SelectDirection(BypassDirection.Left);
		System.Windows.Controls.Grid.SetColumn(_btnLeft, 4);
		dirGrid.Children.Add(_btnLeft);

		_btnRight = CreateToggleButton("Phải →", false);
		_btnRight.Click += (s, e) => SelectDirection(BypassDirection.Right);
		System.Windows.Controls.Grid.SetColumn(_btnRight, 6);
		dirGrid.Children.Add(_btnRight);

		body.Children.Add(dirGrid);

		// Clearance Input
		TextBlock lblClearance = new TextBlock
		{
			Text = "KHOẢNG HỞ AN TOÀN (mm):",
			FontWeight = FontWeights.SemiBold,
			FontSize = 11,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105))
		};
		body.Children.Add(lblClearance);

		_txtClearance = new System.Windows.Controls.TextBox
		{
			Text = "50",
			Height = 32,
			VerticalContentAlignment = VerticalAlignment.Center,
			Padding = new Thickness(8, 0, 8, 0),
			FontSize = 13,
			FontWeight = FontWeights.Bold,
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
			Margin = new Thickness(0, 4, 0, 10)
		};
		body.Children.Add(_txtClearance);

		// Status / Guide Card
		Border guideCard = new Border
		{
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(187, 247, 208)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(6),
			Padding = new Thickness(10, 8, 10, 8)
		};
		_lblStatus = new TextBlock
		{
			Text = "SPACE: đảo chiều · ENTER: thực hiện · ESC: đóng",
			FontSize = 11,
			TextWrapping = TextWrapping.Wrap,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 101, 52)),
			FontWeight = FontWeights.SemiBold
		};
		guideCard.Child = _lblStatus;
		body.Children.Add(guideCard);

		System.Windows.Controls.Grid.SetRow(body, 2);
		root.Children.Add(body);

		// 4. ACTION BUTTONS
		System.Windows.Controls.Grid actionGrid = new System.Windows.Controls.Grid { Margin = new Thickness(14, 0, 14, 14) };
		actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
		actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		Button btnCancel = new Button
		{
			Content = "ĐÓNG (ESC)",
			Height = 38,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105)),
			FontWeight = FontWeights.SemiBold
		};
		btnCancel.Click += (s, e) => Close();
		System.Windows.Controls.Grid.SetColumn(btnCancel, 0);
		actionGrid.Children.Add(btnCancel);

		Button btnApply = new Button
		{
			Content = "THỰC HIỆN (ENTER)",
			Height = 38,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
			Foreground = System.Windows.Media.Brushes.White,
			FontWeight = FontWeights.Bold
		};
		btnApply.Click += (s, e) => ApplyBypass();
		System.Windows.Controls.Grid.SetColumn(btnApply, 2);
		actionGrid.Children.Add(btnApply);

		System.Windows.Controls.Grid.SetRow(actionGrid, 3);
		root.Children.Add(actionGrid);

        Content = new ScrollViewer { Background = Background, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = root };
        SelectShape(BypassShapeMode.U45);
	}

	private void UpdateDiagram()
	{
		if (_canvas == null) return;
		_canvas.Children.Clear();

		SolidColorBrush obstacleBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));
		SolidColorBrush pipeBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
		SolidColorBrush lateralBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(124, 58, 237));
		const double centerY = 96.0;
		const double obstacleX = 174.0;
		const double obstacleY = 78.0;

		AddDiagramBadge(GetDiagramBadgeText());
		System.Windows.Shapes.Rectangle obstacle = new System.Windows.Shapes.Rectangle
		{
			Width = 72,
			Height = 36,
			Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226)),
			Stroke = obstacleBrush,
			StrokeThickness = 2,
			RadiusX = 3,
			RadiusY = 3
		};
		Canvas.SetLeft(obstacle, obstacleX);
		Canvas.SetTop(obstacle, obstacleY);
		_canvas.Children.Add(obstacle);

		bool verticalBypass = _direction == BypassDirection.Down || _direction == BypassDirection.Up;
		bool lowerOffset = _direction == BypassDirection.Down || _direction == BypassDirection.Left;
		double bypassY = lowerOffset ? 142.0 : 50.0;
		SolidColorBrush pathBrush = verticalBypass ? pipeBrush : lateralBrush;
		DrawPipePath(BuildDiagramPath(centerY, bypassY), pathBrush);

		if (verticalBypass)
		{
			AddDiagramText("ĐẦU 1", 22, centerY + 7, pipeBrush);
		}
		else
		{
			AddDiagramText(_direction == BypassDirection.Left
				? "<- NE TRAI (mat bang)"
				: "NE PHAI (mat bang) ->",
				126, lowerOffset ? 154 : 32, lateralBrush);
		}
		AddDiagramText("VẬT CẢN / DẦM", obstacleX + 4, obstacleY + 11, obstacleBrush);
	}

	private List<System.Windows.Point> BuildDiagramPath(double centerY, double bypassY)
	{
		if (_shapeMode == BypassShapeMode.U90)
		{
			return new List<System.Windows.Point>
			{
				new System.Windows.Point(18, centerY), new System.Windows.Point(140, centerY),
				new System.Windows.Point(140, bypassY), new System.Windows.Point(280, bypassY),
				new System.Windows.Point(280, centerY), new System.Windows.Point(422, centerY)
			};
		}
        double run = Math.Abs(bypassY - centerY);
        var points = new List<System.Windows.Point> {
            new System.Windows.Point(18, centerY), new System.Windows.Point(160 - run, centerY),
            new System.Windows.Point(160, bypassY) };
        if (_shapeMode == BypassShapeMode.Z45) points.Add(new System.Windows.Point(422, bypassY));
        else {
            points.Add(new System.Windows.Point(260, bypassY));
            points.Add(new System.Windows.Point(260 + run, centerY));
            points.Add(new System.Windows.Point(422, centerY));
        }
        return points;
    }

    private string GetDiagramBadgeText()
    {
        string mode = _shapeMode == BypassShapeMode.U45 ? "U45 · 4 co 45°" : _shapeMode == BypassShapeMode.U90 ? "U90 · 4 co 90°" : "Z45 · 2 co 45° · Đi tiếp ở vị trí mới";
        return mode + ((_direction == BypassDirection.Up || _direction == BypassDirection.Down) ? " · Mặt đứng" : " · Mặt bằng");
    }

	private void AddDiagramBadge(string text)
	{
		Border badge = new Border
		{
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(219, 234, 254)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(147, 197, 253)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(4),
			Padding = new Thickness(6, 2, 6, 2),
			Child = new TextBlock
			{
				Text = text,
				FontSize = 10,
				FontWeight = FontWeights.SemiBold,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 64, 175))
			}
		};
		Canvas.SetLeft(badge, 10);
		Canvas.SetTop(badge, 7);
		_canvas.Children.Add(badge);
	}

	private void DrawPipePath(IList<System.Windows.Point> points, SolidColorBrush brush)
	{
		if (points == null || points.Count < 2) return;
		PathFigure figure = new PathFigure { StartPoint = points[0] };
		for (int i = 1; i < points.Count; i++)
		{
			figure.Segments.Add(new System.Windows.Media.LineSegment(points[i], true));
		}
		PathGeometry geometry = new PathGeometry();
		geometry.Figures.Add(figure);
		_canvas.Children.Add(new System.Windows.Shapes.Path
		{
			Data = geometry,
			Stroke = brush,
			StrokeThickness = 4,
			StrokeLineJoin = PenLineJoin.Round,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round
		});
        for (int i = 1; i < points.Count - 1; i++)
        {
            var marker = new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = System.Windows.Media.Brushes.White,
                Stroke = brush, StrokeThickness = 2 };
            Canvas.SetLeft(marker, points[i].X - 4); Canvas.SetTop(marker, points[i].Y - 4);
            _canvas.Children.Add(marker);
        }
        System.Windows.Point end = points[points.Count - 1];
        _canvas.Children.Add(new Polyline { Stroke = brush, StrokeThickness = 3,
            Points = new PointCollection { new System.Windows.Point(end.X - 9, end.Y - 5), end,
                new System.Windows.Point(end.X - 9, end.Y + 5) } });
        AddDiagramText(_shapeMode == BypassShapeMode.Z45 ? "ĐẦU 2 · VỊ TRÍ MỚI →" : "ĐẦU 2 · VỊ TRÍ CŨ",
            _shapeMode == BypassShapeMode.Z45 ? 287 : 335, end.Y + 10, brush);
	}

	private void AddDiagramText(string text, double left, double top, SolidColorBrush brush)
	{
		TextBlock label = new TextBlock
		{
			Text = text,
			FontSize = 9,
			FontWeight = FontWeights.Bold,
			Foreground = brush
		};
		Canvas.SetLeft(label, left);
		Canvas.SetTop(label, top);
		_canvas.Children.Add(label);
	}

	private Button CreateToggleButton(string text, bool isSelected)
	{
		Button btn = new Button
		{
			Content = text,
			Height = 32,
			FontWeight = FontWeights.SemiBold,
			FontSize = 11
		};
		UpdateToggleStyle(btn, isSelected);
		return btn;
	}

	private void UpdateToggleStyle(Button btn, bool isSelected)
	{
		if (isSelected)
		{
			btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(219, 234, 254));
			btn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 78, 216));
			btn.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246));
			btn.BorderThickness = new Thickness(2);
		}
		else
		{
			btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
			btn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));
			btn.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
			btn.BorderThickness = new Thickness(1);
		}
	}

    private void SelectShape(BypassShapeMode mode)
    {
        _shapeMode = mode;
        UpdateToggleStyle(_btn45, mode == BypassShapeMode.U45);
        UpdateToggleStyle(_btn90, mode == BypassShapeMode.U90);
        UpdateToggleStyle(_btnZ45, mode == BypassShapeMode.Z45);
        bool z = mode == BypassShapeMode.Z45;
        _allowDisconnect.Visibility = z && _endConnected ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        if (!z) _allowDisconnect.IsChecked = false;
        _lblModeNote.Text = z ? "Giữ đầu 1; chuyển đầu 2 theo hướng né. " + _endDescription
            + (_endConnected ? " · Đầu 2 đang nối: cần cho phép ngắt kết nối." : " · Đầu 2 đang mở.")
            : "Giữ vị trí và kết nối hai đầu; trở về cao độ cũ sau vật cản.";
        UpdateDiagram();
    }

	private void SelectDirection(BypassDirection dir)
	{
		_direction = dir;
		UpdateToggleStyle(_btnUp, dir == BypassDirection.Up);
		UpdateToggleStyle(_btnDown, dir == BypassDirection.Down);
		UpdateToggleStyle(_btnLeft, dir == BypassDirection.Left);
		UpdateToggleStyle(_btnRight, dir == BypassDirection.Right);
		UpdateDiagram();
	}

	private void AvoidClashWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.IsRepeat) return;
		if (e.Key == Key.Space)
		{
			// Toggle Up <-> Down
			SelectDirection(_direction == BypassDirection.Up ? BypassDirection.Down : BypassDirection.Up);
			e.Handled = true;
		}
		else if (e.Key == Key.Enter)
		{
			ApplyBypass();
			e.Handled = true;
		}
		else if (e.Key == Key.Escape)
		{
			Close();
			e.Handled = true;
		}
	}

    private void ApplyBypass()
    {
        if (_applying) return;
        if (!(double.TryParse(_txtClearance.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double val)
            || double.TryParse(_txtClearance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
            || !AvoidClashGeometry.Finite(val) || val < 10)
        {
            _lblStatus.Text = "Lỗi: Khoảng hở phải là số hữu hạn, tối thiểu 10 mm.";
            _lblStatus.Foreground = System.Windows.Media.Brushes.Red;
            _txtClearance.Focus();
            return;
        }
        _clearanceMm = val;
        _applying = true;
        try
        {
            bool success = AvoidClashCmd.ExecuteBypass(_doc, _runningMepElem, _obstacleElem, _obstacleTransform,
                _shapeMode, _direction, _clearanceMm, out string err, _allowDisconnect.IsChecked == true);
            if (success) DialogResult = true;
            else { _lblStatus.Text = "Lỗi: " + err; _lblStatus.Foreground = System.Windows.Media.Brushes.Red; }
        }
        finally { _applying = false; }
    }
}
