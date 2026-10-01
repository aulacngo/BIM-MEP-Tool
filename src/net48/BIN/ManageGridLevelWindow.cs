using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace BIN;

public class ManageGridLevelWindow : Window, IComponentConnector
{
	internal CheckBox chkVisNorth;

	internal CheckBox chkVisSouth;

	internal CheckBox chkVisWest;

	internal CheckBox chkVisEast;

	internal CheckBox chkFitNorth;

	internal CheckBox chkFitSouth;

	internal CheckBox chkFitWest;

	internal CheckBox chkFitEast;

	internal CheckBox chkLevelFitLeft;

	internal CheckBox chkLevelFitRight;

	private bool _contentLoaded;

	public Action<bool, bool, bool, bool> OnGridVisible { get; set; }

	public Action<bool, bool, bool, bool> OnGridFit { get; set; }

	public Action<bool, bool> OnLevelFit { get; set; }

	public ManageGridLevelWindow()
	{
		InitializeComponent();
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	private void btnGridVisible_Click(object sender, RoutedEventArgs e)
	{
		OnGridVisible?.Invoke(chkVisNorth.IsChecked == true, chkVisSouth.IsChecked == true, chkVisEast.IsChecked == true, chkVisWest.IsChecked == true);
	}

	private void btnGridFit_Click(object sender, RoutedEventArgs e)
	{
		OnGridFit?.Invoke(chkFitNorth.IsChecked == true, chkFitSouth.IsChecked == true, chkFitEast.IsChecked == true, chkFitWest.IsChecked == true);
	}

	private void btnLevelFit_Click(object sender, RoutedEventArgs e)
	{
		OnLevelFit?.Invoke(chkLevelFitLeft.IsChecked == true, chkLevelFitRight.IsChecked == true);
	}

	private void btnClose_Click(object sender, RoutedEventArgs e)
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
			Uri resourceLocater = new Uri("/BIN;component/34.%20manage%20grid%20level/managegridlevelwindow.xaml", UriKind.Relative);
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
			chkVisNorth = (CheckBox)target;
			break;
		case 2:
			chkVisSouth = (CheckBox)target;
			break;
		case 3:
			chkVisWest = (CheckBox)target;
			break;
		case 4:
			chkVisEast = (CheckBox)target;
			break;
		case 5:
			((Button)target).Click += btnGridVisible_Click;
			break;
		case 6:
			chkFitNorth = (CheckBox)target;
			break;
		case 7:
			chkFitSouth = (CheckBox)target;
			break;
		case 8:
			chkFitWest = (CheckBox)target;
			break;
		case 9:
			chkFitEast = (CheckBox)target;
			break;
		case 10:
			((Button)target).Click += btnGridFit_Click;
			break;
		case 11:
			chkLevelFitLeft = (CheckBox)target;
			break;
		case 12:
			chkLevelFitRight = (CheckBox)target;
			break;
		case 13:
			((Button)target).Click += btnLevelFit_Click;
			break;
		case 14:
			((Button)target).Click += btnClose_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
