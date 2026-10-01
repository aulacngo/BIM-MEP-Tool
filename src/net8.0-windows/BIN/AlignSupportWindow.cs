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
using System.Windows.Markup;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public class AlignSupportWindow : Window, IComponentConnector
{
	private readonly UIApplication _uiapp;

	private readonly AlignSupportHandler _handler;

	private readonly ExternalEvent _exEvent;

	internal RadioButton rbActiveView;

	internal RadioButton rbSelection;

	internal ItemsControl icSupportTypes;

	internal TextBlock txtStatus;

	internal TextBlock txtSuccess;

	internal TextBlock txtSkip;

	internal TextBlock txtLogCount;

	internal DataGrid dgLog;

	internal Button btnRun;

	internal Button btnClear;

	internal Button btnClose;

	private bool _contentLoaded;

	public ObservableCollection<SupportTypeItem> SupportTypes { get; } = new ObservableCollection<SupportTypeItem>();

	public ObservableCollection<SupportLogItem> LogItems { get; } = new ObservableCollection<SupportLogItem>();

	public AlignSupportWindow(UIApplication uiapp, AlignSupportHandler handler, ExternalEvent exEvent)
	{
		InitializeComponent();
		_uiapp = uiapp;
		_handler = handler;
		_exEvent = exEvent;
		icSupportTypes.ItemsSource = SupportTypes;
		dgLog.ItemsSource = LogItems;
		LoadSupportTypes();
	}

	private void LoadSupportTypes()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		SupportTypes.Clear();
		try
		{
			Document doc = _uiapp.ActiveUIDocument.Document;
			List<FamilySymbol> symbols = (from FamilySymbol s in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))
				where ((Element)s).Name.IndexOf("Support", StringComparison.OrdinalIgnoreCase) >= 0 || (s.Family != null && ((Element)s.Family).Name.IndexOf("Support", StringComparison.OrdinalIgnoreCase) >= 0)
				select s).ToList();
			List<SupportTypeItem> items = new List<SupportTypeItem>();
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (FamilySymbol sym in symbols)
			{
				Family family = sym.Family;
				string familyName = ((family != null) ? ((Element)family).Name : null) ?? "";
				string displayName = (string.IsNullOrEmpty(familyName) ? ((Element)sym).Name : (familyName + ": " + ((Element)sym).Name));
				if (seen.Add(displayName))
				{
					items.Add(new SupportTypeItem
					{
						TypeName = displayName,
						SymbolId = ((Element)sym).Id,
						IsSelected = true
					});
				}
			}
			foreach (SupportTypeItem item in items.OrderBy((SupportTypeItem i) => i.TypeName))
			{
				SupportTypes.Add(item);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi khi tải danh sách support: " + ex.Message, "Align Support", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
	{
		foreach (SupportTypeItem item in SupportTypes)
		{
			item.IsSelected = true;
		}
	}

	private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
	{
		foreach (SupportTypeItem item in SupportTypes)
		{
			item.IsSelected = false;
		}
	}

	private void BtnRun_Click(object sender, RoutedEventArgs e)
	{
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		List<ElementId> selectedIds = (from t in SupportTypes
			where t.IsSelected
			select t.SymbolId).ToList();
		if (selectedIds.Count == 0)
		{
			MessageBox.Show("Vui lòng chọn ít nhất một loại Support.", "Align Support", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		bool useSelection = rbSelection.IsChecked == true;
		txtStatus.Text = (useSelection ? "\ud83d\uddb1\ufe0f Quét chọn vùng chứa Support cần căn chỉnh..." : "⏳ Đang xử lý...");
		txtStatus.Foreground = Brushes.Yellow;
		btnRun.IsEnabled = false;
		_handler.SelectedSymbolIds = selectedIds;
		_handler.UseSelectionMode = useSelection;
		_handler.ElementToSelect = null;
		_handler.OnCompleteLogs = delegate(List<SupportLogItem> logs)
		{
			base.Dispatcher.Invoke(delegate
			{
				btnRun.IsEnabled = true;
				if (logs == null)
				{
					txtStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 226, 175));
					txtStatus.Text = "⚠\ufe0f Đã hủy thao tác.";
				}
				else
				{
					LogItems.Clear();
					int num = 1;
					int num2 = 0;
					int num3 = 0;
					foreach (SupportLogItem current in logs)
					{
						current.Index = num++;
						LogItems.Add(current);
						if (current.StatusCode == "moved" || current.StatusCode == "already")
						{
							num2++;
						}
						else
						{
							num3++;
						}
					}
					txtStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 227, 161));
					txtStatus.Text = "Hoàn thành xử lý.";
					txtSuccess.Text = $"✔ Thành công: {num2}";
					txtSkip.Text = $"⚠ Bỏ qua: {num3}";
					txtLogCount.Text = $"({logs.Count} đối tượng)";
				}
				if (useSelection)
				{
					Show();
					Activate();
				}
			});
		};
		if (useSelection)
		{
			Hide();
		}
		_exEvent.Raise();
	}

	private void DgLog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		if (dgLog.SelectedItem is SupportLogItem selectedItem && int.TryParse(selectedItem.ElementId, out var idVal))
		{
			ElementId id = new ElementId(idVal);
			_handler.ElementToSelect = id;
			_exEvent.Raise();
		}
	}

	private void BtnClear_Click(object sender, RoutedEventArgs e)
	{
		LogItems.Clear();
		txtSuccess.Text = "";
		txtSkip.Text = "";
		txtLogCount.Text = "";
		txtStatus.Text = "Đã xóa log.";
		txtStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(108, 112, 134));
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
			Uri resourceLocater = new Uri("/BIN;component/65.%20align%20support/alignsupportwindow.xaml", UriKind.Relative);
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
			rbActiveView = (RadioButton)target;
			break;
		case 2:
			rbSelection = (RadioButton)target;
			break;
		case 3:
			((Button)target).Click += BtnSelectAll_Click;
			break;
		case 4:
			((Button)target).Click += BtnDeselectAll_Click;
			break;
		case 5:
			icSupportTypes = (ItemsControl)target;
			break;
		case 6:
			txtStatus = (TextBlock)target;
			break;
		case 7:
			txtSuccess = (TextBlock)target;
			break;
		case 8:
			txtSkip = (TextBlock)target;
			break;
		case 9:
			txtLogCount = (TextBlock)target;
			break;
		case 10:
			dgLog = (DataGrid)target;
			dgLog.MouseDoubleClick += DgLog_MouseDoubleClick;
			break;
		case 11:
			btnRun = (Button)target;
			btnRun.Click += BtnRun_Click;
			break;
		case 12:
			btnClear = (Button)target;
			btnClear.Click += BtnClear_Click;
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
