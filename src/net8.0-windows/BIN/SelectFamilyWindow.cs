using System;
using System.CodeDom.Compiler;
using System.Collections;
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

public class SelectFamilyWindow : Window, IComponentConnector
{
	internal ListBox lbSymbols;

	private bool _contentLoaded;

	public FamilySymbol SelectedSymbol { get; set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public SelectFamilyWindow(Document doc)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		InitializeComponent();
		List<FamilySymbol> symbols = ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).OfCategory((BuiltInCategory)(-2008049))).Cast<FamilySymbol>().Where(delegate(FamilySymbol fs)
		{
			Parameter val = ((Element)fs.Family).get_Parameter((BuiltInParameter)(-1114206));
			if (val != null)
			{
				int num = val.AsInteger();
				return num == 13;
			}
			return false;
		}).ToList();
		List<FamilySymbol> sortedSymbols = (from x in symbols
			orderby (!((Element)x).Name.ToUpper().Contains("KEP REN")) ? 1 : 0, ((Element)x).Name
			select x).ToList();
		lbSymbols.ItemsSource = sortedSymbols;
		if (sortedSymbols.Count > 0)
		{
			lbSymbols.SelectedIndex = 0;
		}
	}

	private void btnOk_Click(object sender, RoutedEventArgs e)
	{
		object selectedItem = lbSymbols.SelectedItem;
		SelectedSymbol = (FamilySymbol)((selectedItem is FamilySymbol) ? selectedItem : null);
		if (SelectedSymbol == null)
		{
			MessageBox.Show("Please select a Nipple Family!");
			return;
		}
		base.DialogResult = true;
		Close();
	}

	private void btnCancel_Click(object sender, RoutedEventArgs e)
	{
		base.DialogResult = false;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/05.%20replace%20pipe%20with%20nipple/selectfamilywindow.xaml", UriKind.Relative);
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
			lbSymbols = (ListBox)target;
			break;
		case 2:
			((Button)target).Click += btnOk_Click;
			break;
		case 3:
			((Button)target).Click += btnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
