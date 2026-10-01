using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using WGrid = System.Windows.Controls.Grid;

namespace BIN;

public class SuppressAllWarnings : IFailuresPreprocessor
{
	public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
	{
		IList<FailureMessageAccessor> failList = failuresAccessor.GetFailureMessages();
		foreach (FailureMessageAccessor failure in failList)
		{
			if (failure.GetSeverity() == FailureSeverity.Warning)
			{
				failuresAccessor.DeleteWarning(failure);
			}
		}
		return FailureProcessingResult.Continue;
	}
}

public class RotateElementsWindow : Window
{
	private UIDocument _uidoc;
	private Document _doc;
	private List<RotationGroup> _groups;
	private bool _isMultiMode;

	private double _angleDegree = 45.0;
	private double _totalAngleRotated = 0.0;

	private System.Windows.Controls.TextBox _txtAngle;
	private TextBlock _lblStatus;

	public RotateElementsWindow(UIDocument uidoc, List<RotationGroup> groups, bool isMultiMode = false)
	{
		_uidoc = uidoc;
		_doc = uidoc.Document;
		_groups = groups;
		_isMultiMode = isMultiMode;

		InitializeUI();
		SetupKeyboardShortcuts();
	}

	private void SetupKeyboardShortcuts()
	{
		this.Focusable = true;
		this.Loaded += (s, e) =>
		{
			this.Activate();
			this.Focus();
		};

		this.PreviewKeyDown += (s, e) =>
		{
			if (e.Key == Key.Space)
			{
				e.Handled = true;
				if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
				{
					Rotate(-_angleDegree);
				}
				else
				{
					Rotate(_angleDegree);
				}
			}
			else if (e.Key == Key.Escape || e.Key == Key.Return)
			{
				e.Handled = true;
				Close();
			}
			else if (e.Key == Key.Left)
			{
				e.Handled = true;
				Rotate(-_angleDegree);
			}
			else if (e.Key == Key.Right)
			{
				e.Handled = true;
				Rotate(_angleDegree);
			}
		};
	}

