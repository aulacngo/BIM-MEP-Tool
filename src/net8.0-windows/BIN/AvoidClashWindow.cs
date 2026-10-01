using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public enum BypassDirection
{
	Up,
	Down,
	Left,
	Right
}

public class AvoidClashWindow : Window
{
	private UIDocument _uidoc;
	private Document _doc;
	private Element _runningPipeElem;
	private Element _obstacleElem;

	private double _angleDegree = 45.0;
	private BypassDirection _direction = BypassDirection.Up;
	private double _clearanceMm = 50.0;

	private System.Windows.Controls.TextBox _txtClearance;
	private TextBlock _lblStatus;
	private Button _btnUp;
	private Button _btnDown;
	private Button _btnLeft;
	private Button _btnRight;
	private Button _btn45;
	private Button _btn90;

	public AvoidClashWindow(UIDocument uidoc, Element runningPipeElem, Element obstacleElem)
	{
		_uidoc = uidoc;
		_doc = uidoc.Document;
		_runningPipeElem = runningPipeElem;
		_obstacleElem = obstacleElem;

		Title = "BIM TOOL - AVOID CLASH (NE VA CHAM TU DONG)";
		Width = 430;
		Height = 560;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		ResizeMode = ResizeMode.NoResize;
		Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));

		BuildUI();
		PreviewKeyDown += AvoidClashWindow_PreviewKeyDown;
	}

	private void BuildUI()
	{
		System.Windows.Controls.Grid root = new System.Windows.Controls.Grid();
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(140) }); // Diagram
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
			Text = "BIM TOOL - AVOID CLASH (Bypass 45 / 90 deg)",
			Foreground = System.Windows.Media.Brushes.White,
			FontSize = 14,
			FontWeight = FontWeights.Bold
		};
		TextBlock txtSubtitle = new TextBlock
		{
			Text = "Tu dong chen 4 cut ne ong, ong gio, mang cap va dam ket cau",
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
			FontSize = 11,
			Margin = new Thickness(0, 4, 0, 0)
		};
		headerStack.Children.Add(txtTitle);
		headerStack.Children.Add(txtSubtitle);
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
		Canvas canvas = new Canvas { ClipToBounds = true };
		DrawDiagram(canvas);
		diagramBorder.Child = canvas;
		System.Windows.Controls.Grid.SetRow(diagramBorder, 1);
		root.Children.Add(diagramBorder);

		// 3. CONTROLS BODY
		StackPanel body = new StackPanel { Margin = new Thickness(14, 12, 14, 10) };

		// Angle Selection
		TextBlock lblAngle = new TextBlock
		{
			Text = "GOC NE VA CHAM (Bypass Angle):",
			FontWeight = FontWeights.SemiBold,
			FontSize = 11,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105))
		};
		body.Children.Add(lblAngle);

		System.Windows.Controls.Grid angleGrid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 4, 0, 10) };
		angleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		angleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
		angleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

		_btn45 = CreateToggleButton("45 deg (Chuan Thuy Luc)", true);
		_btn45.Click += (s, e) => SelectAngle(45.0);
		System.Windows.Controls.Grid.SetColumn(_btn45, 0);
		angleGrid.Children.Add(_btn45);

		_btn90 = CreateToggleButton("90 deg (Vuong Goc)", false);
		_btn90.Click += (s, e) => SelectAngle(90.0);
		System.Windows.Controls.Grid.SetColumn(_btn90, 2);
		angleGrid.Children.Add(_btn90);

		body.Children.Add(angleGrid);

		// Direction Selection
		TextBlock lblDir = new TextBlock
		{
			Text = "HUONG NE (Go SPACE de dao Len/Xuong):",
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

		_btnUp = CreateToggleButton("Up (Len)", true);
		_btnUp.Click += (s, e) => SelectDirection(BypassDirection.Up);
		System.Windows.Controls.Grid.SetColumn(_btnUp, 0);
		dirGrid.Children.Add(_btnUp);

		_btnDown = CreateToggleButton("Down (Xuong)", false);
		_btnDown.Click += (s, e) => SelectDirection(BypassDirection.Down);
		System.Windows.Controls.Grid.SetColumn(_btnDown, 2);
		dirGrid.Children.Add(_btnDown);

		_btnLeft = CreateToggleButton("Left (Trai)", false);
		_btnLeft.Click += (s, e) => SelectDirection(BypassDirection.Left);
		System.Windows.Controls.Grid.SetColumn(_btnLeft, 4);
		dirGrid.Children.Add(_btnLeft);

		_btnRight = CreateToggleButton("Right (Phai)", false);
		_btnRight.Click += (s, e) => SelectDirection(BypassDirection.Right);
		System.Windows.Controls.Grid.SetColumn(_btnRight, 6);
		dirGrid.Children.Add(_btnRight);

		body.Children.Add(dirGrid);

		// Clearance Input
		TextBlock lblClearance = new TextBlock
		{
			Text = "KHOANG HO AN TOAN (Clearance - mm):",
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
			Text = "Phim tat: [ SPACE ] dao chieu | [ ENTER ] thuc hien | [ ESC ] huy",
			FontSize = 11,
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
			Content = "Dong (ESC)",
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
			Content = "THUC HIEN NE (ENTER)",
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

		Content = root;
	}

	private void DrawDiagram(Canvas canvas)
	{
		// Draw 45 deg bridge over obstacle
		System.Windows.Shapes.Ellipse obs = new System.Windows.Shapes.Ellipse
		{
			Width = 36,
			Height = 36,
			Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
			Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28)),
			StrokeThickness = 2
		};
		Canvas.SetLeft(obs, 180);
		Canvas.SetTop(obs, 65);
		canvas.Children.Add(obs);

		// Running pipe path
		PathGeometry geom = new PathGeometry();
		PathFigure fig = new PathFigure { StartPoint = new System.Windows.Point(20, 83) };
		fig.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(120, 83), true));
		fig.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(165, 35), true));
		fig.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(230, 35), true));
		fig.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(275, 83), true));
		fig.Segments.Add(new System.Windows.Media.LineSegment(new System.Windows.Point(380, 83), true));
		geom.Figures.Add(fig);

		System.Windows.Shapes.Path path = new System.Windows.Shapes.Path
		{
			Data = geom,
			Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
			StrokeThickness = 4
		};
		canvas.Children.Add(path);

		// Text labels
		TextBlock lblPipe = new TextBlock
		{
			Text = "ONG CHINH",
			FontSize = 10,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235))
		};
		Canvas.SetLeft(lblPipe, 30);
		Canvas.SetTop(lblPipe, 90);
		canvas.Children.Add(lblPipe);

		TextBlock lblObs = new TextBlock
		{
			Text = "VAT CAN",
			FontSize = 10,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68))
		};
		Canvas.SetLeft(lblObs, 175);
		Canvas.SetTop(lblObs, 105);
		canvas.Children.Add(lblObs);
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

	private void SelectAngle(double angle)
	{
		_angleDegree = angle;
		UpdateToggleStyle(_btn45, angle == 45.0);
		UpdateToggleStyle(_btn90, angle == 90.0);
	}

	private void SelectDirection(BypassDirection dir)
	{
		_direction = dir;
		UpdateToggleStyle(_btnUp, dir == BypassDirection.Up);
		UpdateToggleStyle(_btnDown, dir == BypassDirection.Down);
		UpdateToggleStyle(_btnLeft, dir == BypassDirection.Left);
		UpdateToggleStyle(_btnRight, dir == BypassDirection.Right);
	}

	private void AvoidClashWindow_PreviewKeyDown(object sender, KeyEventArgs e)
	{
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
		if (double.TryParse(_txtClearance.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
		{
			_clearanceMm = Math.Max(10.0, val);
		}

		bool success = AvoidClashCmd.ExecuteBypass(_doc, _runningPipeElem, _obstacleElem, _angleDegree, _direction, _clearanceMm, out string err);
		if (success)
		{
			Close();
		}
		else
		{
			_lblStatus.Text = "Loi: " + err;
			_lblStatus.Foreground = System.Windows.Media.Brushes.Red;
		}
	}
}
