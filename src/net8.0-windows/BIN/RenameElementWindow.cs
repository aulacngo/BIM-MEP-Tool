using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public class RenameElementWindow : Window, IComponentConnector, IStyleConnector
{
	private readonly UIApplication _uiapp;

	private readonly Document _doc;

	private List<ViewSheet> _allSheets;

	private List<ViewSheetSet> _sheetSets;

	private ObservableCollection<SheetItem> _displayItems;

	private ObservableCollection<RenameItem> _templateItems;

	private ObservableCollection<FamilyTreeNode> _familyNodes;

	private ObservableCollection<RenameItem> _filterItems;

	private ObservableCollection<RenameItem> _viewItems;

	private bool _templateLoaded = false;

	private bool _familyLoaded = false;

	private bool _filterLoaded = false;

	private bool _viewLoaded = false;

	private object _lastClickedItem;

	internal TabControl tabRenameType;

	internal TextBox txtOldTemplate;

	internal TextBox txtNewTemplate;

	internal ListBox lstTemplateItems;

	internal ListBox lstLogTemplate;

	internal TextBlock txtSummaryTemplate;

	internal Button btnRunTemplate;

	internal CheckBox chkRenameFamily;

	internal CheckBox chkRenameType;

	internal TextBox txtOldFamily;

	internal TextBox txtNewFamily;

	internal TreeView tvFamilies;

	internal ListBox lstLogFamily;

	internal TextBlock txtSummaryFamily;

	internal Button btnRunFamily;

	internal TextBox txtOldFilter;

	internal TextBox txtNewFilter;

	internal ListBox lstFilterItems;

	internal ListBox lstLogFilter;

	internal TextBlock txtSummaryFilter;

	internal Button btnRunFilter;

	internal ComboBox cboSheetSet;

	internal ListBox lstSheets;

	internal CheckBox chkNumber;

	internal CheckBox chkName;

	internal TextBox txtFindSheet;

	internal TextBox txtReplaceSheet;

	internal TextBox txtOldView;

	internal TextBox txtNewView;

	internal ListBox lstViewItems;

	internal ListBox lstLogView;

	internal TextBlock txtSummaryView;

	internal Button btnRunView;

	private bool _contentLoaded;

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public RenameElementWindow(UIApplication uiapp)
	{
		InitializeComponent();
		Title = "BIM TOOL – Rename Element";
		_uiapp = uiapp;
		_doc = uiapp.ActiveUIDocument.Document;
		try
		{
			new WindowInteropHelper(this).Owner = uiapp.MainWindowHandle;
		}
		catch
		{
		}
		LoadTemplateList();
		InitializeSheetTab();
	}

	private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.Source == tabRenameType)
		{
			int index = tabRenameType.SelectedIndex;
			if (index == 0 && !_templateLoaded)
			{
				LoadTemplateList();
			}
			else if (index == 1 && !_familyLoaded)
			{
				LoadFamilyList();
			}
			else if (index == 2 && !_filterLoaded)
			{
				LoadFilterList();
			}
			else if (index == 4 && !_viewLoaded)
			{
				LoadViewList();
			}
		}
	}

	private void btnClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void LoadTemplateList()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<View> templates = (from View v in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(View))
			where v.IsTemplate
			orderby ((Element)v).Name
			select v).ToList();
		_templateItems = new ObservableCollection<RenameItem>(templates.Select((View v) => new RenameItem
		{
			DisplayName = ((Element)v).Name,
			ElementId = ((Element)v).Id,
			IsSelected = false
		}));
		lstTemplateItems.ItemsSource = _templateItems;
		_templateLoaded = true;
	}

	private void BtnSelectAllTemplate_Click(object sender, RoutedEventArgs e)
	{
		if (_templateItems == null)
		{
			return;
		}
		foreach (RenameItem i in _templateItems)
		{
			i.IsSelected = true;
		}
	}

	private void BtnSelectNoneTemplate_Click(object sender, RoutedEventArgs e)
	{
		if (_templateItems == null)
		{
			return;
		}
		foreach (RenameItem i in _templateItems)
		{
			i.IsSelected = false;
		}
	}

	private void LogTemplate(string s)
	{
		lstLogTemplate.Items.Add(s);
		lstLogTemplate.ScrollIntoView(s);
	}

	private void btnRunTemplate_Click(object sender, RoutedEventArgs e)
	{
		lstLogTemplate.Items.Clear();
		string oldTxt = txtOldTemplate.Text.Trim();
		string newTxt = txtNewTemplate.Text ?? "";
		if (string.IsNullOrEmpty(oldTxt))
		{
				MessageBox.Show("Vui lòng nhập cụm từ cần tìm!", "BIM TOOL - Notification");
			return;
		}
		HashSet<ElementId> selectedIds = (from x in _templateItems?.Where((RenameItem x) => x.IsSelected)
			select x.ElementId).ToHashSet();
		if (selectedIds == null || selectedIds.Count == 0)
		{
				MessageBox.Show("Vui lòng chọn ít nhất 1 View Template!", "BIM TOOL - Notification");
			return;
		}
		try
		{
			RenameResultData res = BatchRenameViewTemplates(_doc, oldTxt, newTxt, selectedIds, LogTemplate);
			txtSummaryTemplate.Text = $"Tổng: {res.TotalItems} | Khớp: {res.MatchedItems} | Đã đổi: {res.RenamedItems} | Trùng tên: {res.SkippedConflict}";
			if (res.RenamedItems > 0)
			{
				LogTemplate($">>> HOÀN THÀNH: Đã thay đổi {res.RenamedItems} View Templates.");
				_templateLoaded = false;
				LoadTemplateList();
			}
			else
			{
				LogTemplate(">>> Không có đối tượng nào được thay đổi.");
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message);
		}
	}

	public static RenameResultData BatchRenameViewTemplates(Document doc, string oldToken, string newToken, HashSet<ElementId> selectedIds, Action<string> log)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Expected O, but got Unknown
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Expected O, but got Unknown
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		RenameResultData res = new RenameResultData();
		List<View> templates = (from View v in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(View))
			where v.IsTemplate && selectedIds.Contains(((Element)v).Id)
			orderby ((Element)v).Name
			select v).ToList();
		res.TotalItems = templates.Count;
		HashSet<string> allNames = (from View v in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(View))
			where v.IsTemplate
			select v into t
			select ((Element)t).Name).ToHashSet();
		TransactionGroup tg = new TransactionGroup(doc, "Rename View Templates");
		try
		{
			tg.Start();
			Transaction t2 = new Transaction(doc, "Replace Name");
			try
			{
				t2.Start();
				foreach (View temp in templates)
				{
					string oldName = ((Element)temp).Name;
					if (!oldName.Contains(oldToken))
					{
						continue;
					}
					res.MatchedItems++;
					string newName = oldName.Replace(oldToken, newToken);
					if (oldName == newName)
					{
						res.SkippedSameName++;
						continue;
					}
					if (allNames.Contains(newName))
					{
						res.SkippedConflict++;
						log?.Invoke("[SKIP - Trùng] \"" + oldName + "\" -> \"" + newName + "\"");
						continue;
					}
					try
					{
						((Element)temp).Name = newName;
						allNames.Remove(oldName);
						allNames.Add(newName);
						res.RenamedItems++;
						log?.Invoke("[OK] \"" + oldName + "\" -> \"" + newName + "\"");
					}
					catch (Exception ex)
					{
						res.Failed++;
						log?.Invoke("[FAIL] \"" + oldName + "\" | " + ex.Message);
					}
				}
				t2.Commit();
			}
			finally
			{
				((IDisposable)t2)?.Dispose();
			}
			tg.Assimilate();
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return res;
	}

	private void LoadFamilyList()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<Family> families = (from Family f in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(Family))
			orderby ((Element)f).Name
			select f).ToList();
		_familyNodes = new ObservableCollection<FamilyTreeNode>();
		foreach (Family f2 in families)
		{
			FamilyTreeNode famNode = new FamilyTreeNode
			{
				Name = ((Element)f2).Name,
				Family = f2,
				Children = new ObservableCollection<FamilyTypeNode>()
			};
			ISet<ElementId> symbolIds = f2.GetFamilySymbolIds();
			foreach (ElementId id in symbolIds)
			{
				Element element = _doc.GetElement(id);
				FamilySymbol fs = (FamilySymbol)(object)((element is FamilySymbol) ? element : null);
				if (fs != null)
				{
					famNode.Children.Add(new FamilyTypeNode
					{
						Name = ((Element)fs).Name,
						Symbol = fs,
						Parent = famNode
					});
				}
			}
			List<FamilyTypeNode> sortedChildren = famNode.Children.OrderBy((FamilyTypeNode c) => c.Name).ToList();
			famNode.Children.Clear();
			foreach (FamilyTypeNode sc in sortedChildren)
			{
				famNode.Children.Add(sc);
			}
			_familyNodes.Add(famNode);
		}
		tvFamilies.ItemsSource = _familyNodes;
		_familyLoaded = true;
	}

	private void BtnSelectAllFamily_Click(object sender, RoutedEventArgs e)
	{
		if (_familyNodes == null)
		{
			return;
		}
		foreach (FamilyTreeNode node in _familyNodes)
		{
			node.IsChecked = true;
		}
	}

	private void BtnSelectNoneFamily_Click(object sender, RoutedEventArgs e)
	{
		if (_familyNodes == null)
		{
			return;
		}
		foreach (FamilyTreeNode node in _familyNodes)
		{
			node.IsChecked = false;
		}
	}

	private void LogFamily(string s)
	{
		lstLogFamily.Items.Add(s);
		lstLogFamily.ScrollIntoView(s);
	}

	private void btnRunFamily_Click(object sender, RoutedEventArgs e)
	{
		lstLogFamily.Items.Clear();
		txtSummaryFamily.Text = "";
		string oldToken = (txtOldFamily.Text ?? "").Trim();
		string newToken = (txtNewFamily.Text ?? "").Trim();
		if (string.IsNullOrEmpty(oldToken))
		{
				MessageBox.Show("Vui lòng nhập tên cũ.", "BIM TOOL", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		bool renameFamily = chkRenameFamily.IsChecked == true;
		bool renameType = chkRenameType.IsChecked == true;
		if (!renameFamily && !renameType)
		{
				MessageBox.Show("Vui lòng chọn đối tượng cần đổi tên (Family hoặc Type).", "BIM TOOL");
			return;
		}
		List<Family> selectedFamilies = new List<Family>();
		List<FamilySymbol> selectedTypes = new List<FamilySymbol>();
		if (_familyNodes != null)
		{
			foreach (FamilyTreeNode famNode in _familyNodes)
			{
				if (renameFamily && famNode.IsChecked != false)
				{
					selectedFamilies.Add(famNode.Family);
				}
				if (!renameType)
				{
					continue;
				}
				foreach (FamilyTypeNode typeNode in famNode.Children)
				{
					if (typeNode.IsChecked)
					{
						selectedTypes.Add(typeNode.Symbol);
					}
				}
			}
		}
		if (selectedFamilies.Count == 0 && selectedTypes.Count == 0)
		{
				MessageBox.Show("Vui lòng tích chọn ít nhất 1 Family hoặc Type!", "BIM TOOL - Notification");
			return;
		}
		try
		{
			RenameResultData result = BatchRenameFamiliesAndTypes(_doc, oldToken, newToken, selectedFamilies, selectedTypes, LogFamily);
			txtSummaryFamily.Text = $"Total: {result.TotalItems} | Matched: {result.MatchedItems} | " + $"Renamed: {result.RenamedItems} | Same: {result.SkippedSameName} | " + $"Conflict: {result.SkippedConflict} | Failed: {result.Failed}";
			if (result.RenamedItems > 0)
			{
				_familyLoaded = false;
				LoadFamilyList();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString(), "BIM - Error", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	public static RenameResultData BatchRenameFamiliesAndTypes(Document doc, string oldToken, string newToken, List<Family> families, List<FamilySymbol> types, Action<string> log)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_046b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0482: Unknown result type (might be due to invalid IL or missing references)
		RenameResultData res = new RenameResultData();
		res.TotalItems = families.Count + types.Count;
		HashSet<string> allFamilyNames = new HashSet<string>(from Family f in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(Family))
			select ((Element)f).Name, StringComparer.OrdinalIgnoreCase);
		TransactionGroup tg = new TransactionGroup(doc, "BIM - Rename Family and Types");
		try
		{
			tg.Start();
			Transaction t = new Transaction(doc, "Replace token");
			try
			{
				t.Start();
				foreach (Family fam in families)
				{
					string oldName = ((Element)fam).Name;
					if (!oldName.Contains(oldToken))
					{
						continue;
					}
					res.MatchedItems++;
					string newName = oldName.Replace(oldToken, newToken ?? string.Empty);
					if (oldName == newName)
					{
						res.SkippedSameName++;
						continue;
					}
					if (allFamilyNames.Contains(newName))
					{
						res.SkippedConflict++;
						log?.Invoke("[SKIP Family] \"" + oldName + "\" -> \"" + newName + "\" (tên đã tồn tại)");
						continue;
					}
					try
					{
						((Element)fam).Name = newName;
						allFamilyNames.Remove(oldName);
						allFamilyNames.Add(newName);
						res.RenamedItems++;
						log?.Invoke("[OK Family] \"" + oldName + "\" -> \"" + newName + "\"");
					}
					catch (Exception ex)
					{
						res.Failed++;
						log?.Invoke("[FAIL Family] \"" + oldName + "\": " + ex.Message);
					}
				}
				foreach (FamilySymbol sym in types)
				{
					string oldName2 = ((Element)sym).Name;
					if (!oldName2.Contains(oldToken))
					{
						continue;
					}
					res.MatchedItems++;
					string newName2 = oldName2.Replace(oldToken, newToken ?? string.Empty);
					if (oldName2 == newName2)
					{
						res.SkippedSameName++;
						continue;
					}
					Family parentFam = sym.Family;
					HashSet<string> existingTypeNames = new HashSet<string>(from n in parentFam.GetFamilySymbolIds().Select(delegate(ElementId id)
						{
							Element element = doc.GetElement(id);
							Element obj = ((element is FamilySymbol) ? element : null);
							return (obj != null) ? obj.Name : null;
						})
						where n != null
						select n, StringComparer.OrdinalIgnoreCase);
					if (existingTypeNames.Contains(newName2))
					{
						res.SkippedConflict++;
						log?.Invoke("[SKIP Type] \"" + oldName2 + "\" -> \"" + newName2 + "\" (tên type đã tồn tại trong Family)");
						continue;
					}
					try
					{
						((Element)sym).Name = newName2;
						res.RenamedItems++;
						log?.Invoke("[OK Type] \"" + oldName2 + "\" -> \"" + newName2 + "\"");
					}
					catch (Exception ex2)
					{
						res.Failed++;
						log?.Invoke("[FAIL Type] \"" + oldName2 + "\": " + ex2.Message);
					}
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			tg.Assimilate();
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return res;
	}

	private void LoadFilterList()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<ParameterFilterElement> filters = (from ParameterFilterElement f in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ParameterFilterElement))
			orderby ((Element)f).Name
			select f).ToList();
		_filterItems = new ObservableCollection<RenameItem>(filters.Select((ParameterFilterElement f) => new RenameItem
		{
			DisplayName = ((Element)f).Name,
			ElementId = ((Element)f).Id,
			IsSelected = false
		}));
		lstFilterItems.ItemsSource = _filterItems;
		_filterLoaded = true;
	}

	private void BtnSelectAllFilter_Click(object sender, RoutedEventArgs e)
	{
		if (_filterItems == null)
		{
			return;
		}
		foreach (RenameItem i in _filterItems)
		{
			i.IsSelected = true;
		}
	}

	private void BtnSelectNoneFilter_Click(object sender, RoutedEventArgs e)
	{
		if (_filterItems == null)
		{
			return;
		}
		foreach (RenameItem i in _filterItems)
		{
			i.IsSelected = false;
		}
	}

	private void LogFilter(string s)
	{
		lstLogFilter.Items.Add(s);
		lstLogFilter.ScrollIntoView(s);
	}

	private void btnRunFilter_Click(object sender, RoutedEventArgs e)
	{
		lstLogFilter.Items.Clear();
		string oldTxt = txtOldFilter.Text.Trim();
		string newTxt = txtNewFilter.Text ?? "";
		if (string.IsNullOrEmpty(oldTxt))
		{
				MessageBox.Show("Vui lòng nhập cụm từ cần tìm!", "BIM TOOL - Notification");
			return;
		}
		HashSet<ElementId> selectedIds = (from x in _filterItems?.Where((RenameItem x) => x.IsSelected)
			select x.ElementId).ToHashSet();
		if (selectedIds == null || selectedIds.Count == 0)
		{
				MessageBox.Show("Vui lòng chọn ít nhất 1 Filter!", "BIM TOOL - Notification");
			return;
		}
		try
		{
			RenameResultData res = BatchRenameFilters(_doc, oldTxt, newTxt, selectedIds, LogFilter);
			txtSummaryFilter.Text = $"Tổng: {res.TotalItems} | Khớp: {res.MatchedItems} | Đã đổi: {res.RenamedItems} | Trùng tên: {res.SkippedConflict}";
			if (res.RenamedItems > 0)
			{
				LogFilter($">>> HOÀN THÀNH: Đã thay đổi {res.RenamedItems} Filters.");
				_filterLoaded = false;
				LoadFilterList();
			}
			else
			{
				LogFilter(">>> Không có Filter nào được thay đổi.");
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message);
		}
	}

	public static RenameResultData BatchRenameFilters(Document doc, string oldToken, string newToken, HashSet<ElementId> selectedIds, Action<string> log)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Expected O, but got Unknown
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Unknown result type (might be due to invalid IL or missing references)
		RenameResultData res = new RenameResultData();
		List<ParameterFilterElement> filters = (from ParameterFilterElement f in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement))
			where selectedIds.Contains(((Element)f).Id)
			orderby ((Element)f).Name
			select f).ToList();
		res.TotalItems = filters.Count;
		HashSet<string> allNames = (from ParameterFilterElement f in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement))
			select ((Element)f).Name).ToHashSet();
		TransactionGroup tg = new TransactionGroup(doc, "Rename Filters");
		try
		{
			tg.Start();
			Transaction t = new Transaction(doc, "Replace Filter Name");
			try
			{
				t.Start();
				foreach (ParameterFilterElement filter in filters)
				{
					string oldName = ((Element)filter).Name;
					if (!oldName.Contains(oldToken))
					{
						continue;
					}
					res.MatchedItems++;
					string newName = oldName.Replace(oldToken, newToken);
					if (oldName == newName)
					{
						res.SkippedSameName++;
						continue;
					}
					if (allNames.Contains(newName))
					{
						res.SkippedConflict++;
						log?.Invoke("[SKIP - Trùng] \"" + oldName + "\" -> \"" + newName + "\"");
						continue;
					}
					try
					{
						((Element)filter).Name = newName;
						allNames.Remove(oldName);
						allNames.Add(newName);
						res.RenamedItems++;
						log?.Invoke("[OK] \"" + oldName + "\" -> \"" + newName + "\"");
					}
					catch (Exception ex)
					{
						res.Failed++;
						log?.Invoke("[FAIL] \"" + oldName + "\" | " + ex.Message);
					}
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			tg.Assimilate();
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return res;
	}

	private void InitializeSheetTab()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		_allSheets = (from ViewSheet s in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet))
			orderby s.SheetNumber
			select s).ToList();
		_sheetSets = ((IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ViewSheetSet))).Cast<ViewSheetSet>().ToList();
		List<object> comboSource = new List<object>
		{
			new
			{
				Name = "--- All Sheets ---"
			}
		};
		if (_sheetSets != null)
		{
			comboSource.AddRange(_sheetSets);
		}
		cboSheetSet.ItemsSource = comboSource;
		cboSheetSet.SelectedIndex = 0;
		RefreshSheetList(_allSheets);
	}

	private void RefreshSheetList(List<ViewSheet> sheetsToDisplay)
	{
		_displayItems = new ObservableCollection<SheetItem>(sheetsToDisplay.Select((ViewSheet s) => new SheetItem
		{
			DisplayName = s.SheetNumber + " - " + ((Element)s).Name,
			Sheet = s,
			IsSelected = false
		}));
		lstSheets.ItemsSource = _displayItems;
	}

	private void CboSheetSet_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (cboSheetSet.SelectedIndex == 0)
		{
			RefreshSheetList(_allSheets);
			return;
		}
		object selectedItem = cboSheetSet.SelectedItem;
		ViewSheetSet selectedSet = (ViewSheetSet)((selectedItem is ViewSheetSet) ? selectedItem : null);
		if (selectedSet != null)
		{
			List<ElementId> setSheetIds = (from View v in (IEnumerable)selectedSet.Views
				select ((Element)v).Id).ToList();
			RefreshSheetList(_allSheets.Where((ViewSheet s) => setSheetIds.Contains(((Element)s).Id)).ToList());
		}
	}

	private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
	{
		foreach (SheetItem i in _displayItems)
		{
			i.IsSelected = true;
		}
	}

	private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
	{
		foreach (SheetItem i in _displayItems)
		{
			i.IsSelected = false;
		}
	}

	private void BtnRunSheet_Click(object sender, RoutedEventArgs e)
	{
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Expected O, but got Unknown
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Expected O, but got Unknown
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0208: Unknown result type (might be due to invalid IL or missing references)
		//IL_0317: Unknown result type (might be due to invalid IL or missing references)
		string find = txtFindSheet.Text;
		string replace = txtReplaceSheet.Text;
		List<ViewSheet> selectedSheets = (from x in _displayItems
			where x.IsSelected
			select x.Sheet).ToList();
		bool isRenameNumber = chkNumber.IsChecked == true;
		bool isRenameName = chkName.IsChecked == true;
		if (string.IsNullOrEmpty(find) || !selectedSheets.Any())
		{
				MessageBox.Show("Vui lòng nhập từ cần tìm và chọn ít nhất 1 sheet.", "BIM TOOL");
			return;
		}
		try
		{
			int count = 0;
			Transaction t = new Transaction(_doc, "Rename Number and Name");
			try
			{
				t.Start();
				foreach (ViewSheet sheet in selectedSheets)
				{
					bool changed = false;
					if (isRenameNumber && sheet.SheetNumber.Contains(find))
					{
						try
						{
							sheet.SheetNumber = sheet.SheetNumber.Replace(find, replace);
							changed = true;
						}
						catch
						{
						}
					}
					if (isRenameName && ((Element)sheet).Name.Contains(find))
					{
						try
						{
							((Element)sheet).Name = ((Element)sheet).Name.Replace(find, replace);
							changed = true;
						}
						catch
						{
						}
					}
					if (changed)
					{
						count++;
					}
				}
				_doc.Regenerate();
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			Transaction tRefresh = new Transaction(_doc, "Refresh Browser");
			try
			{
				tRefresh.Start();
				tRefresh.Commit();
			}
			finally
			{
				((IDisposable)tRefresh)?.Dispose();
			}
			_allSheets = (from ViewSheet s in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet))
				orderby s.SheetNumber
				select s).ToList();
			if (cboSheetSet.SelectedIndex == 0)
			{
				RefreshSheetList(_allSheets);
			}
			else
			{
				object selectedItem = cboSheetSet.SelectedItem;
				ViewSheetSet selectedSet = (ViewSheetSet)((selectedItem is ViewSheetSet) ? selectedItem : null);
				if (selectedSet != null)
				{
					List<ElementId> setSheetIds = (from View v in (IEnumerable)selectedSet.Views
						select ((Element)v).Id).ToList();
					RefreshSheetList(_allSheets.Where((ViewSheet s) => setSheetIds.Contains(((Element)s).Id)).ToList());
				}
			}
			TaskDialog.Show("Revit Tool", $"Đã đổi tên thành công {count} sheet(s).");
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message, "BIM - Error");
		}
	}

	private void LoadViewList()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<View> views = (from View v in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(View))
			where !v.IsTemplate && (int)v.ViewType != 6 && (int)v.ViewType != 7 && (int)v.ViewType != 12 && (int)v.ViewType != 214
			orderby ((Element)v).Name
			select v).ToList();
		_viewItems = new ObservableCollection<RenameItem>(views.Select((View v) => new RenameItem
		{
			DisplayName = ((Element)v).Name,
			ElementId = ((Element)v).Id,
			IsSelected = false
		}));
		lstViewItems.ItemsSource = _viewItems;
		_viewLoaded = true;
	}

	private void BtnSelectAllView_Click(object sender, RoutedEventArgs e)
	{
		if (_viewItems == null)
		{
			return;
		}
		foreach (RenameItem i in _viewItems)
		{
			i.IsSelected = true;
		}
	}

	private void BtnSelectNoneView_Click(object sender, RoutedEventArgs e)
	{
		if (_viewItems == null)
		{
			return;
		}
		foreach (RenameItem i in _viewItems)
		{
			i.IsSelected = false;
		}
	}

	private void LogView(string s)
	{
		lstLogView.Items.Add(s);
		lstLogView.ScrollIntoView(s);
	}

	private void btnRunView_Click(object sender, RoutedEventArgs e)
	{
		lstLogView.Items.Clear();
		string oldTxt = txtOldView.Text.Trim();
		string newTxt = txtNewView.Text ?? "";
		if (string.IsNullOrEmpty(oldTxt))
		{
				MessageBox.Show("Vui lòng nhập cụm từ cần tìm!", "BIM TOOL - Notification");
			return;
		}
		HashSet<ElementId> selectedIds = (from x in _viewItems?.Where((RenameItem x) => x.IsSelected)
			select x.ElementId).ToHashSet();
		if (selectedIds == null || selectedIds.Count == 0)
		{
				MessageBox.Show("Vui lòng chọn ít nhất 1 View!", "BIM TOOL - Notification");
			return;
		}
		try
		{
			RenameResultData res = BatchRenameViews(_doc, oldTxt, newTxt, selectedIds, LogView);
			txtSummaryView.Text = $"Tổng: {res.TotalItems} | Khớp: {res.MatchedItems} | Đã đổi: {res.RenamedItems} | Trùng tên: {res.SkippedConflict}";
			if (res.RenamedItems > 0)
			{
				LogView($">>> HOÀN THÀNH: Đã thay đổi {res.RenamedItems} Views.");
				_viewLoaded = false;
				LoadViewList();
			}
			else
			{
				LogView(">>> Không có View nào được thay đổi.");
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.Message);
		}
	}

	public static RenameResultData BatchRenameViews(Document doc, string oldToken, string newToken, HashSet<ElementId> selectedIds, Action<string> log)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Expected O, but got Unknown
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Expected O, but got Unknown
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Unknown result type (might be due to invalid IL or missing references)
		RenameResultData res = new RenameResultData();
		List<View> views = (from View v in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(View))
			where !v.IsTemplate && selectedIds.Contains(((Element)v).Id)
			orderby ((Element)v).Name
			select v).ToList();
		res.TotalItems = views.Count;
		HashSet<string> allNames = (from View v in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(View))
			where !v.IsTemplate
			select ((Element)v).Name).ToHashSet();
		TransactionGroup tg = new TransactionGroup(doc, "Rename Views");
		try
		{
			tg.Start();
			Transaction t = new Transaction(doc, "Replace View Name");
			try
			{
				t.Start();
				foreach (View view in views)
				{
					string oldName = ((Element)view).Name;
					if (!oldName.Contains(oldToken))
					{
						continue;
					}
					res.MatchedItems++;
					string newName = oldName.Replace(oldToken, newToken);
					if (oldName == newName)
					{
						res.SkippedSameName++;
						continue;
					}
					if (allNames.Contains(newName))
					{
						res.SkippedConflict++;
						log?.Invoke("[SKIP - Trùng] \"" + oldName + "\" -> \"" + newName + "\"");
						continue;
					}
					try
					{
						((Element)view).Name = newName;
						allNames.Remove(oldName);
						allNames.Add(newName);
						res.RenamedItems++;
						log?.Invoke("[OK] \"" + oldName + "\" -> \"" + newName + "\"");
					}
					catch (Exception ex)
					{
						res.Failed++;
						log?.Invoke("[FAIL] \"" + oldName + "\" | " + ex.Message);
					}
				}
				t.Commit();
			}
			finally
			{
				((IDisposable)t)?.Dispose();
			}
			tg.Assimilate();
		}
		finally
		{
			((IDisposable)tg)?.Dispose();
		}
		return res;
	}

	private void CheckBox_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is CheckBox { DataContext: { } currentItem, IsChecked: var isChecked }))
		{
			return;
		}
		bool isChecked2 = isChecked == true;
		if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && _lastClickedItem != null)
		{
			if (currentItem is RenameItem && _lastClickedItem is RenameItem)
			{
				IList<RenameItem> list = null;
				if (_templateItems != null && _templateItems.Contains((RenameItem)currentItem))
				{
					list = _templateItems;
				}
				else if (_filterItems != null && _filterItems.Contains((RenameItem)currentItem))
				{
					list = _filterItems;
				}
				else if (_viewItems != null && _viewItems.Contains((RenameItem)currentItem))
				{
					list = _viewItems;
				}
				if (list != null && list.Contains((RenameItem)_lastClickedItem))
				{
					int start = list.IndexOf((RenameItem)_lastClickedItem);
					int end = list.IndexOf((RenameItem)currentItem);
					int min = Math.Min(start, end);
					int max = Math.Max(start, end);
					for (int i = min; i <= max; i++)
					{
						list[i].IsSelected = isChecked2;
					}
				}
			}
			else if (currentItem is SheetItem && _lastClickedItem is SheetItem)
			{
				if (_displayItems != null && _displayItems.Contains((SheetItem)currentItem) && _displayItems.Contains((SheetItem)_lastClickedItem))
				{
					int start2 = _displayItems.IndexOf((SheetItem)_lastClickedItem);
					int end2 = _displayItems.IndexOf((SheetItem)currentItem);
					int min2 = Math.Min(start2, end2);
					int max2 = Math.Max(start2, end2);
					for (int j = min2; j <= max2; j++)
					{
						_displayItems[j].IsSelected = isChecked2;
					}
				}
			}
			else if (currentItem is FamilyTypeNode && _lastClickedItem is FamilyTypeNode)
			{
				FamilyTypeNode type1 = (FamilyTypeNode)currentItem;
				FamilyTypeNode type2 = (FamilyTypeNode)_lastClickedItem;
				if (type1.Parent == type2.Parent)
				{
					FamilyTreeNode parent = type1.Parent;
					int start3 = parent.Children.IndexOf(type2);
					int end3 = parent.Children.IndexOf(type1);
					int min3 = Math.Min(start3, end3);
					int max3 = Math.Max(start3, end3);
					for (int k = min3; k <= max3; k++)
					{
						parent.Children[k].IsChecked = isChecked2;
					}
				}
			}
			else if (currentItem is FamilyTreeNode && _lastClickedItem is FamilyTreeNode && _familyNodes != null && _familyNodes.Contains((FamilyTreeNode)currentItem) && _familyNodes.Contains((FamilyTreeNode)_lastClickedItem))
			{
				int start4 = _familyNodes.IndexOf((FamilyTreeNode)_lastClickedItem);
				int end4 = _familyNodes.IndexOf((FamilyTreeNode)currentItem);
				int min4 = Math.Min(start4, end4);
				int max4 = Math.Max(start4, end4);
				for (int l = min4; l <= max4; l++)
				{
					_familyNodes[l].IsChecked = isChecked2;
				}
			}
		}
		_lastClickedItem = currentItem;
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/31.%20rename%20element/renameelementwindow.xaml", UriKind.Relative);
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
			tabRenameType = (TabControl)target;
			tabRenameType.SelectionChanged += TabControl_SelectionChanged;
			break;
		case 2:
			txtOldTemplate = (TextBox)target;
			break;
		case 3:
			txtNewTemplate = (TextBox)target;
			break;
		case 4:
			lstTemplateItems = (ListBox)target;
			break;
		case 6:
			((Button)target).Click += BtnSelectAllTemplate_Click;
			break;
		case 7:
			((Button)target).Click += BtnSelectNoneTemplate_Click;
			break;
		case 8:
			lstLogTemplate = (ListBox)target;
			break;
		case 9:
			txtSummaryTemplate = (TextBlock)target;
			break;
		case 10:
			btnRunTemplate = (Button)target;
			btnRunTemplate.Click += btnRunTemplate_Click;
			break;
		case 11:
			((Button)target).Click += btnClose_Click;
			break;
		case 12:
			chkRenameFamily = (CheckBox)target;
			break;
		case 13:
			chkRenameType = (CheckBox)target;
			break;
		case 14:
			txtOldFamily = (TextBox)target;
			break;
		case 15:
			txtNewFamily = (TextBox)target;
			break;
		case 16:
			tvFamilies = (TreeView)target;
			break;
		case 19:
			((Button)target).Click += BtnSelectAllFamily_Click;
			break;
		case 20:
			((Button)target).Click += BtnSelectNoneFamily_Click;
			break;
		case 21:
			lstLogFamily = (ListBox)target;
			break;
		case 22:
			txtSummaryFamily = (TextBlock)target;
			break;
		case 23:
			btnRunFamily = (Button)target;
			btnRunFamily.Click += btnRunFamily_Click;
			break;
		case 24:
			((Button)target).Click += btnClose_Click;
			break;
		case 25:
			txtOldFilter = (TextBox)target;
			break;
		case 26:
			txtNewFilter = (TextBox)target;
			break;
		case 27:
			lstFilterItems = (ListBox)target;
			break;
		case 29:
			((Button)target).Click += BtnSelectAllFilter_Click;
			break;
		case 30:
			((Button)target).Click += BtnSelectNoneFilter_Click;
			break;
		case 31:
			lstLogFilter = (ListBox)target;
			break;
		case 32:
			txtSummaryFilter = (TextBlock)target;
			break;
		case 33:
			btnRunFilter = (Button)target;
			btnRunFilter.Click += btnRunFilter_Click;
			break;
		case 34:
			((Button)target).Click += btnClose_Click;
			break;
		case 35:
			cboSheetSet = (ComboBox)target;
			cboSheetSet.SelectionChanged += CboSheetSet_SelectionChanged;
			break;
		case 36:
			lstSheets = (ListBox)target;
			break;
		case 38:
			((Button)target).Click += BtnSelectAll_Click;
			break;
		case 39:
			((Button)target).Click += BtnSelectNone_Click;
			break;
		case 40:
			chkNumber = (CheckBox)target;
			break;
		case 41:
			chkName = (CheckBox)target;
			break;
		case 42:
			txtFindSheet = (TextBox)target;
			break;
		case 43:
			txtReplaceSheet = (TextBox)target;
			break;
		case 44:
			((Button)target).Click += BtnRunSheet_Click;
			break;
		case 45:
			((Button)target).Click += btnClose_Click;
			break;
		case 46:
			txtOldView = (TextBox)target;
			break;
		case 47:
			txtNewView = (TextBox)target;
			break;
		case 48:
			lstViewItems = (ListBox)target;
			break;
		case 50:
			((Button)target).Click += BtnSelectAllView_Click;
			break;
		case 51:
			((Button)target).Click += BtnSelectNoneView_Click;
			break;
		case 52:
			lstLogView = (ListBox)target;
			break;
		case 53:
			txtSummaryView = (TextBlock)target;
			break;
		case 54:
			btnRunView = (Button)target;
			btnRunView.Click += btnRunView_Click;
			break;
		case 55:
			((Button)target).Click += btnClose_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IStyleConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 5:
			((CheckBox)target).Click += CheckBox_Click;
			break;
		case 17:
			((CheckBox)target).Click += CheckBox_Click;
			break;
		case 18:
			((CheckBox)target).Click += CheckBox_Click;
			break;
		case 28:
			((CheckBox)target).Click += CheckBox_Click;
			break;
		case 37:
			((CheckBox)target).Click += CheckBox_Click;
			break;
		case 49:
			((CheckBox)target).Click += CheckBox_Click;
			break;
		}
	}
}
