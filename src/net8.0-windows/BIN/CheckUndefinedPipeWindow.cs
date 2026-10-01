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
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BIN;

public class CheckUndefinedPipeWindow : Window, IComponentConnector
{
	private UIApplication _uiapp;

	private CheckUndefinedPipeHandler _handler;

	private ExternalEvent _externalEvent;

	private List<string> _resolvedIds;

	internal RadioButton rbActiveView;

	internal RadioButton rbEntireProject;

	internal Button btnCheck;

	internal DataGrid dgPipes;

	internal TextBlock txtTotalCount;

	private bool _contentLoaded;

	public CheckUndefinedPipeWindow(UIApplication uiapp, CheckUndefinedPipeHandler handler, ExternalEvent externalEvent, List<string> resolvedIds)
	{
		InitializeComponent();
		_uiapp = uiapp;
		_handler = handler;
		_externalEvent = externalEvent;
		_resolvedIds = resolvedIds ?? new List<string>();
	}

	private ElementId GetPipeSystemTypeId(Pipe pipe)
	{
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		if (pipe == null)
		{
			return ElementId.InvalidElementId;
		}
		Parameter sysTypeParam = ((Element)pipe).get_Parameter((BuiltInParameter)(-1140334));
		if (sysTypeParam != null && sysTypeParam.HasValue)
		{
			ElementId sysTypeId = sysTypeParam.AsElementId();
			if (sysTypeId != (ElementId)null && sysTypeId != ElementId.InvalidElementId)
			{
				return sysTypeId;
			}
		}
		if (((MEPCurve)pipe).MEPSystem != null)
		{
			return ((Element)((MEPCurve)pipe).MEPSystem).GetTypeId();
		}
		if (((MEPCurve)pipe).ConnectorManager != null)
		{
			foreach (Connector connector2 in ((MEPCurve)pipe).ConnectorManager.Connectors)
			{
				Connector connector = connector2;
				if (connector.MEPSystem != null)
				{
					ElementId sysTypeId2 = ((Element)connector.MEPSystem).GetTypeId();
					if (sysTypeId2 != (ElementId)null && sysTypeId2 != ElementId.InvalidElementId)
					{
						return sysTypeId2;
					}
				}
			}
		}
		return ElementId.InvalidElementId;
	}

