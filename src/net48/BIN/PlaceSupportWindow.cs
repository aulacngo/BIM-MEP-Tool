using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Autodesk.Revit.DB;

namespace BIN;

public class PlaceSupportWindow : Window, IComponentConnector
{
	private List<FamilySymbol> _allSymbols;

	internal TabControl tabMEPType;

	internal TextBox txtStartPipe;

	internal TextBox txtOffsetPipe;

	internal ComboBox cbSupportTypePipe;

	internal TextBox txtStartDuct;

	internal TextBox txtOffsetDuct;

	internal ComboBox cbSupportTypeDuct;

	internal TextBox txtStartRoundDuct;

	internal TextBox txtOffsetRoundDuct;

	internal ComboBox cbSupportTypeRoundDuct;

	internal TextBox txtStartCableTray;

	internal TextBox txtOffsetCableTray;

	internal ComboBox cbSupportTypeCableTray;

	internal TextBox txtStartConduit;

	internal TextBox txtOffsetConduit;

	internal ComboBox cbSupportTypeConduit;

	private bool _contentLoaded;

	public double DistanceStart { get; set; }

	public double DistanceOffset { get; set; }

	public FamilySymbol SelectedSymbol { get; set; }

	public int MEPType { get; set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public PlaceSupportWindow(List<FamilySymbol> symbols)
	{
		InitializeComponent();
		_allSymbols = symbols;
		LoadSymbolsForComboBox(cbSupportTypePipe, "Pipe Support");
		LoadSymbolsForComboBox(cbSupportTypeDuct, "Duct Support");
		LoadSymbolsForComboBox(cbSupportTypeRoundDuct, "Round Duct Support");
		LoadSymbolsForComboBox(cbSupportTypeCableTray, "Cable Tray Support");
		LoadSymbolsForComboBox(cbSupportTypeConduit, "Conduit Support");
	}

	private void LoadSymbolsForComboBox(ComboBox comboBox, string filterName)
	{
		List<FamilySymbol> filteredSymbols = _allSymbols.Where((FamilySymbol x) => ((ElementType)x).FamilyName.Contains(filterName)).ToList();
		if (filteredSymbols.Count == 0 && filterName == "Conduit Support")
		{
			filteredSymbols = _allSymbols.Where((FamilySymbol x) => ((ElementType)x).FamilyName.Contains("Conduit") || ((ElementType)x).FamilyName.Contains("Pipe Support")).ToList();
		}
		comboBox.ItemsSource = filteredSymbols;
		if (filteredSymbols.Count > 0)
		{
			comboBox.SelectedItem = filteredSymbols[0];
		}
	}

	private void tabMEPType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
	}

	private void btnOK_Click(object sender, RoutedEventArgs e)
	{
		MEPType = tabMEPType.SelectedIndex;
		TextBox txtStart;
		TextBox txtOffset;
		ComboBox cbSupportType;
		switch (MEPType)
		{
		default:
			return;
		case 0:
			txtStart = txtStartPipe;
			txtOffset = txtOffsetPipe;
			cbSupportType = cbSupportTypePipe;
			break;
		case 1:
			txtStart = txtStartDuct;
			txtOffset = txtOffsetDuct;
			cbSupportType = cbSupportTypeDuct;
			break;
		case 2:
			txtStart = txtStartRoundDuct;
			txtOffset = txtOffsetRoundDuct;
			cbSupportType = cbSupportTypeRoundDuct;
			break;
		case 3:
			txtStart = txtStartCableTray;
			txtOffset = txtOffsetCableTray;
			cbSupportType = cbSupportTypeCableTray;
			break;
		case 4:
			txtStart = txtStartConduit;
			txtOffset = txtOffsetConduit;
			cbSupportType = cbSupportTypeConduit;
			break;
		}
		if (!double.TryParse(txtStart.Text, out var s) || !double.TryParse(txtOffset.Text, out var o))
		{
			MessageBox.Show("Vui lòng nhập giá trị khoảng cách là số (mm)!", "Thông báo");
			return;
		}
		DistanceStart = s / 304.8;
		DistanceOffset = o / 304.8;
		object selectedItem = cbSupportType.SelectedItem;
		SelectedSymbol = (FamilySymbol)((selectedItem is FamilySymbol) ? selectedItem : null);
		if (SelectedSymbol == null)
		{
			MessageBox.Show("Vui lòng chọn một loại Support!");
			return;
		}
		base.DialogResult = true;
		Close();
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/28.%20place%20support/placesupportwindow.xaml", UriKind.Relative);
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
			tabMEPType = (TabControl)target;
			tabMEPType.SelectionChanged += tabMEPType_SelectionChanged;
			break;
		case 2:
			txtStartPipe = (TextBox)target;
			break;
		case 3:
			txtOffsetPipe = (TextBox)target;
			break;
		case 4:
			cbSupportTypePipe = (ComboBox)target;
			break;
		case 5:
			txtStartDuct = (TextBox)target;
			break;
		case 6:
			txtOffsetDuct = (TextBox)target;
			break;
		case 7:
			cbSupportTypeDuct = (ComboBox)target;
			break;
		case 8:
			txtStartRoundDuct = (TextBox)target;
			break;
		case 9:
			txtOffsetRoundDuct = (TextBox)target;
			break;
		case 10:
			cbSupportTypeRoundDuct = (ComboBox)target;
			break;
		case 11:
			txtStartCableTray = (TextBox)target;
			break;
		case 12:
			txtOffsetCableTray = (TextBox)target;
			break;
		case 13:
			cbSupportTypeCableTray = (ComboBox)target;
			break;
		case 14:
			txtStartConduit = (TextBox)target;
			break;
		case 15:
			txtOffsetConduit = (TextBox)target;
			break;
		case 16:
			cbSupportTypeConduit = (ComboBox)target;
			break;
		case 17:
			((Button)target).Click += btnOK_Click;
			break;
		case 18:
			((Button)target).Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
