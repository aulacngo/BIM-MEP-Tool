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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using Autodesk.Revit.DB;

namespace BIN;

public class MainWindow : Window, IComponentConnector
{
	private Document _doc;

	private List<ViewSchedule> _allSchedules;

	internal System.Windows.Controls.TreeView TvSchedules;

	internal System.Windows.Controls.RadioButton RbSeparate;

	internal System.Windows.Controls.RadioButton RbSingle;

	private bool _contentLoaded;

	public ObservableCollection<ScheduleNode> ScheduleTree { get; set; }

	protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		if (e.Key == Key.Escape)
		{
			Close();
		}
	}

	public MainWindow(Document doc)
	{
		InitializeComponent();
		_doc = doc;
		LoadAllSchedules();
		LoadScheduleTree();
		base.DataContext = this;
	}

	private void LoadAllSchedules()
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		_allSchedules = (from ViewSchedule s in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(ViewSchedule)).WhereElementIsNotElementType()
			where !s.IsTitleblockRevisionSchedule && !s.IsInternalKeynoteSchedule
			orderby ((Element)s).Name
			select s).ToList();
	}

	private void LoadScheduleTree()
	{
		ScheduleTree = new ObservableCollection<ScheduleNode>();
		BrowserOrganization browserOrg = BrowserOrganization.GetCurrentBrowserOrganizationForSchedules(_doc);
		IList<FolderItemInfo> folderSettings = null;
		if (browserOrg != null)
		{
			folderSettings = browserOrg.GetFolderItems(((Element)browserOrg).Id);
		}
		ScheduleNode rootNode = new ScheduleNode
		{
			Name = "Schedules/Quantities (KHỐI LƯỢNG)",
			IsExpanded = true
		};
		foreach (ViewSchedule schedule in _allSchedules)
		{
			List<string> folderPath = GetScheduleFolderPath(schedule, folderSettings);
			ScheduleNode currentNode = rootNode;
			foreach (string folderName in folderPath)
			{
				ScheduleNode childNode = currentNode.Children.FirstOrDefault((ScheduleNode n) => n.Name == folderName);
				if (childNode == null)
				{
					childNode = new ScheduleNode
					{
						Name = folderName,
						IsExpanded = false
					};
					currentNode.Children.Add(childNode);
				}
				currentNode = childNode;
			}
			ScheduleNode scheduleNode = new ScheduleNode
			{
				Name = ((Element)schedule).Name,
				Schedule = schedule
			};
			currentNode.Children.Add(scheduleNode);
		}
		ScheduleTree.Add(rootNode);
		TvSchedules.ItemsSource = ScheduleTree;
	}

	private List<string> GetScheduleFolderPath(ViewSchedule schedule, IList<FolderItemInfo> folderSettings)
	{
		List<string> path = new List<string>();
		if (folderSettings == null || folderSettings.Count == 0)
		{
			return path;
		}
		foreach (FolderItemInfo setting in folderSettings)
		{
			ElementId paramId = setting.ElementId;
			if (paramId == ElementId.InvalidElementId)
			{
				continue;
			}
			Element paramElement = _doc.GetElement(paramId);
			if (paramElement != null)
			{
				Parameter param = ((Element)schedule).LookupParameter(paramElement.Name);
				string paramValue = ((param != null) ? param.AsString() : null);
				if (!string.IsNullOrEmpty(paramValue))
				{
					path.Add(paramValue);
				}
			}
		}
		return path;
	}

	private void SelectAll_Click(object sender, RoutedEventArgs e)
	{
		SetSelectionRecursive(ScheduleTree, val: true);
	}

	private void SelectNone_Click(object sender, RoutedEventArgs e)
	{
		SetSelectionRecursive(ScheduleTree, val: false);
	}

	private void SetSelectionRecursive(IEnumerable<ScheduleNode> nodes, bool val)
	{
		foreach (ScheduleNode node in nodes)
		{
			node.IsSelected = val;
			SetSelectionRecursive(node.Children, val);
		}
	}

	private List<ViewSchedule> GetSelectedSchedules(IEnumerable<ScheduleNode> nodes)
	{
		List<ViewSchedule> result = new List<ViewSchedule>();
		foreach (ScheduleNode node in nodes)
		{
			if (node.Schedule != null && node.IsSelected == true)
			{
				result.Add(node.Schedule);
			}
			result.AddRange(GetSelectedSchedules(node.Children));
		}
		return result;
	}

	private void BtnExport_Click(object sender, RoutedEventArgs e)
	{
		List<ViewSchedule> selected = GetSelectedSchedules(ScheduleTree);
		if (!selected.Any())
		{
			System.Windows.MessageBox.Show("Vui lòng chọn ít nhất 1 Schedule!");
			return;
		}
		using FolderBrowserDialog dialog = new FolderBrowserDialog();
		if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
		{
			try
			{
				ExcelExporter.Export(selected, dialog.SelectedPath, RbSingle.IsChecked == true);
				System.Windows.MessageBox.Show("Xuất file thành công!", "Thông báo");
				Close();
				return;
			}
			catch (Exception ex)
			{
				System.Windows.MessageBox.Show("Lỗi: " + ex.Message);
				return;
			}
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/24.%20export%20schedule%20to%20excel/mainwindow.xaml", UriKind.Relative);
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
			((System.Windows.Controls.Button)target).Click += SelectAll_Click;
			break;
		case 2:
			((System.Windows.Controls.Button)target).Click += SelectNone_Click;
			break;
		case 3:
			TvSchedules = (System.Windows.Controls.TreeView)target;
			break;
		case 4:
			RbSeparate = (System.Windows.Controls.RadioButton)target;
			break;
		case 5:
			RbSingle = (System.Windows.Controls.RadioButton)target;
			break;
		case 6:
			((System.Windows.Controls.Button)target).Click += BtnExport_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
