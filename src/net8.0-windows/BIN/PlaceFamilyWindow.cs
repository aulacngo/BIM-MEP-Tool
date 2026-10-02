using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Autodesk.Revit.DB;
using WpfControl = System.Windows.Controls.Control;
using WpfGrid = System.Windows.Controls.Grid;

namespace BIN;

public class PlaceFamilyWindow : Window, IComponentConnector
{
	private static string LastSelectedCadUnit = "Auto Detect (Tự động)";

	private static bool LastUseTrueCenter = true;

	private static bool LastCalibrateBasepoint;

	private Document doc;

	private FamilyInstance instance;

	public string blockName;

	public string familyName;

	public string typeName;

	public string levelName;

	public double elevation;

	public bool IsUseTrueCenter { get; set; } = true;

	public bool IsCalibrateBasepoint { get; set; }

	public string SelectedCadUnit { get; set; } = "Auto Detect (Tự động)";

	internal TextBox tbFileCad;

	internal ComboBox cbbCadBlock;

	internal ComboBox cbbCadUnit;

	internal ComboBox cbbFamily;

	internal ComboBox cbbTypeName;

	internal ComboBox cbbLevel;

	internal TextBox tbDistance;

	internal CheckBox cbUseTrueCenter;

	internal CheckBox cbCalibrateBasepoint;

	internal Button btOk;

	internal Button btCancel;

	private bool _contentLoaded;

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public PlaceFamilyWindow(Document doc, FamilyInstance instance, string fileCadName, List<string> listBlock, List<string> listFamily)
	{
		InitializeComponent();
		this.doc = doc;
		this.instance = instance;
		tbFileCad.Text = fileCadName;
		cbbCadBlock.ItemsSource = listBlock;
		if (listBlock != null && listBlock.Count > 0)
		{
			cbbCadBlock.SelectedIndex = 0;
		}
		cbbFamily.ItemsSource = listFamily;
		if (listFamily != null && listFamily.Count > 0)
		{
			cbbFamily.SelectedIndex = 0;
		}
		List<string> listLevels = PlaceFamilyUtils.ListLevel(doc);
		cbbLevel.ItemsSource = listLevels;
		Element element = doc.GetElement(((Element)instance).LevelId);
		Level refLevel = (Level)(object)((element is Level) ? element : null);
		if (refLevel != null)
		{
			cbbLevel.SelectedItem = ((Element)refLevel).Name;
		}
		else
		{
			Level activeViewLevel = doc.ActiveView?.GenLevel;
			if (activeViewLevel != null)
			{
				cbbLevel.SelectedItem = ((Element)activeViewLevel).Name;
			}
			else
			{
				// Do not silently use the lowest level (often a basement) when the
				// picked sample is host-based and has no LevelId.
				cbbLevel.SelectedIndex = -1;
			}
		}
		Parameter p = ((Element)instance).get_Parameter((BuiltInParameter)(-1001360));
		if (p != null)
		{
			tbDistance.Text = (p.AsDouble() * 304.8).ToString("0");
		}
		else
		{
			tbDistance.Text = "0";
		}
	}