	private void BtnCheck_Click(object sender, RoutedEventArgs e)
	{
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		bool entireProject = rbEntireProject.IsChecked == true;
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Expected O, but got Unknown
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Expected O, but got Unknown
			try
			{
				UIDocument activeUIDocument = uiapp.ActiveUIDocument;
				Document document = activeUIDocument.Document;
				FilteredElementCollector val = ((!entireProject) ? new FilteredElementCollector(document, ((Element)document.ActiveView).Id) : new FilteredElementCollector(document));
				List<Pipe> list = ((IEnumerable)val.OfCategory((BuiltInCategory)(-2008044)).OfClass(typeof(Pipe))).Cast<Pipe>().ToList();
				List<UndefinedPipeData> list2 = new List<UndefinedPipeData>();
				foreach (Pipe current in list)
				{
					ElementId pipeSystemTypeId = GetPipeSystemTypeId(current);
					bool flag = false;
					if (pipeSystemTypeId == ElementId.InvalidElementId)
					{
						flag = true;
					}
					else
					{
						Element element = document.GetElement(pipeSystemTypeId);
						if (element == null)
						{
							flag = true;
						}
					}
					if (flag)
					{
						string text = "";
						Parameter val2 = ((Element)current).get_Parameter((BuiltInParameter)(-1114240));
						if (val2 != null && val2.HasValue)
						{
							text = val2.AsString();
						}
						if (string.IsNullOrEmpty(text))
						{
							text = ((Element)current).Name;
						}
						double num = 0.0;
						Parameter val3 = ((Element)current).get_Parameter((BuiltInParameter)(-1004005));
						if (val3 != null && val3.HasValue)
						{
							num = val3.AsDouble();
						}
						double num2 = Math.Round(num * 304.8);
						string length = $"{num2} mm";
						string systemType = "<Undefined>";
						list2.Add(new UndefinedPipeData
						{
							Id = ((object)((Element)current).Id).ToString(),
							Size = text,
							Length = length,
							SystemType = systemType
						});
					}
				}
				List<UndefinedPipeData> undefinedData = new List<UndefinedPipeData>();
				for (int i = 0; i < list2.Count; i++)
				{
					UndefinedPipeData undefinedPipeData = list2[i];
					undefinedPipeData.Index = i + 1;
					undefinedPipeData.IsResolved = _resolvedIds.Contains(undefinedPipeData.Id);
					undefinedPipeData.PropertyChanged += PipeData_PropertyChanged;
					undefinedData.Add(undefinedPipeData);
				}
				base.Dispatcher.Invoke(delegate
				{
					if (undefinedData.Count == 0)
					{
						MessageBox.Show("Không tìm thấy Pipe nào chưa thiết lập hệ thống", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
						dgPipes.ItemsSource = null;
					}
					else
					{
						dgPipes.ItemsSource = undefinedData;
					}
					UpdateTotalCount();
				});
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				base.Dispatcher.Invoke(delegate
				{
					MessageBox.Show("Lỗi trong quá trình kiểm tra: " + ex3.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				});
			}
		};
		_externalEvent.Raise();
	}

	private void DgPipes_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		if (!(dgPipes.SelectedItem is UndefinedPipeData { Id: var selectedIdString }) || !int.TryParse(selectedIdString, out var idValueInt))
		{
			return;
		}
		ElementId selectedId = new ElementId(idValueInt);
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0065: Expected O, but got Unknown
			//IL_0068: Unknown result type (might be due to invalid IL or missing references)
			//IL_0070: Unknown result type (might be due to invalid IL or missing references)
			//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
			//IL_025c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0129: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
			//IL_01c8: Expected O, but got Unknown
			//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
			//IL_0201: Expected O, but got Unknown
			//IL_0231: Unknown result type (might be due to invalid IL or missing references)
			//IL_023b: Expected O, but got Unknown
			try
			{
				UIDocument activeUIDocument = uiapp.ActiveUIDocument;
				Document document = activeUIDocument.Document;
				activeUIDocument.Selection.SetElementIds((ICollection<ElementId>)new List<ElementId> { selectedId });
				View3D view3D = null;
				string username = uiapp.Application.Username;
				string dedicatedViewName = "CheckUndefinedPipe Nav - " + username;
				Transaction val = new Transaction(document, "Zoom to Pipe");
				try
				{
					val.Start();
					view3D = ((IEnumerable)new FilteredElementCollector(document).OfClass(typeof(View3D))).Cast<View3D>().FirstOrDefault((View3D v) => !((View)v).IsTemplate && ((Element)v).Name == dedicatedViewName);
					if (view3D == null)
					{
						ViewFamilyType val2 = ((IEnumerable)new FilteredElementCollector(document).OfClass(typeof(ViewFamilyType))).Cast<ViewFamilyType>().FirstOrDefault((ViewFamilyType vft) => (int)vft.ViewFamily == 102);
						if (val2 != null)
						{
							view3D = View3D.CreateIsometric(document, ((Element)val2).Id);
							((Element)view3D).Name = dedicatedViewName;
						}
						else
						{
							view3D = ((IEnumerable)new FilteredElementCollector(document).OfClass(typeof(View3D))).Cast<View3D>().FirstOrDefault((View3D v) => !((View)v).IsTemplate);
						}
					}
					if (view3D != null)
					{
						Element element = document.GetElement(selectedId);
						if (element != null)
						{
							BoundingBoxXYZ val3 = element.get_BoundingBox(null);
							if (val3 != null)
							{
								double num = 2.0;
								BoundingBoxXYZ val4 = new BoundingBoxXYZ();
								val4.Min = new XYZ(val3.Min.X - num, val3.Min.Y - num, val3.Min.Z - num);
								val4.Max = new XYZ(val3.Max.X + num, val3.Max.Y + num, val3.Max.Z + num);
								view3D.IsSectionBoxActive = true;
								view3D.SetSectionBox(val4);
							}
						}
					}
					val.Commit();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
				if (view3D != null)
				{
					activeUIDocument.ActiveView = (View)(object)view3D;
					UIView val5 = activeUIDocument.GetOpenUIViews().FirstOrDefault((UIView v) => ((object)v.ViewId).Equals((object)((Element)view3D).Id));
					if (val5 != null)
					{
						val5.ZoomToFit();
						activeUIDocument.ShowElements(selectedId);
					}
				}
				else
				{
					activeUIDocument.ShowElements(selectedId);
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				base.Dispatcher.Invoke(delegate
				{
					MessageBox.Show("Lỗi khi mở View 3D: " + ex3.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
				});
			}
		};
		_externalEvent.Raise();
	}

	private void UpdateTotalCount()
	{
		if (dgPipes.ItemsSource is IEnumerable<UndefinedPipeData> items)
		{
			int total = items.Count();
			int unresolved = items.Count((UndefinedPipeData x) => !x.IsResolved);
			txtTotalCount.Text = "Chưa xử lý: " + unresolved + "/" + total + " đối tượng";
		}
		else
		{
			txtTotalCount.Text = "Chưa xử lý: 0 đối tượng";
		}
	}

	private void PipeData_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		if (!(e.PropertyName == "IsResolved") || !(sender is UndefinedPipeData item))
		{
			return;
		}
		base.Dispatcher.Invoke(UpdateTotalCount);
		string currentId = item.Id;
		bool isModified = false;
		if (item.IsResolved && !_resolvedIds.Contains(currentId))
		{
			_resolvedIds.Add(currentId);
			isModified = true;
		}
		else if (!item.IsResolved && _resolvedIds.Contains(currentId))
		{
			_resolvedIds.Remove(currentId);
			isModified = true;
		}
		if (!isModified)
		{
			return;
		}
		List<string> copyToSave = _resolvedIds.ToList();
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Expected O, but got Unknown
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0030: Unknown result type (might be due to invalid IL or missing references)
			try
			{
				Document document = uiapp.ActiveUIDocument.Document;
				Transaction val = new Transaction(document, "Save CheckUndefinedPipe Status");
				try
				{
					val.Start();
					CheckUndefinedPipeStorageUtil.SaveResolvedPipes(document, copyToSave);
					val.Commit();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			catch
			{
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
			Uri resourceLocater = new Uri("/BIN;component/62.%20check%20undefined%20pipe/checkundefinedpipewindow.xaml", UriKind.Relative);
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
			rbEntireProject = (RadioButton)target;
			break;
		case 3:
			btnCheck = (Button)target;
			btnCheck.Click += BtnCheck_Click;
			break;
		case 4:
			dgPipes = (DataGrid)target;
			dgPipes.MouseDoubleClick += DgPipes_MouseDoubleClick;
			break;
		case 5:
			txtTotalCount = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
