using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIN.Common.Mvvm;
using BIN.Common.Revit;
using BIN.SelectByParam.Models;

namespace BIN.SelectByParam.ViewModels;

public class SelectByParameterViewModel : BaseViewModel
{
	private readonly UIApplication _uiapp;

	private Document _doc;

	private readonly RevitApiHandler _handler;

	private readonly ExternalEvent _externalEvent;

	private List<ElementCacheEntry> _elementCache = new List<ElementCacheEntry>();

	private readonly List<BuiltInCategory> _supportedCategories = new List<BuiltInCategory>
	{
		(BuiltInCategory)(-2001330),
		(BuiltInCategory)(-2001320),
		(BuiltInCategory)(-2001300),
		(BuiltInCategory)(-2000011),
		(BuiltInCategory)(-2000032),
		(BuiltInCategory)(-2000160),
		(BuiltInCategory)(-2000100),
		(BuiltInCategory)(-2000023),
		(BuiltInCategory)(-2000014),
		(BuiltInCategory)(-2000038),
		(BuiltInCategory)(-2000035),
		(BuiltInCategory)(-2000120),
		(BuiltInCategory)(-2008013),
		(BuiltInCategory)(-2008044),
		(BuiltInCategory)(-2008049),
		(BuiltInCategory)(-2008055),
		(BuiltInCategory)(-2008122),
		(BuiltInCategory)(-2008161),
		(BuiltInCategory)(-2008050),
		(BuiltInCategory)(-2008000),
		(BuiltInCategory)(-2008010),
		(BuiltInCategory)(-2008016),
		(BuiltInCategory)(-2008123),
		(BuiltInCategory)(-2008124),
		(BuiltInCategory)(-2008160),
		(BuiltInCategory)(-2008020),
		(BuiltInCategory)(-2008130),
		(BuiltInCategory)(-2008126),
		(BuiltInCategory)(-2008132),
		(BuiltInCategory)(-2008128),
		(BuiltInCategory)(-2001140),
		(BuiltInCategory)(-2008232),
		(BuiltInCategory)(-2001160),
		(BuiltInCategory)(-2008234),
		(BuiltInCategory)(-2008099),
		(BuiltInCategory)(-2001049),
		(BuiltInCategory)(-2001046),
		(BuiltInCategory)(-2001040),
		(BuiltInCategory)(-2001060),
		(BuiltInCategory)(-2001120),
		(BuiltInCategory)(-2008087),
		(BuiltInCategory)(-2008083),
		(BuiltInCategory)(-2008085),
		(BuiltInCategory)(-2008077),
		(BuiltInCategory)(-2008079),
		(BuiltInCategory)(-2008075),
		(BuiltInCategory)(-2008212),
		(BuiltInCategory)(-2008193),
		(BuiltInCategory)(-2008203),
		(BuiltInCategory)(-2008208),
		(BuiltInCategory)(-2000151)
	};

	private ScopeOption _selectedScope = ScopeOption.ActiveView;

	private SBP_CategoryItem _selectedCategory;

	private ParameterItem _selectedParameter;

	private int _matchingElementCount;

	private bool _isLoading;

	private string _statusMessage = "Ready";

	public ObservableCollection<SBP_CategoryItem> Categories { get; set; } = new ObservableCollection<SBP_CategoryItem>();

	public ObservableCollection<ParameterItem> Parameters { get; set; } = new ObservableCollection<ParameterItem>();

	public ObservableCollection<string> ParameterValues { get; set; } = new ObservableCollection<string>();

	public ObservableCollection<string> SelectedValues { get; set; } = new ObservableCollection<string>();

	public ScopeOption SelectedScope
	{
		get
		{
			return _selectedScope;
		}
		set
		{
			if (_selectedScope != value)
			{
				_selectedScope = value;
				OnPropertyChanged("SelectedScope");
				TriggerRefresh();
			}
		}
	}

	public SBP_CategoryItem SelectedCategory
	{
		get
		{
			return _selectedCategory;
		}
		set
		{
			if (_selectedCategory != value)
			{
				_selectedCategory = value;
				OnPropertyChanged("SelectedCategory");
				LoadParametersForCategory();
			}
		}
	}

	public ParameterItem SelectedParameter
	{
		get
		{
			return _selectedParameter;
		}
		set
		{
			if (_selectedParameter != value)
			{
				_selectedParameter = value;
				OnPropertyChanged("SelectedParameter");
				LoadValuesForParameter();
			}
		}
	}

