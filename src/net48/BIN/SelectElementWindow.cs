using ComboBox = System.Windows.Controls.ComboBox;
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
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BIN;

public class SelectElementWindow : Window, IComponentConnector
{
	private Document _doc;

	private UIDocument _uidoc;

	private string _currentTab = "Workset";

	private SelectElementEventHandler _handler;

	private ExternalEvent _exEvent;

	private Dictionary<string, List<FilterItem>> _filterData = new Dictionary<string, List<FilterItem>>();

	private Dictionary<ElementId, Element> _allElements = new Dictionary<ElementId, Element>();

	private List<LevelItem> _levels = new List<LevelItem>();

	private List<ElementId> _currentResultIds = new List<ElementId>();

	private List<Workset> _allWorksets = new List<Workset>();

	private List<Element> _allMepSystemTypes = new List<Element>();

	private bool _suppressEvents = false;

	private static readonly HashSet<int> _excludedCategories = new HashSet<int> { -2000279, -2003100, -2000573, -2003101, -2000500, -2006000, -2000193, -2000198, -2000301 };

	internal TabControl tabMain;

	internal CheckBox chkSelectAllItems;

	internal TextBlock tbItemCount;

	internal StackPanel spItemList;

	internal CheckBox chkActiveViewOnly;

	internal CheckBox chkSelectAllLevels;

	internal TextBlock tbLevelCount;

	internal StackPanel spLevelList;

	internal TextBlock tbTotalCount;

	internal DataGrid dgCategorySummary;

	internal TextBlock tbChangeLabel;

	internal ComboBox cmbChangeTarget;

	internal Button btnApplyChange;

	internal CheckBox chkIsolateAndView;

	internal Border borderStatus;

	internal TextBlock tbStatusMessage;

	private bool _contentLoaded;

	public SelectElementWindow(Document doc, UIDocument uidoc, SelectElementEventHandler handler, ExternalEvent exEvent)
	{
		_suppressEvents = true;
		InitializeComponent();
		_doc = doc;
		_uidoc = uidoc;
		_handler = handler;
		_exEvent = exEvent;
		LoadAllData();
		LoadLevels();
		LoadMepSystemTypes();
		tabMain.SelectedIndex = 0;
		RefreshItemList();
		RefreshChangeComboBox();
		_suppressEvents = false;
	}