	private void InitializeUI()
	{
		Title = _isMultiMode ? "BIM - Rotate Multi" : "BIM - Rotate";
		Width = 410;
		Height = 560;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		Topmost = true;
		ResizeMode = ResizeMode.NoResize;
		Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));

		var mainGrid = new WGrid { Margin = new Thickness(14) };
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Diagram
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Guide Card
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: Angle Inputs
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: Action Buttons
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 5: Target / Rollback
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 6: Status
		mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 7: Footer

		int totalElems = _groups.Sum(g => g.ElementIds.Count);
		string subInfo = _isMultiMode || _groups.Count > 1
			? string.Format("\u0110ang ch\u1ecdn: {0} c\u1ee5m ({1} \u0111\u1ed1i t\u01b0\u1ee3ng) tr\u00ean {0} tuy\u1ebfn \u1ed1ng ri\u00eang bi\u1ec7t", _groups.Count, totalElems)
			: string.Format("\u0110ang ch\u1ecdn: {0} \u0111\u1ed1i t\u01b0\u1ee3ng xoay quanh tr\u1ee5c \u0111\u00e3 ch\u1ecdn", totalElems);

		// 1. Header
		var headerPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical, Margin = new Thickness(0, 0, 0, 6) };
		var titleBlock = new TextBlock
		{
			Text = _isMultiMode ? "Xoay \u0110\u1ed3ng Lo\u1ea1t \u0110a Tr\u1ee5c (Rotate Multi)" : "Xoay C\u00fat & Ph\u1ee5 Ki\u1ec7n (Rotate)",
			FontSize = 15,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59))
		};
		var subBlock = new TextBlock
		{
			Text = subInfo,
			FontSize = 11,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)),
			Margin = new Thickness(0, 2, 0, 0)
		};
		headerPanel.Children.Add(titleBlock);
		headerPanel.Children.Add(subBlock);
		WGrid.SetRow(headerPanel, 0);
		mainGrid.Children.Add(headerPanel);

		// 2. Illustration Diagram
		var diagramBorder = CreateIllustrationDiagram();
		WGrid.SetRow(diagramBorder, 1);
		mainGrid.Children.Add(diagramBorder);

		// 3. Instructions Guide Card
		var guideCard = new Border
		{
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 242, 255)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(199, 210, 254)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(6),
			Padding = new Thickness(8, 6, 8, 6),
			Margin = new Thickness(0, 0, 0, 8)
		};
		var guidePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
		var guideTitle = new TextBlock
		{
			Text = "\u2328 H\u01b0\u1edbng d\u1eabn ph\u00edm t\u1eaft nhanh:",
			FontWeight = FontWeights.Bold,
			FontSize = 11,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(67, 56, 202))
		};
		var guideTxt = new TextBlock
		{
			Text = "\u2022 [ SPACE ] ho\u1eb7c [ \u2192 ] : Xoay ti\u1ebfp theo b\u01b0\u1edbc g\u00f3c (+Angle)\n\u2022 [ Shift + SPACE ] ho\u1eb7c [ \u2190 ] : Xoay l\u00f9i ng\u01b0\u1ee3c l\u1ea1i (-Angle)\n\u2022 [ Esc / Enter ] : L\u01b0u v\u1ecb tr\u00ed & Ho\u00e0n t\u1ea5t",
			FontSize = 10.5,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(79, 70, 229)),
			Margin = new Thickness(0, 2, 0, 0)
		};
		guidePanel.Children.Add(guideTitle);
		guidePanel.Children.Add(guideTxt);
		guideCard.Child = guidePanel;
		WGrid.SetRow(guideCard, 2);
		mainGrid.Children.Add(guideCard);

		// 4. Buoc goc xoay (Presets + Custom)
		var angleGroup = new GroupBox
		{
			Header = "Buoc goc xoay (Angle Step)",
			Padding = new Thickness(6),
			Margin = new Thickness(0, 0, 0, 8),
			FontWeight = FontWeights.SemiBold
		};
		var anglePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
		string[] presets = new[] { "45 deg", "90 deg", "180 deg" };
		foreach (var p in presets)
		{
			var btn = new System.Windows.Controls.Button
			{
				Content = p,
				Width = 60,
				Height = 26,
				Margin = new Thickness(0, 0, 8, 0),
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)),
				FontWeight = FontWeights.Normal,
				Focusable = false
			};
			string localP = p;
			btn.Click += (s, e) =>
			{
				double val = double.Parse(localP.Replace(" deg", ""));
				_angleDegree = val;
				_txtAngle.Text = val.ToString();
				this.Focus();
			};
			anglePanel.Children.Add(btn);
		}

		_txtAngle = new System.Windows.Controls.TextBox
		{
			Text = "45",
			Width = 50,
			Height = 26,
			VerticalContentAlignment = VerticalAlignment.Center,
			HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
			FontWeight = FontWeights.Bold
		};
		_txtAngle.TextChanged += (s, e) =>
		{
			double customVal;
			if (double.TryParse(_txtAngle.Text, out customVal))
			{
				_angleDegree = customVal;
			}
		};
		anglePanel.Children.Add(_txtAngle);
		var lblDeg = new TextBlock { Text = "deg", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
		anglePanel.Children.Add(lblDeg);

		angleGroup.Content = anglePanel;
		WGrid.SetRow(angleGroup, 3);
		mainGrid.Children.Add(angleGroup);

		// 5. Nut xoay Trai / Phai
		var rotateGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 6) };
		var btnLeft = new System.Windows.Controls.Button
		{
			Content = "<- Xoay Trai (-Angle)",
			Height = 32,
			Margin = new Thickness(0, 0, 4, 0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
			Foreground = System.Windows.Media.Brushes.White,
			FontWeight = FontWeights.Bold,
			Focusable = false
		};
		btnLeft.Click += (s, e) => { Rotate(-_angleDegree); this.Focus(); };

		var btnRight = new System.Windows.Controls.Button
		{
			Content = "-> Xoay Phai (+Angle)",
			Height = 32,
			Margin = new Thickness(4, 0, 0, 0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
			Foreground = System.Windows.Media.Brushes.White,
			FontWeight = FontWeights.Bold,
			Focusable = false
		};
		btnRight.Click += (s, e) => { Rotate(_angleDegree); this.Focus(); };

		rotateGrid.Children.Add(btnLeft);
		rotateGrid.Children.Add(btnRight);
		WGrid.SetRow(rotateGrid, 4);
		mainGrid.Children.Add(rotateGrid);

		// 6. Target / Rollback
		var extraGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 6) };
		var btnTarget = new System.Windows.Controls.Button
		{
			Content = "Target Point",
			Height = 30,
			Margin = new Thickness(0, 0, 4, 0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)),
			Foreground = System.Windows.Media.Brushes.White,
			FontWeight = FontWeights.SemiBold,
			Focusable = false
		};
		btnTarget.Click += (s, e) => PointToTarget();

		var btnRollback = new System.Windows.Controls.Button
		{
			Content = "\u21ba Kh\u00f4i Ph\u1ee5c (0\u00b0)",
			Height = 30,
			Margin = new Thickness(4, 0, 0, 0),
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),
			Foreground = System.Windows.Media.Brushes.White,
			FontWeight = FontWeights.SemiBold,
			Focusable = false
		};
		btnRollback.Click += (s, e) => { Rollback(); this.Focus(); };

		extraGrid.Children.Add(btnTarget);
		extraGrid.Children.Add(btnRollback);
		WGrid.SetRow(extraGrid, 5);
		mainGrid.Children.Add(extraGrid);

		// 7. Status Text
		_lblStatus = new TextBlock
		{
			Text = "T\u1ed5ng g\u00f3c \u0111\u00e3 xoay: 0\u00b0 (V\u1ecb tr\u00ed: 0\u00b0)",
			FontSize = 11,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105)),
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 2, 0, 4)
		};
		WGrid.SetRow(_lblStatus, 6);
		mainGrid.Children.Add(_lblStatus);

		// 8. Footer Close Button
		var btnClose = new System.Windows.Controls.Button
		{
			Content = "\u2714 Ho\u00e0n t\u1ea5t & \u0110\u00f3ng (Esc)",
			Height = 32,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
			FontWeight = FontWeights.SemiBold,
			Focusable = false
		};
		btnClose.Click += (s, e) => Close();
		WGrid.SetRow(btnClose, 7);
		mainGrid.Children.Add(btnClose);

		Content = mainGrid;
	}

	private Border CreateIllustrationDiagram()
	{
		var border = new Border
		{
			Height = 90,
			Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
			BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
			BorderThickness = new Thickness(1),
			CornerRadius = new CornerRadius(6),
			Margin = new Thickness(0, 0, 0, 8),
			ClipToBounds = true
		};

		var canvas = new Canvas { Width = 370, Height = 90 };

		// 1. Truc ong chinh (Main Axis)
		var axisLine = new System.Windows.Shapes.Line
		{
			X1 = 30, Y1 = 55,
			X2 = 340, Y2 = 55,
			Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
			StrokeThickness = 10,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round
		};
		canvas.Children.Add(axisLine);

		var axisCenter = new System.Windows.Shapes.Line
		{
			X1 = 20, Y1 = 55,
			X2 = 350, Y2 = 55,
			Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
			StrokeThickness = 1.5,
			StrokeDashArray = new DoubleCollection { 4, 3 }
		};
		canvas.Children.Add(axisCenter);

		var lblAxis = new TextBlock
		{
			Text = "<-- Truc Xoay Co Dinh (Axis Pipe) -->",
			FontSize = 9.5,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38))
		};
		Canvas.SetLeft(lblAxis, 35);
		Canvas.SetTop(lblAxis, 65);
		canvas.Children.Add(lblAxis);

		// 2. Cut xoay & nhanh dung
		var elbowShape = new System.Windows.Shapes.Ellipse
		{
			Width = 20, Height = 20,
			Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
			Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 78, 216)),
			StrokeThickness = 2
		};
		Canvas.SetLeft(elbowShape, 215);
		Canvas.SetTop(elbowShape, 45);
		canvas.Children.Add(elbowShape);

		var branchPipe = new System.Windows.Shapes.Line
		{
			X1 = 225, Y1 = 45,
			X2 = 225, Y2 = 12,
			Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
			StrokeThickness = 8,
			StrokeStartLineCap = PenLineCap.Round,
			StrokeEndLineCap = PenLineCap.Round
		};
		canvas.Children.Add(branchPipe);

		var lblRot = new TextBlock
		{
			Text = "\u21ba Xoay 360\u00b0",
			FontSize = 9.5,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105))
		};
		Canvas.SetLeft(lblRot, 240);
		Canvas.SetTop(lblRot, 15);
		canvas.Children.Add(lblRot);

		border.Child = canvas;
		return border;
	}

	private void Rotate(double angleDeg)
	{
		if (_groups == null || _groups.Count == 0) return;
		try
		{
			double rad = angleDeg * Math.PI / 180.0;
			using (Transaction trans = new Transaction(_doc, "BIN Rotate Elements"))
			{
				FailureHandlingOptions opts = trans.GetFailureHandlingOptions();
				opts.SetFailuresPreprocessor(new SuppressAllWarnings());
				opts.SetClearAfterRollback(true);
				trans.SetFailureHandlingOptions(opts);

				trans.Start();
				foreach (var g in _groups)
				{
					if (g.ElementIds.Count > 0 && g.AxisLine != null)
					{
						ElementTransformUtils.RotateElements(_doc, g.ElementIds, g.AxisLine, rad);
					}
				}
				trans.Commit();
			}
			_totalAngleRotated += angleDeg;
			_uidoc.RefreshActiveView();
			UpdateStatus();
		}
		catch (Exception ex)
		{
			TaskDialog.Show("Rotate Error", ex.Message);
		}
	}

	private void Rollback()
	{
		if (Math.Abs(_totalAngleRotated) > 0.001)
		{
			try
			{
				double rad = -_totalAngleRotated * Math.PI / 180.0;
				using (Transaction trans = new Transaction(_doc, "BIN Rollback Rotate"))
				{
					FailureHandlingOptions opts = trans.GetFailureHandlingOptions();
					opts.SetFailuresPreprocessor(new SuppressAllWarnings());
					opts.SetClearAfterRollback(true);
					trans.SetFailureHandlingOptions(opts);

					trans.Start();
					foreach (var g in _groups)
					{
						if (g.ElementIds.Count > 0 && g.AxisLine != null)
						{
							ElementTransformUtils.RotateElements(_doc, g.ElementIds, g.AxisLine, rad);
						}
					}
					trans.Commit();
				}
				_totalAngleRotated = 0.0;
				_uidoc.RefreshActiveView();
				UpdateStatus();
			}
			catch (Exception ex)
			{
				TaskDialog.Show("Rollback Error", ex.Message);
			}
		}
	}

	private void UpdateStatus()
	{
		double normalized = _totalAngleRotated % 360.0;
		if (normalized < 0) normalized += 360.0;
		_lblStatus.Text = string.Format("T\u1ed5ng g\u00f3c \u0111\u00e3 xoay: {0}\u00b0 (V\u1ecb tr\u00ed: {1}\u00b0)", Math.Round(_totalAngleRotated, 1), Math.Round(normalized, 1));
	}

	private void PointToTarget()
	{
		Hide();
		try
		{
			Reference rBranch = _uidoc.Selection.PickObject(ObjectType.Element, new AxisSelectionFilter(), "1. Ch\u1ecdn \u1ed1ng nh\u00e1nh l\u00e0m CHU\u1ea8N H\u01af\u1edaNG");
			if (rBranch == null) return;
			Element branchElem = _doc.GetElement(rBranch);

			Reference rTarget = _uidoc.Selection.PickObject(ObjectType.Element, new AxisSelectionFilter(), "2. Ch\u1ecdn \u1ed1ng l\u00e0m M\u1ee4C TI\u00caU C\u1ea6N H\u01af\u1edaNG T\u1edaI");
			if (rTarget == null) return;
			Element targetElem = _doc.GetElement(rTarget);

			Autodesk.Revit.DB.Line baseAxis = _groups.FirstOrDefault()?.AxisLine;
			if (baseAxis != null)
			{
				double angleRad = CalculateAngleToTarget(baseAxis, branchElem, targetElem);
				if (Math.Abs(angleRad) > 0.001)
				{
					using (Transaction trans = new Transaction(_doc, "BIN Point To Target"))
					{
						FailureHandlingOptions opts = trans.GetFailureHandlingOptions();
						opts.SetFailuresPreprocessor(new SuppressAllWarnings());
						opts.SetClearAfterRollback(true);
						trans.SetFailureHandlingOptions(opts);

						trans.Start();
						foreach (var g in _groups)
						{
							if (g.ElementIds.Count > 0 && g.AxisLine != null)
							{
								ElementTransformUtils.RotateElements(_doc, g.ElementIds, g.AxisLine, angleRad);
							}
						}
						trans.Commit();
					}
					_totalAngleRotated += angleRad * 180.0 / Math.PI;
					_uidoc.RefreshActiveView();
					UpdateStatus();
				}
			}
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
		catch (Exception ex)
		{
			TaskDialog.Show("Error", ex.Message);
		}
		finally
		{
			ShowDialog();
		}
	}

	private double CalculateAngleToTarget(Autodesk.Revit.DB.Line axisLine, Element branchElem, Element targetElem)
	{
		LocationCurve lcBranch = branchElem?.Location as LocationCurve;
		Autodesk.Revit.DB.Line lBranch = lcBranch?.Curve as Autodesk.Revit.DB.Line;
		LocationCurve lcTarget = targetElem?.Location as LocationCurve;
		Autodesk.Revit.DB.Line lTarget = lcTarget?.Curve as Autodesk.Revit.DB.Line;

		if (lBranch == null || lTarget == null)
		{
			TaskDialog.Show("L\u1ed7i", "Kh\u00f4ng th\u1ec3 x\u00e1c \u0111\u1ecbnh \u0111\u01b0\u1eddng t\u00e2m tr\u1ee5c \u1ed1ng.");
			return 0.0;
		}

		XYZ origin = axisLine.Origin;
		XYZ dirAxis = axisLine.Direction.Normalize();
		XYZ dirBranch = lBranch.Direction.Normalize();

		double num = (lBranch.Evaluate(0.5, true) - origin).DotProduct(dirAxis);
		XYZ pInt = origin + dirAxis * num;

		XYZ origin2 = lTarget.Origin;
		XYZ dirTarget = lTarget.Direction.Normalize();

		XYZ val5 = origin2 - (origin2 - pInt).DotProduct(dirAxis) * dirAxis;
		XYZ val6 = dirTarget - dirTarget.DotProduct(dirAxis) * dirAxis;

		XYZ zero = XYZ.Zero;
		if (val6.IsAlmostEqualTo(XYZ.Zero))
		{
			zero = val5 - pInt;
		}
		else
		{
			val6 = val6.Normalize();
			double num3 = (pInt - val5).DotProduct(val6);
			XYZ val7 = val5 + val6 * num3 - pInt;
			XYZ val8 = dirTarget.CrossProduct(pInt - origin2);
			if (!val8.IsAlmostEqualTo(XYZ.Zero))
			{
				val8 = val8.Normalize();
				XYZ p2 = pInt + val8;
				XYZ val9 = p2 - (p2 - pInt).DotProduct(dirAxis) * dirAxis - pInt;
				if (!val9.IsAlmostEqualTo(XYZ.Zero))
				{
					XYZ val10 = dirAxis.CrossProduct(val9).Normalize();
					XYZ val11 = -val10;
					XYZ val12 = val7.IsAlmostEqualTo(XYZ.Zero) ? val6 : val7;
					zero = (val10.DotProduct(val12) > val11.DotProduct(val12)) ? val10 : val11;
				}
				else zero = val7;
			}
			else zero = val7;
		}

		if (zero.IsAlmostEqualTo(XYZ.Zero))
		{
			TaskDialog.Show("L\u1ed7i", "T\u00e2m \u1ed1ng qu\u00e1 s\u00e1t tr\u1ee5c xoay, kh\u00f4ng th\u1ec3 t\u00ednh to\u00e1n g\u00f3c.");
			return 0.0;
		}

		zero = zero.Normalize();
		XYZ val13 = dirBranch - dirBranch.DotProduct(dirAxis) * dirAxis;
		if (val13.IsAlmostEqualTo(XYZ.Zero))
		{
			TaskDialog.Show("L\u1ed7i", "\u1ed0ng nh\u00e1nh song song v\u1edbi tr\u1ee5c xoay.");
			return 0.0;
		}
		val13 = val13.Normalize();
		double angle = val13.AngleTo(zero);
		if (val13.CrossProduct(zero).DotProduct(dirAxis) < 0.0)
		{
			angle = -angle;
		}
		return angle;
	}
}
