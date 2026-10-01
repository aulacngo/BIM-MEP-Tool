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

public class MultiPipeSupportWindow : Window, IComponentConnector
{
	private const double MM_TO_FEET = 304.8;

	internal TextBox txtStart;

	internal TextBox txtOffset;

	internal ComboBox cbSupportType;

	private bool _contentLoaded;

	public double DistanceStartFeet { get; private set; }

	public double DistanceOffsetFeet { get; private set; }

	public FamilySymbol SelectedSymbol { get; private set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public MultiPipeSupportWindow(List<FamilySymbol> symbols)
	{
		InitializeComponent();
		Title = "BIM TOOL – Multi Pipe Support";
		LoadSymbols(symbols);
	}

	private void LoadSymbols(List<FamilySymbol> symbols)
	{
		if (symbols != null)
		{
			List<FamilySymbol> filteredSymbols = (from x in symbols
				where ((ElementType)x).FamilyName.IndexOf("Multi Pipe Support", StringComparison.OrdinalIgnoreCase) >= 0
				orderby ((ElementType)x).FamilyName, ((Element)x).Name
				select x).ToList();
			cbSupportType.ItemsSource = filteredSymbols;
			if (filteredSymbols.Any())
			{
				cbSupportType.SelectedIndex = 0;
			}
			else
			{
				MessageBox.Show("Không tìm thấy Family Support phù hợp trong Project!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}
	}

	private void btnOK_Click(object sender, RoutedEventArgs e)
	{
		if (!double.TryParse(txtStart.Text, out var s) || s < 0.0)
		{
			ShowError("Khoảng cách bắt đầu phải là số dương!");
			return;
		}
		if (!double.TryParse(txtOffset.Text, out var o) || o <= 0.0)
		{
			ShowError("Khoảng cách Support phải lớn hơn 0!");
			return;
		}
		object selectedItem = cbSupportType.SelectedItem;
		FamilySymbol selected = (FamilySymbol)((selectedItem is FamilySymbol) ? selectedItem : null);
		if (selected == null)
		{
			ShowError("Vui lòng chọn một loại Support!");
			return;
		}
		DistanceStartFeet = s / 304.8;
		DistanceOffsetFeet = o / 304.8;
		SelectedSymbol = selected;
		base.DialogResult = true;
		Close();
	}

	private void ShowError(string message)
	{
		MessageBox.Show(message, "Thông báo lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
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
			Uri resourceLocater = new Uri("/BIN;component/16.%20place%20multi%20pipe%20support/multipipesupportwindow.xaml", UriKind.Relative);
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
			txtStart = (TextBox)target;
			break;
		case 2:
			txtOffset = (TextBox)target;
			break;
		case 3:
			cbSupportType = (ComboBox)target;
			break;
		case 4:
			((Button)target).Click += btnOK_Click;
			break;
		case 5:
			((Button)target).Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
