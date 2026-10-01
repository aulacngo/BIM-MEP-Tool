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

namespace BIN;

public class SprinklerSelectionWindow : Window, IComponentConnector
{
	internal ListBox lbFamilySymbols;

	private bool _contentLoaded;

	public FamilySymbol SelectedSymbol { get; private set; }

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public SprinklerSelectionWindow(List<FamilySymbol> symbols)
	{
		InitializeComponent();
		List<object> displayList = new List<object>();
		foreach (FamilySymbol s in symbols)
		{
			displayList.Add(new
			{
				FullName = ((ElementType)s).FamilyName + " : " + ((Element)s).Name,
				Symbol = s
			});
		}
		lbFamilySymbols.ItemsSource = displayList;
		if (lbFamilySymbols.Items.Count > 0)
		{
			lbFamilySymbols.SelectedIndex = 0;
		}
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (lbFamilySymbols.SelectedItem != null)
		{
			dynamic selected = lbFamilySymbols.SelectedItem;
			SelectedSymbol = (FamilySymbol)selected.Symbol;
			base.DialogResult = true;
		}
		Close();
	}

	private void BtnCancel_Click(object sender, RoutedEventArgs e)
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
			Uri resourceLocater = new Uri("/BIN;component/08.%20sprinkler%20flipper%20up%20to%20down/sprinklerselectionwindow.xaml", UriKind.Relative);
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
			lbFamilySymbols = (ListBox)target;
			break;
		case 2:
			((Button)target).Click += BtnOk_Click;
			break;
		case 3:
			((Button)target).Click += BtnCancel_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
