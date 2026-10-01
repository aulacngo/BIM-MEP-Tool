using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using Grid = System.Windows.Controls.Grid;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public class CopyFilterWindow : Window, IComponentConnector
{
	private UIApplication _uiapp;

	private CopyFilterHandler _handler;

	private ExternalEvent _externalEvent;

	private List<CopyFilterViewItem> _allSourceViews;

	private List<CopyFilterViewItem> _allTargetViews;

	internal ComboBox cbSourceViewType;

	internal ListBox lbSourceViews;

	internal TextBlock txtSourceFilterCount;

	internal CheckBox chkActiveView;

	internal Grid gridTargetControls;

	internal TextBox txtSearchTarget;

	internal TextBlock lblTargetList;

	internal ListBox lbTargetViews;

	internal TextBlock txtStatus;

	internal Button btnCopy;

	internal Button btnClose;

	private bool _contentLoaded;

	public CopyFilterWindow(UIApplication uiapp, CopyFilterHandler handler, ExternalEvent externalEvent)
	{
		InitializeComponent();
		_uiapp = uiapp;
		_handler = handler;
		_externalEvent = externalEvent;
		LoadViews();
	}

	private void LoadViews()
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Document doc = _uiapp.ActiveUIDocument.Document;
			View activeView = doc.ActiveView;
			List<View> allViews = (from View v in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(View))
				where !v.IsTemplate
				orderby ((Element)v).Name
				select v).ToList();
			List<View> validViews = new List<View>();
			foreach (View v2 in allViews)
			{
				try
				{
					ICollection<ElementId> tempFilters = v2.GetFilters();
					validViews.Add(v2);
				}
				catch
				{
				}
			}
			_allSourceViews = validViews.Select(delegate(View v)
			{
				//IL_0021: Unknown result type (might be due to invalid IL or missing references)
				//IL_0026: Unknown result type (might be due to invalid IL or missing references)
				CopyFilterViewItem obj2 = new CopyFilterViewItem
				{
					Name = ((Element)v).Name,
					Id = ((Element)v).Id
				};
				ViewType viewType = v.ViewType;
				obj2.ViewTypeStr = viewType.ToString();
				return obj2;
			}).ToList();
			_allTargetViews = validViews.Select(delegate(View v)
			{
				CopyFilterViewItem obj3 = new CopyFilterViewItem
				{
					Name = ((Element)v).Name,
					Id = ((Element)v).Id
				};
				ViewType viewType2 = v.ViewType;
				obj3.ViewTypeStr = viewType2.ToString();
				obj3.IsChecked = false;
				return obj3;
			}).ToList();
			List<string> sourceViewTypes = (from t in _allSourceViews.Select((CopyFilterViewItem v) => v.ViewTypeStr).Distinct()
				orderby t
				select t).ToList();
			List<string> cbSourceTypes = new List<string> { "<Tất cả>" };
			cbSourceTypes.AddRange(sourceViewTypes);
			cbSourceViewType.ItemsSource = cbSourceTypes;
			cbSourceViewType.SelectedIndex = 0;
			if (activeView != null)
			{
				chkActiveView.Content = "Áp dụng cho Active View (View hiện hành: " + ((Element)activeView).Name + ")";
			}
			else
			{
				chkActiveView.IsEnabled = false;
				chkActiveView.IsChecked = false;
				chkActiveView.Content = "Áp dụng cho Active View (Không xác định)";
			}
			FilterSourceViews();
			FilterTargetViews();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi khi tải danh sách View: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void FilterSourceViews()
	{
		if (_allSourceViews != null)
		{
			string selectedType = cbSourceViewType.SelectedItem as string;
			List<CopyFilterViewItem> filtered = ((!string.IsNullOrEmpty(selectedType) && !(selectedType == "<Tất cả>")) ? _allSourceViews.Where((CopyFilterViewItem v) => v.ViewTypeStr == selectedType).ToList() : _allSourceViews);
			lbSourceViews.ItemsSource = null;
			lbSourceViews.ItemsSource = filtered;
		}
	}

	private void FilterTargetViews()
	{
		if (_allTargetViews == null)
		{
			return;
		}
		CopyFilterViewItem selectedSource = lbSourceViews.SelectedItem as CopyFilterViewItem;
		ElementId sourceId = ((selectedSource != null) ? selectedSource.Id : ElementId.InvalidElementId);
		string searchText = txtSearchTarget.Text.Trim();
		bool hasSearch = !string.IsNullOrEmpty(searchText) && searchText != "Tìm kiếm view...";
		IEnumerable<CopyFilterViewItem> filtered = _allTargetViews.Where((CopyFilterViewItem v) => v.Id != sourceId);
		if (hasSearch)
		{
			filtered = filtered.Where((CopyFilterViewItem v) => v.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0);
		}
		lbTargetViews.ItemsSource = null;
		lbTargetViews.ItemsSource = filtered.ToList();
	}

	private void UpdateSourceFilterCount()
	{
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		CopyFilterViewItem selectedSource = lbSourceViews.SelectedItem as CopyFilterViewItem;
		if (selectedSource == null)
		{
			txtSourceFilterCount.Text = "Số lượng Filter: 0";
			return;
		}
		_handler.Action = delegate(UIApplication uiapp)
		{
			try
			{
				Document document = uiapp.ActiveUIDocument.Document;
				Element element = document.GetElement(selectedSource.Id);
				View val = (View)(object)((element is View) ? element : null);
				if (val != null)
				{
					ICollection<ElementId> filters = val.GetFilters();
					base.Dispatcher.Invoke(delegate
					{
						txtSourceFilterCount.Text = $"Số lượng Filter: {filters.Count}";
					});
				}
			}
			catch
			{
			}
		};
		_externalEvent.Raise();
	}

	private void CbSourceViewType_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		FilterSourceViews();
	}

	private void LbSourceViews_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		FilterTargetViews();
		UpdateSourceFilterCount();
	}

	private void ChkActiveView_Checked(object sender, RoutedEventArgs e)
	{
		if (gridTargetControls != null)
		{
			gridTargetControls.IsEnabled = false;
		}
		if (lblTargetList != null)
		{
			lblTargetList.Opacity = 0.5;
		}
		if (lbTargetViews != null)
		{
			lbTargetViews.IsEnabled = false;
		}
	}

	private void ChkActiveView_Unchecked(object sender, RoutedEventArgs e)
	{
		if (gridTargetControls != null)
		{
			gridTargetControls.IsEnabled = true;
		}
		if (lblTargetList != null)
		{
			lblTargetList.Opacity = 1.0;
		}
		if (lbTargetViews != null)
		{
			lbTargetViews.IsEnabled = true;
		}
	}

	private void TxtSearchTarget_TextChanged(object sender, TextChangedEventArgs e)
	{
		FilterTargetViews();
	}

	private void TxtSearchTarget_GotFocus(object sender, RoutedEventArgs e)
	{
		if (txtSearchTarget.Text == "Tìm kiếm view...")
		{
			txtSearchTarget.Text = "";
			txtSearchTarget.Foreground = Brushes.Black;
		}
	}

	private void TxtSearchTarget_LostFocus(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(txtSearchTarget.Text))
		{
			txtSearchTarget.Text = "Tìm kiếm view...";
			txtSearchTarget.Foreground = Brushes.Gray;
		}
	}

	private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
	{
		if (!(lbTargetViews.ItemsSource is List<CopyFilterViewItem> visibleTargets))
		{
			return;
		}
		foreach (CopyFilterViewItem item in visibleTargets)
		{
			item.IsChecked = true;
		}
	}

	private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
	{
		if (!(lbTargetViews.ItemsSource is List<CopyFilterViewItem> visibleTargets))
		{
			return;
		}
		foreach (CopyFilterViewItem item in visibleTargets)
		{
			item.IsChecked = false;
		}
	}

	private void BtnClose_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void BtnCopy_Click(object sender, RoutedEventArgs e)
	{
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		CopyFilterViewItem selectedSource = lbSourceViews.SelectedItem as CopyFilterViewItem;
		if (selectedSource == null)
		{
			MessageBox.Show("Vui lòng chọn một View nguồn để sao chép.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		bool useActiveView = chkActiveView.IsChecked == true;
		List<ElementId> targetViewIds = new List<ElementId>();
		if (useActiveView)
		{
			View activeView = _uiapp.ActiveUIDocument.Document.ActiveView;
			if (activeView != null)
			{
				if (((Element)activeView).Id == selectedSource.Id)
				{
					MessageBox.Show("View hiện hành trùng với View nguồn. Vui lòng chọn View hiện hành khác hoặc chọn View nguồn khác.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}
				targetViewIds.Add(((Element)activeView).Id);
			}
		}
		else if (_allTargetViews != null)
		{
			targetViewIds.AddRange(from v in _allTargetViews
				where v.IsChecked && v.Id != selectedSource.Id
				select v.Id);
		}
		if (targetViewIds.Count == 0)
		{
			MessageBox.Show("Vui lòng chọn ít nhất một View đích hoặc chọn Áp dụng cho Active View.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		txtStatus.Text = "Đang sao chép...";
		btnCopy.IsEnabled = false;
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
			//IL_00bc: Expected O, but got Unknown
			//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
			//IL_021a: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Document document = uiapp.ActiveUIDocument.Document;
				Element element = document.GetElement(selectedSource.Id);
				View val = (View)(object)((element is View) ? element : null);
				if (val != null)
				{
					ICollection<ElementId> filters = val.GetFilters();
					if (filters == null || filters.Count == 0)
					{
						base.Dispatcher.Invoke(delegate
						{
							MessageBox.Show("View nguồn không chứa bất kỳ bộ lọc (View Filter) nào để sao chép.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Exclamation);
							txtStatus.Text = "Sao chép thất bại.";
							btnCopy.IsEnabled = true;
						});
					}
					else
					{
						int copiedFiltersCount = filters.Count;
						int successViewsCount = 0;
						int redirectedToTemplateCount = 0;
						Transaction val2 = new Transaction(document, "Copy View Filters");
						try
						{
							val2.Start();
							foreach (ElementId current in targetViewIds)
							{
								Element element2 = document.GetElement(current);
								View val3 = (View)(object)((element2 is View) ? element2 : null);
								if (val3 != null)
								{
									View val4 = val3;
									bool flag = false;
									if (val3.ViewTemplateId != ElementId.InvalidElementId)
									{
										Element element3 = document.GetElement(val3.ViewTemplateId);
										val4 = (View)(object)((element3 is View) ? element3 : null);
										if (val4 == null)
										{
											continue;
										}
										flag = true;
									}
									foreach (ElementId current2 in filters)
									{
										try
										{
											if (!val4.GetFilters().Contains(current2))
											{
												val4.AddFilter(current2);
											}
											bool filterVisibility = val.GetFilterVisibility(current2);
											val4.SetFilterVisibility(current2, filterVisibility);
											OverrideGraphicSettings filterOverrides = val.GetFilterOverrides(current2);
											val4.SetFilterOverrides(current2, filterOverrides);
										}
										catch
										{
										}
									}
									if (flag)
									{
										redirectedToTemplateCount++;
									}
									successViewsCount++;
								}
							}
							val2.Commit();
						}
						finally
						{
							((IDisposable)val2)?.Dispose();
						}
						base.Dispatcher.Invoke(delegate
						{
							string text = $"Sao chép thành công {copiedFiltersCount} bộ lọc sang {successViewsCount} View đích.";
							if (redirectedToTemplateCount > 0)
							{
								text += $"\n(Trong đó có {redirectedToTemplateCount} View áp dụng qua View Template thành công)";
							}
							MessageBox.Show(text, "Thành công", MessageBoxButton.OK, MessageBoxImage.Asterisk);
							txtStatus.Text = "Sao chép thành công!";
							btnCopy.IsEnabled = true;
						});
					}
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				base.Dispatcher.Invoke(delegate
				{
					MessageBox.Show("Lỗi trong quá trình sao chép: " + ex3.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
					txtStatus.Text = "Gặp lỗi khi sao chép.";
					btnCopy.IsEnabled = true;
				});
			}
		};
		_externalEvent.Raise();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/60.%20copy%20filter/copyfilterwindow.xaml", UriKind.Relative);
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
			cbSourceViewType = (ComboBox)target;
			cbSourceViewType.SelectionChanged += CbSourceViewType_SelectionChanged;
			break;
		case 2:
			lbSourceViews = (ListBox)target;
			lbSourceViews.SelectionChanged += LbSourceViews_SelectionChanged;
			break;
		case 3:
			txtSourceFilterCount = (TextBlock)target;
			break;
		case 4:
			chkActiveView = (CheckBox)target;
			chkActiveView.Checked += ChkActiveView_Checked;
			chkActiveView.Unchecked += ChkActiveView_Unchecked;
			break;
		case 5:
			gridTargetControls = (Grid)target;
			break;
		case 6:
			txtSearchTarget = (TextBox)target;
			txtSearchTarget.TextChanged += TxtSearchTarget_TextChanged;
			txtSearchTarget.GotFocus += TxtSearchTarget_GotFocus;
			txtSearchTarget.LostFocus += TxtSearchTarget_LostFocus;
			break;
		case 7:
			((Button)target).Click += BtnSelectAll_Click;
			break;
		case 8:
			((Button)target).Click += BtnDeselectAll_Click;
			break;
		case 9:
			lblTargetList = (TextBlock)target;
			break;
		case 10:
			lbTargetViews = (ListBox)target;
			break;
		case 11:
			txtStatus = (TextBlock)target;
			break;
		case 12:
			btnCopy = (Button)target;
			btnCopy.Click += BtnCopy_Click;
			break;
		case 13:
			btnClose = (Button)target;
			btnClose.Click += BtnClose_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