	public int MatchingElementCount
	{
		get
		{
			return _matchingElementCount;
		}
		set
		{
			if (_matchingElementCount != value)
			{
				_matchingElementCount = value;
				OnPropertyChanged("MatchingElementCount");
			}
		}
	}

	public bool IsLoading
	{
		get
		{
			return _isLoading;
		}
		set
		{
			if (_isLoading != value)
			{
				_isLoading = value;
				OnPropertyChanged("IsLoading");
			}
		}
	}

	public string StatusMessage
	{
		get
		{
			return _statusMessage;
		}
		set
		{
			if (_statusMessage != value)
			{
				_statusMessage = value;
				OnPropertyChanged("StatusMessage");
			}
		}
	}

	public ICommand SelectCommand { get; }

	public ICommand IsolateCommand { get; }

	public ICommand HideCommand { get; }

	public ICommand ShowCommand { get; }

	public ICommand RefreshCommand { get; }

	public SelectByParameterViewModel(UIApplication uiapp)
	{
		_uiapp = uiapp;
		_handler = new RevitApiHandler();
		_externalEvent = ExternalEvent.Create((IExternalEventHandler)(object)_handler);
		SelectCommand = new SBP_RelayCommand(ExecuteSelect, CanExecuteAction);
		IsolateCommand = new SBP_RelayCommand(ExecuteIsolate, CanExecuteAction);
		HideCommand = new SBP_RelayCommand(ExecuteHide, CanExecuteAction);
		ShowCommand = new SBP_RelayCommand(ExecuteShow);
		RefreshCommand = new SBP_RelayCommand(delegate
		{
			TriggerRefresh();
		});
	}

	public void InitialLoad()
	{
		TriggerRefresh();
	}

