using ComboBox = System.Windows.Controls.ComboBox;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public class TransferDocumentWindow : Window, IComponentConnector
{
	private UIApplication _uiApp;

	private Document _sourceDoc;

	private List<Document> _openDocuments;

	private ObservableCollection<TransferDocumentItem> _elementItems;

	private List<View> _legendViews;

	private List<View> _viewTemplates;

	private List<View> _draftingViews;

	private List<ViewSchedule> _schedules;

	private List<ViewSheet> _sheets;

	private List<ViewSheetSet> _sheetSets;

	private List<ParameterFilterElement> _filters;

	private List<View> _views;

	internal TextBlock txtSourceFile;

	internal ComboBox cboDestDocument;

	internal RadioButton rbLegend;

	internal RadioButton rbViewTemplate;

	internal RadioButton rbDraftingView;

	internal RadioButton rbSchedule;

	internal RadioButton rbSheet;

	internal RadioButton rbFilter;

	internal RadioButton rbView;

	internal StackPanel pnlSheetSetFilter;

	internal ComboBox cboSheetSet;

	internal TextBlock txtElementListHeader;

	internal ListBox lstElements;

	internal TextBlock txtSelectedCount;

	private bool _contentLoaded;

	public Document SelectedDestinationDocument
	{
		get
		{
			object selectedItem = cboDestDocument.SelectedItem;
			return (Document)((selectedItem is Document) ? selectedItem : null);
		}
	}

	public TransferDocumentType SelectedElementType
	{
		get
		{
			if (rbLegend.IsChecked == true)
			{
				return TransferDocumentType.Legend;
			}
			if (rbViewTemplate.IsChecked == true)
			{
				return TransferDocumentType.ViewTemplate;
			}
			if (rbDraftingView.IsChecked == true)
			{
				return TransferDocumentType.DraftingView;
			}
			if (rbSchedule.IsChecked == true)
			{
				return TransferDocumentType.Schedule;
			}
			if (rbSheet.IsChecked == true)
			{
				return TransferDocumentType.Sheet;
			}
			if (rbFilter.IsChecked == true)
			{
				return TransferDocumentType.Filter;
			}
			if (rbView.IsChecked == true)
			{
				return TransferDocumentType.View;
			}
			return TransferDocumentType.Legend;
		}
	}

	public List<ElementId> SelectedElementIds => (from item in _elementItems
		where item.IsSelected
		select item.ElementId).ToList();

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public TransferDocumentWindow(UIApplication uiApp, Document sourceDoc, List<Document> openDocuments)
	{
		InitializeComponent();
		_uiApp = uiApp;
		_sourceDoc = sourceDoc;
		_openDocuments = openDocuments;
		string sourceFileName = Path.GetFileName(sourceDoc.PathName);
		if (string.IsNullOrEmpty(sourceFileName))
		{
			sourceFileName = sourceDoc.Title;
		}
		txtSourceFile.Text = sourceFileName;
		cboDestDocument.ItemsSource = openDocuments;
		if (openDocuments.Count > 0)
		{
			cboDestDocument.SelectedIndex = 0;
		}
		_elementItems = new ObservableCollection<TransferDocumentItem>();
		lstElements.ItemsSource = _elementItems;
		CacheAllElements();
		LoadElementsForType(TransferDocumentType.Legend);
	}

	private void CacheAllElements()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0242: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		_legendViews = (from View v in (IEnumerable)new FilteredElementCollector(_sourceDoc).OfClass(typeof(View))
			where (int)v.ViewType == 11 && !v.IsTemplate
			orderby ((Element)v).Name
			select v).ToList();
		_viewTemplates = (from View v in (IEnumerable)new FilteredElementCollector(_sourceDoc).OfClass(typeof(View))
			where v.IsTemplate
			orderby ((Element)v).Name
			select v).ToList();
		_draftingViews = (from View v in (IEnumerable)new FilteredElementCollector(_sourceDoc).OfClass(typeof(View))
			where (int)v.ViewType == 10 && !v.IsTemplate
			orderby ((Element)v).Name
			select v).ToList();
		_schedules = (from ViewSchedule s in (IEnumerable)new FilteredElementCollector(_sourceDoc).OfClass(typeof(ViewSchedule))
			where !((View)s).IsTemplate && !s.IsTitleblockRevisionSchedule
			orderby ((Element)s).Name
			select s).ToList();
		_sheets = (from ViewSheet s in (IEnumerable)new FilteredElementCollector(_sourceDoc).OfClass(typeof(ViewSheet))
			where !s.IsPlaceholder
			orderby s.SheetNumber
			select s).ToList();
		_filters = (from ParameterFilterElement f in (IEnumerable)new FilteredElementCollector(_sourceDoc).OfClass(typeof(ParameterFilterElement))
			orderby ((Element)f).Name
			select f).ToList();
		_sheetSets = (from ViewSheetSet ss in (IEnumerable)new FilteredElementCollector(_sourceDoc).OfClass(typeof(ViewSheetSet))
			orderby ((Element)ss).Name
			select ss).ToList();
		_views = (from View v in (IEnumerable)new FilteredElementCollector(_sourceDoc).OfClass(typeof(View))
			where !v.IsTemplate && (int)v.ViewType != 11 && (int)v.ViewType != 10 && (int)v.ViewType != 5 && (int)v.ViewType != 123 && (int)v.ViewType != 122 && (int)v.ViewType != 7 && (int)v.ViewType != 12 && (int)v.ViewType != 214
			orderby ((Element)v).Name
			select v).ToList();
	}

	private void LoadElementsForType(TransferDocumentType elementType)
	{
		foreach (TransferDocumentItem item in _elementItems)
		{
			item.PropertyChanged -= ElementItem_PropertyChanged;
		}
		_elementItems.Clear();
		string headerText = "Danh sách ";
		List<TransferDocumentItem> items = new List<TransferDocumentItem>();
		switch (elementType)
		{
		case TransferDocumentType.Legend:
			headerText += "Legend:";
			items = _legendViews.Select((View v) => new TransferDocumentItem
			{
				Name = ((Element)v).Name,
				ElementId = ((Element)v).Id
			}).ToList();
			break;
		case TransferDocumentType.ViewTemplate:
			headerText += "View Template:";
			items = _viewTemplates.Select((View v) => new TransferDocumentItem
			{
				Name = ((Element)v).Name,
				ElementId = ((Element)v).Id
			}).ToList();
			break;
		case TransferDocumentType.DraftingView:
			headerText += "Drafting View:";
			items = _draftingViews.Select((View v) => new TransferDocumentItem
			{
				Name = ((Element)v).Name,
				ElementId = ((Element)v).Id
			}).ToList();
			break;
		case TransferDocumentType.Schedule:
			headerText += "Schedule:";
			items = _schedules.Select((ViewSchedule s) => new TransferDocumentItem
			{
				Name = ((Element)s).Name,
				ElementId = ((Element)s).Id
			}).ToList();
			break;
		case TransferDocumentType.Sheet:
			headerText += "Sheet:";
			InitializeSheetSetFilter();
			items = _sheets.Select((ViewSheet s) => new TransferDocumentItem
			{
				Name = s.SheetNumber + " - " + ((Element)s).Name,
				ElementId = ((Element)s).Id
			}).ToList();
			break;
		case TransferDocumentType.Filter:
			headerText += "Filter:";
			items = _filters.Select((ParameterFilterElement f) => new TransferDocumentItem
			{
				Name = ((Element)f).Name,
				ElementId = ((Element)f).Id
			}).ToList();
			break;
		case TransferDocumentType.View:
			headerText += "View (Plan, 3D...):";
			items = _views.Select((View v) => new TransferDocumentItem
			{
				Name = $"{v.ViewType}: {((Element)v).Name}",
				ElementId = ((Element)v).Id
			}).ToList();
			break;
		}
		txtElementListHeader.Text = headerText;
		foreach (TransferDocumentItem item2 in items)
		{
			item2.PropertyChanged += ElementItem_PropertyChanged;
			_elementItems.Add(item2);
		}
		UpdateSelectedCount();
	}

	private void ElementType_Changed(object sender, RoutedEventArgs e)
	{
		if (_elementItems != null)
		{
			bool isSheetSelected = SelectedElementType == TransferDocumentType.Sheet;
			pnlSheetSetFilter.Visibility = ((!isSheetSelected) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible);
			LoadElementsForType(SelectedElementType);
		}
	}

	private void InitializeSheetSetFilter()
	{
		List<object> comboSource = new List<object>
		{
			new
			{
				Name = "--- All Sheets ---"
			}
		};
		if (_sheetSets != null && _sheetSets.Count > 0)
		{
			comboSource.AddRange(_sheetSets);
		}
		cboSheetSet.ItemsSource = comboSource;
		cboSheetSet.SelectedIndex = 0;
	}

	private void CboSheetSet_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_elementItems == null || cboSheetSet.SelectedItem == null)
		{
			return;
		}
		List<ViewSheet> sheetsToDisplay;
		if (cboSheetSet.SelectedIndex == 0)
		{
			sheetsToDisplay = _sheets;
		}
		else
		{
			object selectedItem = cboSheetSet.SelectedItem;
			ViewSheetSet selectedSet = (ViewSheetSet)((selectedItem is ViewSheetSet) ? selectedItem : null);
			if (selectedSet != null)
			{
				List<ElementId> setSheetIds = (from View v in (IEnumerable)selectedSet.Views
					select ((Element)v).Id).ToList();
				sheetsToDisplay = _sheets.Where((ViewSheet s) => setSheetIds.Contains(((Element)s).Id)).ToList();
			}
			else
			{
				sheetsToDisplay = _sheets;
			}
		}
		foreach (TransferDocumentItem item in _elementItems)
		{
			item.PropertyChanged -= ElementItem_PropertyChanged;
		}
		_elementItems.Clear();
		foreach (ViewSheet sheet in sheetsToDisplay)
		{
			TransferDocumentItem item2 = new TransferDocumentItem
			{
				Name = sheet.SheetNumber + " - " + ((Element)sheet).Name,
				ElementId = ((Element)sheet).Id
			};
			item2.PropertyChanged += ElementItem_PropertyChanged;
			_elementItems.Add(item2);
		}
		UpdateSelectedCount();
	}

	private void ElementItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "IsSelected")
		{
			UpdateSelectedCount();
		}
	}

	private void UpdateSelectedCount()
	{
		int selectedCount = _elementItems.Count((TransferDocumentItem item) => item.IsSelected);
		txtSelectedCount.Text = $"({selectedCount} selected)";
	}

	private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
	{
		foreach (TransferDocumentItem item in _elementItems)
		{
			item.IsSelected = true;
		}
	}

	private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
	{
		foreach (TransferDocumentItem item in _elementItems)
		{
			item.IsSelected = false;
		}
	}

	private void BtnOk_Click(object sender, RoutedEventArgs e)
	{
		if (cboDestDocument.SelectedItem == null)
		{
			MessageBox.Show("Vui lòng chọn file đích!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (_elementItems.Count((TransferDocumentItem item) => item.IsSelected) == 0)
		{
			MessageBox.Show("Vui lòng chọn ít nhất một element để copy!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		base.DialogResult = true;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/21.%20copy%202d%20element%20to%20other%20document/copy2delementwindow.xaml", UriKind.Relative);
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
			txtSourceFile = (TextBlock)target;
			break;
		case 2:
			cboDestDocument = (ComboBox)target;
			break;
		case 3:
			rbLegend = (RadioButton)target;
			rbLegend.Checked += ElementType_Changed;
			break;
		case 4:
			rbViewTemplate = (RadioButton)target;
			rbViewTemplate.Checked += ElementType_Changed;
			break;
		case 5:
			rbDraftingView = (RadioButton)target;
			rbDraftingView.Checked += ElementType_Changed;
			break;
		case 6:
			rbSchedule = (RadioButton)target;
			rbSchedule.Checked += ElementType_Changed;
			break;
		case 7:
			rbSheet = (RadioButton)target;
			rbSheet.Checked += ElementType_Changed;
			break;
		case 8:
			rbFilter = (RadioButton)target;
			rbFilter.Checked += ElementType_Changed;
			break;
		case 9:
			rbView = (RadioButton)target;
			rbView.Checked += ElementType_Changed;
			break;
		case 10:
			pnlSheetSetFilter = (StackPanel)target;
			break;
		case 11:
			cboSheetSet = (ComboBox)target;
			cboSheetSet.SelectionChanged += CboSheetSet_SelectionChanged;
			break;
		case 12:
			txtElementListHeader = (TextBlock)target;
			break;
		case 13:
			lstElements = (ListBox)target;
			break;
		case 14:
			((Button)target).Click += BtnSelectAll_Click;
			break;
		case 15:
			((Button)target).Click += BtnSelectNone_Click;
			break;
		case 16:
			txtSelectedCount = (TextBlock)target;
			break;
		case 17:
			((Button)target).Click += BtnOk_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