	private void btOk_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(cbbCadBlock.Text) || cbbFamily.SelectedItem == null || cbbTypeName.SelectedItem == null || cbbLevel.SelectedItem == null)
		{
			MessageBox.Show("Vui lòng chọn đầy đủ thông tin", "Thông báo");
			return;
		}
		blockName = cbbCadBlock.Text.Trim();
		SelectedCadUnit = cbbCadUnit.SelectedItem != null ? cbbCadUnit.SelectedItem.ToString() : "Auto Detect (Tự động)";
		familyName = cbbFamily.SelectedValue.ToString();
		typeName = cbbTypeName.SelectedValue.ToString();
		levelName = cbbLevel.SelectedValue.ToString();
		if (double.TryParse(tbDistance.Text, out var value))
		{
			elevation = value;
			IsUseTrueCenter = cbUseTrueCenter.IsChecked == true;
			IsCalibrateBasepoint = cbCalibrateBasepoint.IsChecked == true;
			LastSelectedCadUnit = SelectedCadUnit;
			LastUseTrueCenter = IsUseTrueCenter;
			LastCalibrateBasepoint = IsCalibrateBasepoint;
			base.DialogResult = true;
		}
		else
		{
			MessageBox.Show("Vui lòng nhập số vào ô Elevation", "Lỗi nhập liệu");
			tbDistance.Text = "0";
		}
	}

	private void btCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	private void cbbFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (cbbFamily.SelectedValue != null)
		{
			string fName = cbbFamily.SelectedValue.ToString();
			List<string> listtype = PlaceFamilyUtils.GetListTypeByFamilyName(doc, instance, fName);
			cbbTypeName.ItemsSource = listtype;
			if (listtype != null && listtype.Count > 0)
			{
				cbbTypeName.SelectedIndex = 0;
			}
		}
	}

	private void cbbLevel_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
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
		Title = "Place Family";
		Width = 460.0;
		SizeToContent = SizeToContent.Height;
		ResizeMode = ResizeMode.NoResize;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;

		WpfGrid grid = new WpfGrid
		{
			Margin = new Thickness(16.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120.0) });
		grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

		void AddRow(string label, WpfControl control, int row)
		{
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			TextBlock text = new TextBlock
			{
				Text = label,
				Margin = new Thickness(0.0, 5.0, 10.0, 9.0),
				VerticalAlignment = VerticalAlignment.Center
			};
			control.Margin = new Thickness(0.0, 3.0, 0.0, 7.0);
			control.MinHeight = 25.0;
			WpfGrid.SetRow(text, row);
			WpfGrid.SetColumn(text, 0);
			WpfGrid.SetRow(control, row);
			WpfGrid.SetColumn(control, 1);
			grid.Children.Add(text);
			grid.Children.Add(control);
		}

		tbFileCad = new TextBox { IsReadOnly = true };
		cbbCadBlock = new ComboBox { IsEditable = true };
		cbbCadUnit = new ComboBox();
		cbbCadUnit.Items.Add("Auto Detect (Tự động)");
		cbbCadUnit.Items.Add("Millimeters (mm)");
		cbbCadUnit.Items.Add("Meters (m)");
		cbbCadUnit.Items.Add("Centimeters (cm)");
		cbbCadUnit.Items.Add("Inches (in)");
		cbbCadUnit.Items.Add("Feet (ft)");
		if (!string.IsNullOrEmpty(LastSelectedCadUnit) && cbbCadUnit.Items.Contains(LastSelectedCadUnit))
		{
			cbbCadUnit.SelectedItem = LastSelectedCadUnit;
		}
		else
		{
			cbbCadUnit.SelectedIndex = 0;
		}
		cbbFamily = new ComboBox();
		cbbTypeName = new ComboBox();
		cbbLevel = new ComboBox();
		tbDistance = new TextBox();
		cbUseTrueCenter = new CheckBox
		{
			Content = "Tự động căn theo tâm hình học (True Center: Sprinkler tròn & Miệng gió vuông)",
			IsChecked = LastUseTrueCenter,
			VerticalAlignment = VerticalAlignment.Center
		};
		cbCalibrateBasepoint = new CheckBox
		{
			Content = "Căn chỉnh mốc theo giao điểm trục / điểm mốc (Grid Calibration)",
			IsChecked = LastCalibrateBasepoint,
			VerticalAlignment = VerticalAlignment.Center
		};
		cbbFamily.SelectionChanged += cbbFamily_SelectionChanged;
		cbbLevel.SelectionChanged += cbbLevel_SelectionChanged;

		AddRow("CAD Link", tbFileCad, 0);
		AddRow("CAD Block", cbbCadBlock, 1);
		AddRow("CAD Unit", cbbCadUnit, 2);
		AddRow("Family", cbbFamily, 3);
		AddRow("Type", cbbTypeName, 4);
		AddRow("Level", cbbLevel, 5);
		AddRow("Elevation (mm)", tbDistance, 6);

		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		cbUseTrueCenter.Margin = new Thickness(0.0, 3.0, 0.0, 7.0);
		WpfGrid.SetRow(cbUseTrueCenter, 7);
		WpfGrid.SetColumnSpan(cbUseTrueCenter, 2);
		grid.Children.Add(cbUseTrueCenter);

		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		cbCalibrateBasepoint.Margin = new Thickness(0.0, 3.0, 0.0, 7.0);
		WpfGrid.SetRow(cbCalibrateBasepoint, 8);
		WpfGrid.SetColumnSpan(cbCalibrateBasepoint, 2);
		grid.Children.Add(cbCalibrateBasepoint);

		grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		StackPanel buttons = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		btOk = new Button { Content = "OK", Width = 90.0, Height = 28.0, IsDefault = true };
		btCancel = new Button { Content = "Cancel", Width = 90.0, Height = 28.0, Margin = new Thickness(8.0, 0.0, 0.0, 0.0), IsCancel = true };
		btOk.Click += btOk_Click;
		btCancel.Click += btCancel_Click;
		buttons.Children.Add(btOk);
		buttons.Children.Add(btCancel);
		WpfGrid.SetRow(buttons, 9);
		WpfGrid.SetColumnSpan(buttons, 2);
		grid.Children.Add(buttons);
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
			tbFileCad = (TextBox)target;
			break;
		case 2:
			cbbCadBlock = (ComboBox)target;
			break;
		case 3:
			cbbFamily = (ComboBox)target;
			cbbFamily.SelectionChanged += cbbFamily_SelectionChanged;
			break;
		case 4:
			cbbTypeName = (ComboBox)target;
			break;
		case 5:
			cbbLevel = (ComboBox)target;
			cbbLevel.SelectionChanged += cbbLevel_SelectionChanged;
			break;
		case 6:
			tbDistance = (TextBox)target;
			break;
		case 7:
			btOk = (Button)target;
			btOk.Click += btOk_Click;
			break;
		case 8:
			btCancel = (Button)target;
			btCancel.Click += btCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