	private void TriggerRefresh()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_011c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0126: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_0035: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				UIDocument activeUIDocument = uiapp.ActiveUIDocument;
				_doc = ((activeUIDocument != null) ? activeUIDocument.Document : null);
				if (_doc == null)
				{
					StatusMessage = "No active document.";
					return Result.Cancelled;
				}
				IsLoading = true;
				StatusMessage = "Loading data...";
				LoadElementCacheInternal();
				LoadCategories();
				Parameters.Clear();
				ParameterValues.Clear();
				SelectedValues.Clear();
				_selectedCategory = null;
				OnPropertyChanged("SelectedCategory");
				_selectedParameter = null;
				OnPropertyChanged("SelectedParameter");
				MatchingElementCount = 0;
				StatusMessage = $"Loaded {_elementCache.Count} elements";
				IsLoading = false;
			}
			catch (Exception ex)
			{
				StatusMessage = "Error: " + ex.Message;
				IsLoading = false;
				TaskDialog.Show("Debug Error", "LoadElementCache failed:\n" + ex.Message + "\n\nStack:\n" + ex.StackTrace);
			}
			return Result.Succeeded;
		};
		_externalEvent.Raise();
	}

	private void LoadElementCacheInternal()
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Expected O, but got Unknown
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Expected O, but got Unknown
		_elementCache.Clear();
		View activeView = _doc.ActiveView;
		FilteredElementCollector collector = ((SelectedScope != ScopeOption.ActiveView || activeView == null) ? new FilteredElementCollector(_doc) : new FilteredElementCollector(_doc, ((Element)activeView).Id));
		List<Element> elements = ((IEnumerable<Element>)collector.WhereElementIsNotElementType()).Where((Element e) => e.Category != null && _supportedCategories.Contains((BuiltInCategory)ElementIdHelper.GetIdValue(e.Category.Id))).ToList();
		foreach (Element elem in elements)
		{
			try
			{
				ElementCacheEntry entry = new ElementCacheEntry
				{
					Id = elem.Id,
					Category = (BuiltInCategory)ElementIdHelper.GetIdValue(elem.Category.Id),
					CategoryName = elem.Category.Name
				};
				foreach (Parameter parameter in elem.Parameters)
				{
					Parameter param = parameter;
					object obj;
					if (param == null)
					{
						obj = null;
					}
					else
					{
						Definition definition = param.Definition;
						obj = ((definition != null) ? definition.Name : null);
					}
					if (obj != null)
					{
						string paramName = param.Definition.Name;
						string paramValue = GetParameterValueAsString(param);
						if (!entry.ParameterValues.ContainsKey(paramName))
						{
							entry.ParameterValues[paramName] = paramValue;
						}
					}
				}
				ElementId typeId = elem.GetTypeId();
				if (typeId != ElementId.InvalidElementId)
				{
					Element elemType = _doc.GetElement(typeId);
					if (elemType != null)
					{
						foreach (Parameter parameter2 in elemType.Parameters)
						{
							Parameter param2 = parameter2;
							object obj2;
							if (param2 == null)
							{
								obj2 = null;
							}
							else
							{
								Definition definition2 = param2.Definition;
								obj2 = ((definition2 != null) ? definition2.Name : null);
							}
							if (obj2 != null)
							{
								string paramName2 = param2.Definition.Name + " (Type)";
								string paramValue2 = GetParameterValueAsString(param2);
								if (!entry.ParameterValues.ContainsKey(paramName2))
								{
									entry.ParameterValues[paramName2] = paramValue2;
								}
							}
						}
					}
				}
				_elementCache.Add(entry);
			}
			catch
			{
			}
		}
	}

	private void LoadCategories()
	{
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		Categories.Clear();
		var categoryGroups = from e in _elementCache
			group e by new { e.Category, e.CategoryName } into g
			orderby g.Key.CategoryName
			select g;
		foreach (var group in categoryGroups)
		{
			Categories.Add(new SBP_CategoryItem
			{
				BuiltInCategory = group.Key.Category,
				Name = group.Key.CategoryName,
				ElementCount = group.Count()
			});
		}
	}

	private void LoadParametersForCategory()
	{
		Parameters.Clear();
		ParameterValues.Clear();
		SelectedValues.Clear();
		SelectedParameter = null;
		MatchingElementCount = 0;
		if (SelectedCategory == null)
		{
			return;
		}
		List<ElementCacheEntry> categoryElements = _elementCache.Where((ElementCacheEntry e) => e.Category == SelectedCategory.BuiltInCategory).ToList();
		List<string> allParamNames = (from n in categoryElements.SelectMany((ElementCacheEntry e) => e.ParameterValues.Keys).Distinct()
			orderby n
			select n).ToList();
		foreach (string paramName in allParamNames)
		{
			bool isType = paramName.EndsWith(" (Type)");
			Parameters.Add(new ParameterItem
			{
				Name = paramName,
				IsInstance = !isType,
				StorageType = (StorageType)3
			});
		}
		StatusMessage = $"Loaded {Parameters.Count} parameters for {SelectedCategory.Name}";
	}

	private void LoadValuesForParameter()
	{
		ParameterValues.Clear();
		SelectedValues.Clear();
		MatchingElementCount = 0;
		if (SelectedCategory == null || SelectedParameter == null)
		{
			return;
		}
		List<ElementCacheEntry> categoryElements = _elementCache.Where((ElementCacheEntry e) => e.Category == SelectedCategory.BuiltInCategory).ToList();
		List<string> distinctValues = (from v in (from e in categoryElements
				where e.ParameterValues.ContainsKey(SelectedParameter.Name)
				select e.ParameterValues[SelectedParameter.Name] into v
				where !string.IsNullOrEmpty(v)
				select v).Distinct()
			orderby v
			select v).ToList();
		foreach (string value in distinctValues)
		{
			ParameterValues.Add(value);
		}
		StatusMessage = $"Found {ParameterValues.Count} unique values";
	}

	private string GetParameterValueAsString(Parameter param)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected I4, but got Unknown
		if (param == null || !param.HasValue)
		{
			return string.Empty;
		}
		StorageType storageType = param.StorageType;
		StorageType val = storageType;
		return (val - 1) switch
		{
			(StorageType)2 => param.AsString() ?? string.Empty, 
			0 => param.AsInteger().ToString(), 
			(StorageType)1 => param.AsValueString() ?? param.AsDouble().ToString("F2"), 
			(StorageType)3 => param.AsValueString() ?? ElementIdHelper.GetIdValue(param.AsElementId()).ToString(), 
			_ => string.Empty, 
		};
	}

	private List<ElementId> GetMatchingElementIds()
	{
		if (SelectedCategory == null)
		{
			return new List<ElementId>();
		}
		IEnumerable<ElementCacheEntry> query = _elementCache.Where((ElementCacheEntry e) => e.Category == SelectedCategory.BuiltInCategory);
		if (SelectedParameter != null && SelectedValues.Any())
		{
			HashSet<string> selectedValuesSet = new HashSet<string>(SelectedValues);
			query = query.Where((ElementCacheEntry e) => e.ParameterValues.ContainsKey(SelectedParameter.Name) && selectedValuesSet.Contains(e.ParameterValues[SelectedParameter.Name]));
		}
		return query.Select((ElementCacheEntry e) => e.Id).ToList();
	}

	public void UpdateMatchingCount()
	{
		MatchingElementCount = GetMatchingElementIds().Count;
		StatusMessage = $"{MatchingElementCount} matching elements";
	}

	private bool CanExecuteAction(object param)
	{
		return SelectedCategory != null;
	}

	private void ExecuteSelect(object param)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		List<ElementId> ids = GetMatchingElementIds();
		if (!ids.Any())
		{
			TaskDialog.Show("Notice", "No elements match the filter criteria.");
			return;
		}
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_006b: Unknown result type (might be due to invalid IL or missing references)
			UIDocument activeUIDocument = uiapp.ActiveUIDocument;
			if (activeUIDocument == null)
			{
				return Result.Cancelled;
			}
			try
			{
				activeUIDocument.Selection.SetElementIds((ICollection<ElementId>)ids);
				StatusMessage = $"Selected {ids.Count} elements";
			}
			catch (Exception ex)
			{
				TaskDialog.Show("Error", ex.Message);
			}
			return Result.Succeeded;
		};
		_externalEvent.Raise();
	}

	private void ExecuteIsolate(object param)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		List<ElementId> ids = GetMatchingElementIds();
		if (!ids.Any())
		{
			TaskDialog.Show("Notice", "No elements match the filter criteria.");
			return;
		}
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_0080: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Expected O, but got Unknown
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_004c: Unknown result type (might be due to invalid IL or missing references)
			UIDocument activeUIDocument = uiapp.ActiveUIDocument;
			if (activeUIDocument == null)
			{
				return Result.Cancelled;
			}
			Document document = activeUIDocument.Document;
			View activeView = document.ActiveView;
			Transaction val = new Transaction(document, "Isolate Elements");
			try
			{
				val.Start();
				try
				{
					activeView.IsolateElementsTemporary((ICollection<ElementId>)ids);
					val.Commit();
					StatusMessage = $"Isolated {ids.Count} elements";
				}
				catch (Exception ex)
				{
					val.RollBack();
					TaskDialog.Show("Error", ex.Message);
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			return Result.Succeeded;
		};
		_externalEvent.Raise();
	}

	private void ExecuteHide(object param)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		List<ElementId> ids = GetMatchingElementIds();
		if (!ids.Any())
		{
			TaskDialog.Show("Notice", "No elements match the filter criteria.");
			return;
		}
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_00da: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_008b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0092: Expected O, but got Unknown
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_010a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0106: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
			UIDocument activeUIDocument = uiapp.ActiveUIDocument;
			if (activeUIDocument == null)
			{
				return Result.Cancelled;
			}
			Document doc = activeUIDocument.Document;
			View activeView = doc.ActiveView;
			List<ElementId> list = ids.Where(delegate(ElementId id)
			{
				Element element = doc.GetElement(id);
				return element != null && element.CanBeHidden(activeView);
			}).ToList();
			if (!list.Any())
			{
				TaskDialog.Show("Notice", "No elements can be hidden in active view.");
				return Result.Succeeded;
			}
			Transaction val = new Transaction(doc, "Hide Elements Temporary");
			try
			{
				val.Start();
				try
				{
					activeView.HideElementsTemporary((ICollection<ElementId>)list);
					val.Commit();
					StatusMessage = $"Temporarily hidden {list.Count} elements";
				}
				catch (Exception ex)
				{
					val.RollBack();
					TaskDialog.Show("Error", ex.Message);
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			return Result.Succeeded;
		};
		_externalEvent.Raise();
	}

	private void ExecuteShow(object param)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_006f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0033: Expected O, but got Unknown
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_009f: Unknown result type (might be due to invalid IL or missing references)
			//IL_009b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0055: Unknown result type (might be due to invalid IL or missing references)
			UIDocument activeUIDocument = uiapp.ActiveUIDocument;
			if (activeUIDocument == null)
			{
				return Result.Cancelled;
			}
			Document document = activeUIDocument.Document;
			View activeView = document.ActiveView;
			Transaction val = new Transaction(document, "Reset Temporary Hide/Isolate");
			try
			{
				val.Start();
				try
				{
					if (activeView.IsTemporaryHideIsolateActive())
					{
						activeView.DisableTemporaryViewMode((TemporaryViewMode)2);
					}
					val.Commit();
					StatusMessage = "Reset hide/isolate status";
				}
				catch (Exception ex)
				{
					val.RollBack();
					TaskDialog.Show("Error", ex.Message);
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			return Result.Succeeded;
		};
		_externalEvent.Raise();
	}
}
