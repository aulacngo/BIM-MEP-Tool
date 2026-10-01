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

public class Delete2DElementWindow : Window, IComponentConnector
{
	private readonly Document _doc;

	private List<SelectableElement> _allElements = new List<SelectableElement>();

	private List<SelectableElement> _filteredElements = new List<SelectableElement>();

	internal ComboBox cmbElementType;

	internal StackPanel pnlSecondaryFilter;

	internal TextBlock txtSecondaryFilterLabel;

	internal ComboBox cmbSecondaryFilter;

	internal TextBlock txtListHeader;

	internal ListBox lstElements;

	internal TextBlock txtCount;

	private bool _contentLoaded;

	public List<ElementId> SelectedElementIds => (from e in _filteredElements
		where e.IsSelected
		select e.Id).ToList();

	public string SelectedElementTypeName { get; private set; } = "Elements";

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public Delete2DElementWindow(Document doc)
	{
		InitializeComponent();
		_doc = doc;
		cmbElementType.SelectedIndex = 0;
	}

	private void CmbElementType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (cmbElementType.SelectedItem != null)
		{
			string tag = ((ComboBoxItem)cmbElementType.SelectedItem).Tag?.ToString();
			LoadElements(tag);
		}
	}

	private void LoadElements(string elementType)
	{
		_allElements.Clear();
		pnlSecondaryFilter.Visibility = System.Windows.Visibility.Collapsed;
		switch (elementType)
		{
		case "Filter":
			LoadFilters();
			SelectedElementTypeName = "Filter";
			txtListHeader.Text = "Danh sách Filters:";
			break;
		case "ViewTemplate":
			LoadViewTemplates();
			SelectedElementTypeName = "View Template";
			txtListHeader.Text = "Danh sách View Templates:";
			break;
		case "Schedule":
			LoadSchedules();
			SelectedElementTypeName = "Schedule";
			txtListHeader.Text = "Danh sách Schedules:";
			break;
		case "Sheet":
			LoadSheets();
			SelectedElementTypeName = "Sheet";
			txtListHeader.Text = "Danh sách Sheets:";
			SetupSheetSetFilter();
			break;
		case "View":
			LoadViews();
			SelectedElementTypeName = "View";
			txtListHeader.Text = "Danh sách Views:";
			SetupViewTypeFilter();
			break;
		}
		_filteredElements = new List<SelectableElement>(_allElements);
		lstElements.ItemsSource = _filteredElements;
		UpdateCount();
	}

	private void LoadFilters()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<ParameterFilterElement> filters = (from ParameterFilterElement f in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ParameterFilterElement))
			orderby ((Element)f).Name
			select f).ToList();
		foreach (ParameterFilterElement filter in filters)
		{
			_allElements.Add(new SelectableElement
			{
				Id = ((Element)filter).Id,
				Name = ((Element)filter).Name,
				DisplayName = ((Element)filter).Name
			});
		}
	}

	private void LoadViewTemplates()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<View> templates = (from View v in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(View))
			where v.IsTemplate
			orderby ((Element)v).Name
			select v).ToList();
		foreach (View template in templates)
		{
			_allElements.Add(new SelectableElement
			{
				Id = ((Element)template).Id,
				Name = ((Element)template).Name,
				DisplayName = ((Element)template).Name
			});
		}
	}

	private void LoadSchedules()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<ViewSchedule> schedules = (from ViewSchedule s in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ViewSchedule))
			where !s.IsTitleblockRevisionSchedule && !((View)s).IsTemplate
			orderby ((Element)s).Name
			select s).ToList();
		foreach (ViewSchedule schedule in schedules)
		{
			_allElements.Add(new SelectableElement
			{
				Id = ((Element)schedule).Id,
				Name = ((Element)schedule).Name,
				DisplayName = ((Element)schedule).Name
			});
		}
	}

	private void LoadSheets()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<ViewSheet> sheets = (from ViewSheet s in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet))
			where !s.IsPlaceholder
			orderby s.SheetNumber
			select s).ToList();
		foreach (ViewSheet sheet in sheets)
		{
			_allElements.Add(new SelectableElement
			{
				Id = ((Element)sheet).Id,
				Name = ((Element)sheet).Name,
				DisplayName = sheet.SheetNumber + " - " + ((Element)sheet).Name,
				SheetNumber = sheet.SheetNumber
			});
		}
	}

	private void LoadViews()
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		View activeView = _doc.ActiveView;
		List<View> views = (from View v in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(View))
			where !v.IsTemplate && (int)v.ViewType != 5 && (int)v.ViewType != 6 && (int)v.ViewType != 7 && (int)v.ViewType != 12 && (int)v.ViewType != 0 && (int)v.ViewType != 214 && ((Element)v).Id != ((Element)activeView).Id
			select v).OrderBy(delegate(View v)
		{
			ViewType viewType = v.ViewType;
			return viewType.ToString();
		}).ThenBy((View v) => ((Element)v).Name).ToList();
		foreach (View view in views)
		{
			List<SelectableElement> allElements = _allElements;
			SelectableElement obj = new SelectableElement
			{
				Id = ((Element)view).Id,
				Name = ((Element)view).Name,
				DisplayName = $"[{view.ViewType}] {((Element)view).Name}"
			};
			ViewType viewType2 = view.ViewType;
			obj.ViewTypeName = viewType2.ToString();
			allElements.Add(obj);
		}
	}

	private void SetupSheetSetFilter()
	{
		pnlSecondaryFilter.Visibility = System.Windows.Visibility.Visible;
		txtSecondaryFilterLabel.Text = "Lọc theo Sheet Set:";
		List<string> sheetSets = new List<string> { "Tất cả" };
		IOrderedEnumerable<string> paramSets = from s in (from s in (from s in _allElements.Select(delegate(SelectableElement e)
					{
						Element element = _doc.GetElement(e.Id);
						return (ViewSheet)(object)((element is ViewSheet) ? element : null);
					})
					where s != null
					select s).Select(delegate(ViewSheet s)
				{
					Parameter obj = ((Element)s).LookupParameter("Sheet Set");
					return (obj != null) ? obj.AsString() : null;
				})
				where !string.IsNullOrEmpty(s)
				select s).Distinct()
			orderby s
			select s;
		sheetSets.AddRange(paramSets);
		cmbSecondaryFilter.ItemsSource = sheetSets;
		cmbSecondaryFilter.SelectedIndex = 0;
	}

	private void SetupViewTypeFilter()
	{
		pnlSecondaryFilter.Visibility = System.Windows.Visibility.Visible;
		txtSecondaryFilterLabel.Text = "Lọc theo loại View:";
		List<string> viewTypes = new List<string> { "Tất cả" };
		viewTypes.AddRange(from t in (from e in _allElements
				select e.ViewTypeName into t
				where !string.IsNullOrEmpty(t)
				select t).Distinct()
			orderby t
			select t);
		cmbSecondaryFilter.ItemsSource = viewTypes;
		cmbSecondaryFilter.SelectedIndex = 0;
	}

	private void CmbSecondaryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (cmbSecondaryFilter.SelectedItem != null)
		{
			string filter = cmbSecondaryFilter.SelectedItem.ToString();
			ApplySecondaryFilter(filter);
		}
	}

	private void ApplySecondaryFilter(string filter)
	{
		if (filter == "Tất cả")
		{
			_filteredElements = new List<SelectableElement>(_allElements);
		}
		else
		{
			string tag = ((ComboBoxItem)cmbElementType.SelectedItem)?.Tag?.ToString();
			if (tag == "Sheet")
			{
				_filteredElements = _allElements.Where(delegate(SelectableElement elem)
				{
					Element element = _doc.GetElement(elem.Id);
					ViewSheet val = (ViewSheet)(object)((element is ViewSheet) ? element : null);
					object obj;
					if (val == null)
					{
						obj = null;
					}
					else
					{
						Parameter obj2 = ((Element)val).LookupParameter("Sheet Set");
						obj = ((obj2 != null) ? obj2.AsString() : null);
					}
					return (string)obj == filter;
				}).ToList();
			}
			else if (tag == "View")
			{
				_filteredElements = _allElements.Where((SelectableElement elem) => elem.ViewTypeName == filter).ToList();
			}
		}
		lstElements.ItemsSource = _filteredElements;
		UpdateCount();
	}

	private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
	{
		foreach (SelectableElement item in _filteredElements)
		{
			item.IsSelected = true;
		}
		UpdateCount();
	}

	private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
	{
		foreach (SelectableElement item in _filteredElements)
		{
			item.IsSelected = false;
		}
		UpdateCount();
	}

	private void BtnDelete_Click(object sender, RoutedEventArgs e)
	{
		int count = _filteredElements.Count((SelectableElement x) => x.IsSelected);
		if (count == 0)
		{
			MessageBox.Show("Vui lòng chọn ít nhất một mục để xóa!", "Thông báo");
			return;
		}
		MessageBoxResult result = MessageBox.Show($"Bạn có chắc chắn muốn xóa {count} {SelectedElementTypeName}?\n\nHành động này không thể hoàn tác!", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);
		if (result == MessageBoxResult.Yes)
		{
			base.DialogResult = true;
			Close();
		}
	}

	private void UpdateCount()
	{
		int count = _filteredElements?.Count((SelectableElement x) => x.IsSelected) ?? 0;
		txtCount.Text = $"{count} mục đã chọn";
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/25.%20delete%202d%20element/delete2delementwindow.xaml", UriKind.Relative);
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
			cmbElementType = (ComboBox)target;
			cmbElementType.SelectionChanged += CmbElementType_SelectionChanged;
			break;
		case 2:
			pnlSecondaryFilter = (StackPanel)target;
			break;
		case 3:
			txtSecondaryFilterLabel = (TextBlock)target;
			break;
		case 4:
			cmbSecondaryFilter = (ComboBox)target;
			cmbSecondaryFilter.SelectionChanged += CmbSecondaryFilter_SelectionChanged;
			break;
		case 5:
			txtListHeader = (TextBlock)target;
			break;
		case 6:
			lstElements = (ListBox)target;
			break;
		case 7:
			((Button)target).Click += BtnSelectAll_Click;
			break;
		case 8:
			((Button)target).Click += BtnSelectNone_Click;
			break;
		case 9:
			txtCount = (TextBlock)target;
			break;
		case 10:
			((Button)target).Click += BtnDelete_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