	private void ExecuteApi(Action action)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		_handler.Action = action;
		_exEvent.Raise();
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	private bool IsValidElement(Element e)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Invalid comparison between Unknown and I4
		if (e == null || e.Category == null)
		{
			return false;
		}
		if ((int)e.Category.CategoryType != 1)
		{
			return false;
		}
		if (e is View || e is ViewSheet)
		{
			return false;
		}
		if (((object)e).GetType().Name == "ScheduleSheetInstance")
		{
			return false;
		}
		int catId = e.Category.GetIdInt();
		if (_excludedCategories.Contains(catId))
		{
			return false;
		}
		try
		{
			Parameter viewTemplP = e.LookupParameter("View Template");
			View v = (View)(object)((e is View) ? e : null);
			if (v != null && v.IsTemplate)
			{
				return false;
			}
		}
		catch
		{
		}
		return true;
	}

	private void LoadAllData()
	{
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		List<Element> allElements = ((chkActiveViewOnly == null || chkActiveViewOnly.IsChecked != true || _doc.ActiveView == null) ? ((IEnumerable<Element>)new FilteredElementCollector(_doc).WhereElementIsNotElementType()).Where((Element e) => IsValidElement(e)).ToList() : ((IEnumerable<Element>)new FilteredElementCollector(_doc, ((Element)_doc.ActiveView).Id).WhereElementIsNotElementType()).Where((Element e) => IsValidElement(e)).ToList());
		_allElements.Clear();
		foreach (Element elem in allElements)
		{
			_allElements[elem.Id] = elem;
		}
		_filterData.Clear();
		HashSet<string> userWorksetNames = new HashSet<string>();
		if (_doc.IsWorkshared)
		{
			IList<Workset> userWorksets = new FilteredWorksetCollector(_doc).OfKind((WorksetKind)4).ToWorksets();
			foreach (Workset ws in userWorksets)
			{
				userWorksetNames.Add(((WorksetPreview)ws).Name);
			}
		}
		Dictionary<string, FilterItem> worksetItems = new Dictionary<string, FilterItem>();
		foreach (Element elem2 in allElements)
		{
			string worksetName = GetWorksetName(elem2);
			if (!string.IsNullOrEmpty(worksetName) && userWorksetNames.Contains(worksetName))
			{
				if (!worksetItems.ContainsKey(worksetName))
				{
					worksetItems[worksetName] = new FilterItem
					{
						Name = worksetName
					};
				}
				worksetItems[worksetName].ElementIds.Add(elem2.Id);
			}
		}
		foreach (FilterItem item in worksetItems.Values)
		{
			item.ElementCount = item.ElementIds.Count;
		}
		_filterData["Workset"] = worksetItems.Values.OrderBy((FilterItem x) => x.Name).ToList();
		Dictionary<string, FilterItem> systemTypeItems = new Dictionary<string, FilterItem>();
		foreach (Element elem3 in allElements)
		{
			string sysType = GetSystemTypeName(elem3);
			if (!string.IsNullOrEmpty(sysType))
			{
				if (!systemTypeItems.ContainsKey(sysType))
				{
					systemTypeItems[sysType] = new FilterItem
					{
						Name = sysType
					};
				}
				systemTypeItems[sysType].ElementIds.Add(elem3.Id);
			}
		}
		foreach (FilterItem item2 in systemTypeItems.Values)
		{
			item2.ElementCount = item2.ElementIds.Count;
		}
		_filterData["SystemType"] = systemTypeItems.Values.OrderBy((FilterItem x) => x.Name).ToList();
		Dictionary<string, FilterItem> sysAbbrItems = new Dictionary<string, FilterItem>();
		foreach (Element elem4 in allElements)
		{
			string abbr = GetSystemAbbreviation(elem4);
			if (!string.IsNullOrEmpty(abbr))
			{
				if (!sysAbbrItems.ContainsKey(abbr))
				{
					sysAbbrItems[abbr] = new FilterItem
					{
						Name = abbr
					};
				}
				sysAbbrItems[abbr].ElementIds.Add(elem4.Id);
			}
		}
		foreach (FilterItem item3 in sysAbbrItems.Values)
		{
			item3.ElementCount = item3.ElementIds.Count;
		}
		_filterData["SystemAbbreviation"] = sysAbbrItems.Values.OrderBy((FilterItem x) => x.Name).ToList();
		Dictionary<string, FilterItem> serviceTypeItems = new Dictionary<string, FilterItem>();
		foreach (Element elem5 in allElements)
		{
			string svc = GetServiceType(elem5);
			if (!string.IsNullOrEmpty(svc))
			{
				if (!serviceTypeItems.ContainsKey(svc))
				{
					serviceTypeItems[svc] = new FilterItem
					{
						Name = svc
					};
				}
				serviceTypeItems[svc].ElementIds.Add(elem5.Id);
			}
		}
		foreach (FilterItem item4 in serviceTypeItems.Values)
		{
			item4.ElementCount = item4.ElementIds.Count;
		}
		_filterData["ServiceType"] = serviceTypeItems.Values.OrderBy((FilterItem x) => x.Name).ToList();
	}

	private void LoadLevels()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		_levels = (from Level l in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(Level))
			orderby l.Elevation
			select new LevelItem
			{
				Name = ((Element)l).Name,
				LevelId = ((Element)l).Id,
				Elevation = l.Elevation
			}).ToList();
		spLevelList.Children.Clear();
		foreach (LevelItem level in _levels)
		{
			CheckBox cb = new CheckBox
			{
				Content = level.Name,
				Tag = level,
				IsChecked = true,
				Margin = new Thickness(2.0, 1.0, 2.0, 1.0),
				Padding = new Thickness(4.0, 1.0, 4.0, 1.0)
			};
			cb.Checked += LevelCheckbox_Changed;
			cb.Unchecked += LevelCheckbox_Changed;
			spLevelList.Children.Add(cb);
		}
		tbLevelCount.Text = $"{_levels.Count} levels";
	}

	private void LoadMepSystemTypes()
	{
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		_allWorksets.Clear();
		if (_doc.IsWorkshared)
		{
			_allWorksets = (from w in new FilteredWorksetCollector(_doc).OfKind((WorksetKind)4).ToWorksets()
				orderby ((WorksetPreview)w).Name
				select w).ToList();
		}
		_allMepSystemTypes.Clear();
		IList<Element> pipingTypes = new FilteredElementCollector(_doc).OfClass(typeof(PipingSystemType)).ToElements();
		_allMepSystemTypes.AddRange(pipingTypes);
		IList<Element> ductTypes = new FilteredElementCollector(_doc).OfClass(typeof(MechanicalSystemType)).ToElements();
		_allMepSystemTypes.AddRange(ductTypes);
		_allMepSystemTypes = _allMepSystemTypes.OrderBy((Element e) => e.Name).ToList();
	}

	private void RefreshChangeComboBox()
	{
		cmbChangeTarget.Items.Clear();
		switch (_currentTab)
		{
		case "Workset":
			tbChangeLabel.Text = "Change Workset to:";
			foreach (Workset ws in _allWorksets)
			{
				cmbChangeTarget.Items.Add(new ChangeTargetItem
				{
					DisplayName = ((WorksetPreview)ws).Name,
					WorksetIdInt = ws.GetIdInt()
				});
			}
			btnApplyChange.IsEnabled = _doc.IsWorkshared;
			cmbChangeTarget.IsEnabled = _doc.IsWorkshared;
			break;
		case "SystemType":
			tbChangeLabel.Text = "Change System Type to:";
			foreach (Element st in _allMepSystemTypes)
			{
				cmbChangeTarget.Items.Add(new ChangeTargetItem
				{
					DisplayName = st.Name,
					ElementId = st.Id
				});
			}
			btnApplyChange.IsEnabled = true;
			cmbChangeTarget.IsEnabled = true;
			break;
		case "SystemAbbreviation":
		{
			tbChangeLabel.Text = "Change Abbreviation to:";
			Dictionary<string, ElementId> abbreviations = new Dictionary<string, ElementId>();
			foreach (Element st2 in _allMepSystemTypes)
			{
				string abbr = GetAbbreviationFromSystemType(st2);
				if (!string.IsNullOrEmpty(abbr) && !abbreviations.ContainsKey(abbr))
				{
					abbreviations[abbr] = st2.Id;
				}
			}
			foreach (KeyValuePair<string, ElementId> kvp in abbreviations.OrderBy((KeyValuePair<string, ElementId> x) => x.Key))
			{
				cmbChangeTarget.Items.Add(new ChangeTargetItem
				{
					DisplayName = kvp.Key,
					ElementId = kvp.Value
				});
			}
			btnApplyChange.IsEnabled = true;
			cmbChangeTarget.IsEnabled = true;
			break;
		}
		case "ServiceType":
			tbChangeLabel.Text = "Change Service Type to:";
			if (_filterData.ContainsKey("ServiceType"))
			{
				foreach (FilterItem item in _filterData["ServiceType"])
				{
					cmbChangeTarget.Items.Add(new ChangeTargetItem
					{
						DisplayName = item.Name
					});
				}
			}
			btnApplyChange.IsEnabled = true;
			cmbChangeTarget.IsEnabled = true;
			break;
		}
		if (cmbChangeTarget.Items.Count > 0)
		{
			cmbChangeTarget.SelectedIndex = 0;
		}
	}

	private string GetAbbreviationFromSystemType(Element systemType)
	{
		try
		{
			Parameter p = systemType.LookupParameter("Abbreviation");
			if (p != null && p.HasValue && !string.IsNullOrEmpty(p.AsString()))
			{
				return p.AsString();
			}
		}
		catch
		{
		}
		return null;
	}

	private string GetServiceTypeFromSystemType(Element systemType)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Invalid comparison between Unknown and I4
		try
		{
			Parameter sp = systemType.LookupParameter("Service Type");
			if (sp != null && sp.HasValue)
			{
				if ((int)sp.StorageType == 1)
				{
					string val = sp.AsValueString();
					if (!string.IsNullOrEmpty(val))
					{
						return val;
					}
				}
				else if ((int)sp.StorageType == 3)
				{
					string val2 = sp.AsString();
					if (!string.IsNullOrEmpty(val2))
					{
						return val2;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private string GetWorksetName(Element elem)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		try
		{
			if (!_doc.IsWorkshared)
			{
				return "(Not Workshared)";
			}
			Parameter wsParam = elem.get_Parameter((BuiltInParameter)(-1002053));
			if (wsParam != null)
			{
				int wsId = wsParam.AsInteger();
				Workset ws = _doc.GetWorksetTable().GetWorkset(new WorksetId(wsId));
				if (ws != null)
				{
					return ((WorksetPreview)ws).Name;
				}
			}
		}
		catch
		{
		}
		return "(No Workset)";
	}

	private string GetSystemTypeName(Element elem)
	{
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Invalid comparison between Unknown and I4
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		//IL_0179: Invalid comparison between Unknown and I4
		try
		{
			Parameter p = elem.get_Parameter((BuiltInParameter)(-1140334));
			if (p != null && p.HasValue)
			{
				ElementId id = p.AsElementId();
				if (id != (ElementId)null && id != ElementId.InvalidElementId)
				{
					Element st = _doc.GetElement(id);
					if (st != null)
					{
						return st.Name;
					}
				}
			}
			p = elem.get_Parameter((BuiltInParameter)(-1140333));
			if (p != null && p.HasValue)
			{
				ElementId id2 = p.AsElementId();
				if (id2 != (ElementId)null && id2 != ElementId.InvalidElementId)
				{
					Element st2 = _doc.GetElement(id2);
					if (st2 != null)
					{
						return st2.Name;
					}
				}
			}
			p = elem.get_Parameter((BuiltInParameter)(-1140018));
			if (p != null && p.HasValue && !string.IsNullOrEmpty(p.AsString()))
			{
				return p.AsString();
			}
			p = elem.LookupParameter("System Type");
			if (p != null && p.HasValue)
			{
				if ((int)p.StorageType == 4)
				{
					Element st3 = _doc.GetElement(p.AsElementId());
					if (st3 != null)
					{
						return st3.Name;
					}
				}
				else if ((int)p.StorageType == 3 && !string.IsNullOrEmpty(p.AsString()))
				{
					return p.AsString();
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private string GetSystemAbbreviation(Element elem)
	{
		try
		{
			Parameter p = elem.LookupParameter("System Abbreviation");
			if (p != null && p.HasValue && !string.IsNullOrEmpty(p.AsValueString()))
			{
				return p.AsValueString();
			}
			Parameter sysParam = elem.get_Parameter((BuiltInParameter)(-1140334));
			if (sysParam == null)
			{
				sysParam = elem.get_Parameter((BuiltInParameter)(-1140333));
			}
			if (sysParam != null && sysParam.HasValue)
			{
				ElementId sysTypeId = sysParam.AsElementId();
				if (sysTypeId != (ElementId)null && sysTypeId != ElementId.InvalidElementId)
				{
					Element sysType = _doc.GetElement(sysTypeId);
					if (sysType != null)
					{
						Parameter abbrP = sysType.LookupParameter("Abbreviation");
						if (abbrP != null && abbrP.HasValue && !string.IsNullOrEmpty(abbrP.AsString()))
						{
							return abbrP.AsString();
						}
					}
				}
			}
			p = elem.get_Parameter((BuiltInParameter)(-1140332));
			if (p != null && p.HasValue && !string.IsNullOrEmpty(p.AsString()))
			{
				return p.AsString();
			}
		}
		catch
		{
		}
		return null;
	}

	private string GetServiceType(Element elem)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Invalid comparison between Unknown and I4
		try
		{
			Parameter p = elem.LookupParameter("Service Type");
			if (p != null && p.HasValue)
			{
				if ((int)p.StorageType == 1)
				{
					string val = p.AsValueString();
					if (!string.IsNullOrEmpty(val))
					{
						return val;
					}
				}
				else if ((int)p.StorageType == 3 && !string.IsNullOrEmpty(p.AsString()))
				{
					return p.AsString();
				}
			}
			p = elem.get_Parameter((BuiltInParameter)(-1140334));
			if (p == null)
			{
				p = elem.get_Parameter((BuiltInParameter)(-1140333));
			}
			if (p != null && p.HasValue)
			{
				ElementId sysTypeId = p.AsElementId();
				if (sysTypeId != (ElementId)null && sysTypeId != ElementId.InvalidElementId)
				{
					Element sysType = _doc.GetElement(sysTypeId);
					if (sysType != null)
					{
						Parameter svcP = sysType.LookupParameter("Service Type");
						if (svcP != null && svcP.HasValue)
						{
							string val2 = svcP.AsValueString();
							if (!string.IsNullOrEmpty(val2))
							{
								return val2;
							}
						}
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private ElementId GetElementLevelId(Element elem)
	{
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Invalid comparison between Unknown and I4
		try
		{
			Parameter p = elem.get_Parameter((BuiltInParameter)(-1114000));
			if (p != null && p.HasValue && p.AsElementId() != ElementId.InvalidElementId)
			{
				return p.AsElementId();
			}
			if (elem.LevelId != (ElementId)null && elem.LevelId != ElementId.InvalidElementId)
			{
				return elem.LevelId;
			}
			p = elem.get_Parameter((BuiltInParameter)(-1002062));
			if (p != null && p.HasValue && p.AsElementId() != ElementId.InvalidElementId)
			{
				return p.AsElementId();
			}
			p = elem.LookupParameter("Reference Level");
			if (p != null && p.HasValue && (int)p.StorageType == 4 && p.AsElementId() != ElementId.InvalidElementId)
			{
				return p.AsElementId();
			}
			p = elem.get_Parameter((BuiltInParameter)(-1001352));
			if (p != null && p.HasValue && p.AsElementId() != ElementId.InvalidElementId)
			{
				return p.AsElementId();
			}
		}
		catch
		{
		}
		return ElementId.InvalidElementId;
	}

	private Parameter GetSystemTypeParam(Element elem)
	{
		Parameter p = elem.get_Parameter((BuiltInParameter)(-1140334));
		if (p != null && !((APIObject)p).IsReadOnly)
		{
			return p;
		}
		p = elem.get_Parameter((BuiltInParameter)(-1140333));
		if (p != null && !((APIObject)p).IsReadOnly)
		{
			return p;
		}
		return null;
	}

	private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_suppressEvents && tabMain != null && tabMain.SelectedItem is TabItem selectedTab)
		{
			_currentTab = selectedTab.Tag?.ToString() ?? "Workset";
			RefreshItemList();
			RefreshChangeComboBox();
		}
	}

	private void ChkActiveViewOnly_Changed(object sender, RoutedEventArgs e)
	{
		if (!_suppressEvents && _doc != null)
		{
			ExecuteApi(delegate
			{
				_suppressEvents = true;
				LoadAllData();
				RefreshItemList();
				_suppressEvents = false;
				UpdateResults();
			});
		}
	}

	private void RefreshItemList()
	{
		if (spItemList == null || chkSelectAllItems == null || tbItemCount == null)
		{
			return;
		}
		_suppressEvents = true;
		spItemList.Children.Clear();
		chkSelectAllItems.IsChecked = false;
		if (!_filterData.ContainsKey(_currentTab))
		{
			tbItemCount.Text = "0 items";
			_suppressEvents = false;
			return;
		}
		List<FilterItem> items = _filterData[_currentTab];
		tbItemCount.Text = $"{items.Count} items";
		foreach (FilterItem item in items)
		{
			CheckBox cb = new CheckBox
			{
				Content = new TextBlock
				{
					Text = $"{item.Name}  ({item.ElementCount})"
				},
				Tag = item,
				IsChecked = false,
				Margin = new Thickness(2.0, 1.0, 2.0, 1.0),
				Padding = new Thickness(4.0, 1.0, 4.0, 1.0)
			};
			cb.Checked += ItemCheckbox_Changed;
			cb.Unchecked += ItemCheckbox_Changed;
			spItemList.Children.Add(cb);
		}
		_suppressEvents = false;
		UpdateResults();
	}

	private void ItemCheckbox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_suppressEvents && spItemList != null)
		{
			UpdateResults();
		}
	}

	private void LevelCheckbox_Changed(object sender, RoutedEventArgs e)
	{
		if (!_suppressEvents && spLevelList != null)
		{
			UpdateResults();
		}
	}

	private void ChkSelectAllItems_Changed(object sender, RoutedEventArgs e)
	{
		if (_suppressEvents || spItemList == null || chkSelectAllItems == null)
		{
			return;
		}
		_suppressEvents = true;
		bool isChecked = chkSelectAllItems.IsChecked == true;
		foreach (object child in spItemList.Children)
		{
			if (child is CheckBox cb)
			{
				cb.IsChecked = isChecked;
			}
		}
		_suppressEvents = false;
		UpdateResults();
	}

	private void ChkSelectAllLevels_Changed(object sender, RoutedEventArgs e)
	{
		if (_suppressEvents || spLevelList == null || chkSelectAllLevels == null)
		{
			return;
		}
		_suppressEvents = true;
		bool isChecked = chkSelectAllLevels.IsChecked == true;
		foreach (object child in spLevelList.Children)
		{
			if (child is CheckBox cb)
			{
				cb.IsChecked = isChecked;
			}
		}
		_suppressEvents = false;
		UpdateResults();
	}

	private void UpdateResults()
	{
		HashSet<ElementId> selectedIds = new HashSet<ElementId>();
		foreach (object child in spItemList.Children)
		{
			if (!(child is CheckBox { IsChecked: var isChecked } cb) || isChecked != true || !(cb.Tag is FilterItem item))
			{
				continue;
			}
			foreach (ElementId id in item.ElementIds)
			{
				selectedIds.Add(id);
			}
		}
		HashSet<ElementId> selectedLevelIds = new HashSet<ElementId>();
		bool anyLevelChecked = false;
		foreach (object child2 in spLevelList.Children)
		{
			if (child2 is CheckBox { IsChecked: var isChecked2 } cb2 && isChecked2 == true && cb2.Tag is LevelItem lvl)
			{
				selectedLevelIds.Add(lvl.LevelId);
				anyLevelChecked = true;
			}
		}
		_currentResultIds.Clear();
		foreach (ElementId id2 in selectedIds)
		{
			if (!_allElements.ContainsKey(id2))
			{
				continue;
			}
			Element elem = _allElements[id2];
			if (anyLevelChecked)
			{
				ElementId levelId = GetElementLevelId(elem);
				if (levelId == ElementId.InvalidElementId)
				{
					if (chkSelectAllLevels.IsChecked == true)
					{
						_currentResultIds.Add(id2);
					}
				}
				else if (selectedLevelIds.Contains(levelId))
				{
					_currentResultIds.Add(id2);
				}
			}
			else
			{
				_currentResultIds.Add(id2);
			}
		}
		UpdateCategorySummary();
	}

	private void UpdateCategorySummary()
	{
		Dictionary<string, int> categoryCount = new Dictionary<string, int>();
		foreach (ElementId id in _currentResultIds)
		{
			if (_allElements.ContainsKey(id))
			{
				Element elem = _allElements[id];
				Category category = elem.Category;
				string catName = ((category != null) ? category.Name : null) ?? "(Unknown)";
				if (!categoryCount.ContainsKey(catName))
				{
					categoryCount[catName] = 0;
				}
				categoryCount[catName]++;
			}
		}
		List<CategorySummaryItem> summaryList = (from kvp in categoryCount
			select new CategorySummaryItem
			{
				CategoryName = kvp.Key,
				Count = kvp.Value
			} into x
			orderby x.Count descending
			select x).ToList();
		dgCategorySummary.ItemsSource = summaryList;
		tbTotalCount.Text = $"Total: {_currentResultIds.Count} elements";
	}

	private void ShowStatus(string message)
	{
		borderStatus.Visibility = System.Windows.Visibility.Visible;
		tbStatusMessage.Text = message;
	}

	private void BtnSelect_Click(object sender, RoutedEventArgs e)
	{
		if (_currentResultIds.Count == 0)
		{
			ShowStatus("⚠ không có đối tượng nào được chọn. Vui lòng tick chọn ít nhất một mục.");
			return;
		}
		ExecuteApi(delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0012: Expected O, but got Unknown
			//IL_02df: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_021c: Unknown result type (might be due to invalid IL or missing references)
			Transaction val = new Transaction(_doc, "BIN_SelectAndHide");
			try
			{
				val.Start();
				try
				{
					_uidoc.Selection.SetElementIds((ICollection<ElementId>)_currentResultIds);
					if (chkIsolateAndView.IsChecked == true)
					{
						View activeView = _doc.ActiveView;
						if (activeView != null)
						{
							PermanentHideOthers(activeView, _currentResultIds);
						}
						List<FilterItem> list = new List<FilterItem>();
						foreach (object current in spItemList.Children)
						{
							if (current is CheckBox { IsChecked: var isChecked } checkBox && isChecked == true && checkBox.Tag is FilterItem item)
							{
								list.Add(item);
							}
						}
						if (list.Count > 0)
						{
							ViewFamilyType val2 = ((IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ViewFamilyType))).Cast<ViewFamilyType>().FirstOrDefault((ViewFamilyType x) => (int)x.ViewFamily == 102);
							if (val2 != null)
							{
								int num = 0;
								foreach (FilterItem current2 in list)
								{
									string viewName = "B_3D_" + current2.Name;
									string text = "\\{[:]|;<>?`~";
									foreach (char c in text)
									{
										viewName = viewName.Replace(c.ToString(), "_");
									}
									if (viewName.Length > 100)
									{
										viewName = viewName.Substring(0, 100);
									}
									View3D val3 = ((IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(View3D))).Cast<View3D>().FirstOrDefault((View3D v) => ((Element)v).Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));
									if (val3 == null)
									{
										val3 = View3D.CreateIsometric(_doc, ((Element)val2).Id);
										((Element)val3).Name = viewName;
									}
									PermanentHideOthers((View)(object)val3, current2.ElementIds);
									num++;
								}
							}
						}
					}
					val.Commit();
					ShowStatus($"✔ Đã chọn {_currentResultIds.Count} đối tượng.");
				}
				catch (Exception ex)
				{
					val.RollBack();
					ShowStatus("✖ Lỗi: " + ex.Message);
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		});
	}

	private void PermanentHideOthers(View view, ICollection<ElementId> idsToKeep)
	{
		if (view != null && idsToKeep != null && idsToKeep.Count != 0)
		{
			if (view.IsTemporaryHideIsolateActive())
			{
				view.DisableTemporaryViewMode((TemporaryViewMode)2);
			}
			view.IsolateElementsTemporary(idsToKeep);
			view.ConvertTemporaryHideIsolateToPermanent();
		}
	}

	private void BtnApplyChange_Click(object sender, RoutedEventArgs e)
	{
		if (cmbChangeTarget.SelectedItem == null)
		{
			ShowStatus("⚠ Vui lòng chọn giá trị đích.");
		}
		else if (_currentResultIds.Count == 0)
		{
			ShowStatus("⚠ Không có đối tượng nào được chọn.");
		}
		else if (cmbChangeTarget.SelectedItem is ChangeTargetItem target)
		{
			switch (_currentTab)
			{
			case "Workset":
				ApplyChangeWorkset(target);
				break;
			case "SystemType":
				ApplyChangeSystemType(target);
				break;
			case "SystemAbbreviation":
				ApplyChangeAbbreviation(target);
				break;
			case "ServiceType":
				ApplyChangeServiceType(target);
				break;
			}
		}
	}

	private void ApplyChangeWorkset(ChangeTargetItem target)
	{
		if (!_doc.IsWorkshared)
		{
			ShowStatus("⚠ File này chưa bật Worksharing.");
			return;
		}
		Workset targetWs = _allWorksets.FirstOrDefault((Workset w) => w.GetIdInt() == target.WorksetIdInt);
		if (targetWs == null)
		{
			ShowStatus("✖ Không tìm thấy workset đích.");
			return;
		}
		MessageBoxResult result = MessageBox.Show($"Chuyển {_currentResultIds.Count} đối tượng sang workset \"{((WorksetPreview)targetWs).Name}\"?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
		if (result != MessageBoxResult.Yes)
		{
			return;
		}
		ExecuteApi(delegate
		{
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Expected O, but got Unknown
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_00db: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				int num = 0;
				int num2 = 0;
				Transaction val = new Transaction(_doc, "BIN_ChangeWorkset");
				try
				{
					val.Start();
					foreach (ElementId current in _currentResultIds)
					{
						try
						{
							Element element = _doc.GetElement(current);
							if (element == null)
							{
								num2++;
							}
							else
							{
								Parameter val2 = element.get_Parameter((BuiltInParameter)(-1002053));
								if (val2 != null && !((APIObject)val2).IsReadOnly)
								{
									val2.Set(targetWs.GetIdInt());
									num++;
								}
								else
								{
									num2++;
								}
							}
						}
						catch
						{
							num2++;
						}
					}
					val.Commit();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
				ShowStatus($"✔ Đổi workset: {num} thành công, {num2} bỏ qua/lỗi.");
				ReloadAllData();
			}
			catch (Exception ex)
			{
				ShowStatus("✖ Lỗi: " + ex.Message);
			}
		});
	}

	private void ApplyChangeSystemType(ChangeTargetItem target)
	{
		if (target.ElementId == (ElementId)null || target.ElementId == ElementId.InvalidElementId)
		{
			ShowStatus("✖ Giá trị đích không hợp lệ.");
			return;
		}
		Element targetST = _doc.GetElement(target.ElementId);
		if (targetST == null)
		{
			ShowStatus("✖ Không tìm thấy System Type đích.");
			return;
		}
		MessageBoxResult result = MessageBox.Show($"Đổi System Type của {_currentResultIds.Count} đối tượng sang \"{targetST.Name}\"?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
		if (result != MessageBoxResult.Yes)
		{
			return;
		}
		ExecuteApi(delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				Transaction val = new Transaction(_doc, "BIN_ChangeSystemType");
				try
				{
					val.Start();
					foreach (ElementId current in _currentResultIds)
					{
						try
						{
							Element element = _doc.GetElement(current);
							if (element == null)
							{
								num3++;
							}
							else
							{
								Parameter systemTypeParam = GetSystemTypeParam(element);
								if (systemTypeParam != null)
								{
									systemTypeParam.Set(target.ElementId);
									num++;
								}
								else
								{
									num3++;
								}
							}
						}
						catch
						{
							num2++;
						}
					}
					val.Commit();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
				ShowStatus($"✔ Đổi System Type: {num} thành công, {num3} không có param, {num2} lỗi.");
				ReloadAllData();
			}
			catch (Exception ex)
			{
				ShowStatus("✖ Lỗi: " + ex.Message);
			}
		});
	}

	private void ApplyChangeAbbreviation(ChangeTargetItem target)
	{
		string targetAbbr = target.DisplayName;
		Dictionary<string, ElementId> matchingSystemTypes = new Dictionary<string, ElementId>();
		foreach (Element st in _allMepSystemTypes)
		{
			string abbr = GetAbbreviationFromSystemType(st);
			if (abbr == targetAbbr)
			{
				string className = ((object)st).GetType().Name;
				if (!matchingSystemTypes.ContainsKey(className))
				{
					matchingSystemTypes[className] = st.Id;
				}
			}
		}
		if (matchingSystemTypes.Count == 0)
		{
			ShowStatus("✖ Không tìm thấy System Type nào có Abbreviation \"" + targetAbbr + "\".");
			return;
		}
		MessageBoxResult result = MessageBox.Show($"Đổi Abbreviation của {_currentResultIds.Count} đối tượng sang \"{targetAbbr}\"?\n" + "(Bằng cách đổi System Type sang loại có abbreviation tương ứng)", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
		if (result != MessageBoxResult.Yes)
		{
			return;
		}
		ExecuteApi(delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_0188: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				Transaction val = new Transaction(_doc, "BIN_ChangeAbbreviation");
				try
				{
					val.Start();
					foreach (ElementId current in _currentResultIds)
					{
						try
						{
							Element element = _doc.GetElement(current);
							if (element == null)
							{
								num3++;
							}
							else
							{
								Parameter systemTypeParam = GetSystemTypeParam(element);
								if (systemTypeParam == null)
								{
									num3++;
								}
								else
								{
									Parameter val2 = element.get_Parameter((BuiltInParameter)(-1140334));
									Parameter val3 = element.get_Parameter((BuiltInParameter)(-1140333));
									ElementId val4 = ElementId.InvalidElementId;
									if (val2 != null && !((APIObject)val2).IsReadOnly && matchingSystemTypes.ContainsKey("PipingSystemType"))
									{
										val4 = matchingSystemTypes["PipingSystemType"];
									}
									else if (val3 != null && !((APIObject)val3).IsReadOnly && matchingSystemTypes.ContainsKey("MechanicalSystemType"))
									{
										val4 = matchingSystemTypes["MechanicalSystemType"];
									}
									if (val4 == ElementId.InvalidElementId)
									{
										val4 = matchingSystemTypes.Values.First();
									}
									systemTypeParam.Set(val4);
									num++;
								}
							}
						}
						catch
						{
							num2++;
						}
					}
					val.Commit();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
				ShowStatus($"✔ Đổi Abbreviation → \"{targetAbbr}\": {num} thành công, {num3} không có param, {num2} lỗi.");
				ReloadAllData();
			}
			catch (Exception ex)
			{
				ShowStatus("✖ Lỗi: " + ex.Message);
			}
		});
	}

	private void ApplyChangeServiceType(ChangeTargetItem target)
	{
		string targetSvc = target.DisplayName;
		MessageBoxResult result = MessageBox.Show($"Đổi Service Type của {_currentResultIds.Count} đối tượng sang \"{targetSvc}\"?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
		if (result != MessageBoxResult.Yes)
		{
			return;
		}
		ExecuteApi(delegate
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected O, but got Unknown
			//IL_0020: Unknown result type (might be due to invalid IL or missing references)
			//IL_017b: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a6: Invalid comparison between Unknown and I4
			//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00cf: Invalid comparison between Unknown and I4
			try
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				Transaction val = new Transaction(_doc, "BIN_ChangeServiceType");
				try
				{
					val.Start();
					foreach (ElementId current in _currentResultIds)
					{
						try
						{
							Element element = _doc.GetElement(current);
							if (element == null)
							{
								num3++;
							}
							else
							{
								Parameter val2 = element.LookupParameter("Service Type");
								if (val2 == null || ((APIObject)val2).IsReadOnly)
								{
									num3++;
								}
								else if ((int)val2.StorageType == 3)
								{
									val2.Set(targetSvc);
									num++;
								}
								else if ((int)val2.StorageType == 1)
								{
									bool flag = false;
									int num4 = val2.AsInteger();
									for (int i = 0; i <= 20; i++)
									{
										val2.Set(i);
										if (val2.AsValueString() == targetSvc)
										{
											flag = true;
											num++;
											break;
										}
									}
									if (!flag)
									{
										val2.Set(num4);
										num3++;
									}
								}
								else
								{
									num3++;
								}
							}
						}
						catch
						{
							num2++;
						}
					}
					val.Commit();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
				ShowStatus($"✔ Đổi Service Type → \"{targetSvc}\": {num} thành công, {num3} không đổi được, {num2} lỗi.");
				ReloadAllData();
			}
			catch (Exception ex)
			{
				ShowStatus("✖ Lỗi: " + ex.Message);
			}
		});
	}

	private void ReloadAllData()
	{
		LoadAllData();
		LoadMepSystemTypes();
		RefreshItemList();
		RefreshChangeComboBox();
	}

	private void BtnClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/36.%20select%20element/selectelementwindow.xaml", UriKind.Relative);
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
			tabMain = (TabControl)target;
			tabMain.SelectionChanged += TabMain_SelectionChanged;
			break;
		case 2:
			chkSelectAllItems = (CheckBox)target;
			chkSelectAllItems.Checked += ChkSelectAllItems_Changed;
			chkSelectAllItems.Unchecked += ChkSelectAllItems_Changed;
			break;
		case 3:
			tbItemCount = (TextBlock)target;
			break;
		case 4:
			spItemList = (StackPanel)target;
			break;
		case 5:
			chkActiveViewOnly = (CheckBox)target;
			chkActiveViewOnly.Checked += ChkActiveViewOnly_Changed;
			chkActiveViewOnly.Unchecked += ChkActiveViewOnly_Changed;
			break;
		case 6:
			chkSelectAllLevels = (CheckBox)target;
			chkSelectAllLevels.Checked += ChkSelectAllLevels_Changed;
			chkSelectAllLevels.Unchecked += ChkSelectAllLevels_Changed;
			break;
		case 7:
			tbLevelCount = (TextBlock)target;
			break;
		case 8:
			spLevelList = (StackPanel)target;
			break;
		case 9:
			tbTotalCount = (TextBlock)target;
			break;
		case 10:
			dgCategorySummary = (DataGrid)target;
			break;
		case 11:
			tbChangeLabel = (TextBlock)target;
			break;
		case 12:
			cmbChangeTarget = (ComboBox)target;
			break;
		case 13:
			btnApplyChange = (Button)target;
			btnApplyChange.Click += BtnApplyChange_Click;
			break;
		case 14:
			chkIsolateAndView = (CheckBox)target;
			break;
		case 15:
			((Button)target).Click += BtnSelect_Click;
			break;
		case 16:
			((Button)target).Click += BtnClose_Click;
			break;
		case 17:
			borderStatus = (Border)target;
			break;
		case 18:
			tbStatusMessage = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
