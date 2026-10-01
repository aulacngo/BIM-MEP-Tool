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
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BIN;

public class CheckPipeFittingWindow : Window, IComponentConnector
{
	private UIApplication _uiapp;

	private CheckPipeFittingHandler _handler;

	private ExternalEvent _externalEvent;

	private List<string> _resolvedIds;

	internal RadioButton rbActiveView;

	internal RadioButton rbEntireProject;

	internal ComboBox cbSystemType;

	internal ComboBox cbFittingType;

	internal Button btnCheck;

	internal DataGrid dgPipeFittings;

	internal TextBlock txtTotalCount;

	private bool _contentLoaded;

	public CheckPipeFittingWindow(UIApplication uiapp, CheckPipeFittingHandler handler, ExternalEvent externalEvent, List<string> resolvedIds)
	{
		InitializeComponent();
		_uiapp = uiapp;
		_handler = handler;
		_externalEvent = externalEvent;
		_resolvedIds = resolvedIds ?? new List<string>();
		LoadSystemTypes();
	}

	private void LoadSystemTypes()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			UIDocument uidoc = _uiapp.ActiveUIDocument;
			if (uidoc == null)
			{
				return;
			}
			Document doc = uidoc.Document;
			List<PipingSystemType> systemTypes = (from PipingSystemType st in (IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(PipingSystemType))
				orderby ((Element)st).Name
				select st).ToList();
			List<SystemTypeItem> list = new List<SystemTypeItem>();
			list.Add(new SystemTypeItem
			{
				Name = "<Tất cả>",
				Id = ElementId.InvalidElementId
			});
			foreach (PipingSystemType st2 in systemTypes)
			{
				list.Add(new SystemTypeItem
				{
					Name = ((Element)st2).Name,
					Id = ((Element)st2).Id
				});
			}
			cbSystemType.ItemsSource = list;
			cbSystemType.SelectedIndex = 0;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi khi tải danh sách System Type: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private ElementId GetFittingSystemTypeId(FamilyInstance fitting)
	{
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Expected O, but got Unknown
		if (fitting == null)
		{
			return ElementId.InvalidElementId;
		}
		Parameter sysTypeParam = ((Element)fitting).get_Parameter((BuiltInParameter)(-1140334));
		if (sysTypeParam != null && sysTypeParam.HasValue)
		{
			ElementId sysTypeId = sysTypeParam.AsElementId();
			if (sysTypeId != (ElementId)null && sysTypeId != ElementId.InvalidElementId)
			{
				return sysTypeId;
			}
		}
		MEPModel mEPModel = fitting.MEPModel;
		if (((mEPModel != null) ? mEPModel.ConnectorManager : null) != null)
		{
			foreach (Connector connector2 in fitting.MEPModel.ConnectorManager.Connectors)
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
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		bool entireProject = rbEntireProject.IsChecked == true;
		SystemTypeItem selectedSystemType = cbSystemType.SelectedItem as SystemTypeItem;
		ElementId targetSystemTypeId = ((selectedSystemType != null) ? selectedSystemType.Id : ElementId.InvalidElementId);
		string targetFittingType = null;
		if (cbFittingType.SelectedItem is ComboBoxItem selectedComboItem)
		{
			targetFittingType = selectedComboItem.Content as string;
		}
		if (targetFittingType == "<Tất cả>")
		{
			targetFittingType = null;
		}
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_0041: Unknown result type (might be due to invalid IL or missing references)
			//IL_0047: Expected O, but got Unknown
			//IL_002b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0031: Expected O, but got Unknown
			//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f3: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f6: Invalid comparison between Unknown and I4
			//IL_010f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0112: Invalid comparison between Unknown and I4
			//IL_0114: Unknown result type (might be due to invalid IL or missing references)
			//IL_0118: Invalid comparison between Unknown and I4
			//IL_0134: Unknown result type (might be due to invalid IL or missing references)
			//IL_0137: Invalid comparison between Unknown and I4
			//IL_014d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0151: Invalid comparison between Unknown and I4
			//IL_0153: Unknown result type (might be due to invalid IL or missing references)
			//IL_0157: Invalid comparison between Unknown and I4
			//IL_0170: Unknown result type (might be due to invalid IL or missing references)
			//IL_0174: Invalid comparison between Unknown and I4
			//IL_0176: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Invalid comparison between Unknown and I4
			//IL_017c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0180: Invalid comparison between Unknown and I4
			//IL_0182: Unknown result type (might be due to invalid IL or missing references)
			//IL_0186: Invalid comparison between Unknown and I4
			//IL_026f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0276: Expected O, but got Unknown
			//IL_0279: Unknown result type (might be due to invalid IL or missing references)
			//IL_027f: Invalid comparison between Unknown and I4
			try
			{
				UIDocument activeUIDocument = uiapp.ActiveUIDocument;
				Document document = activeUIDocument.Document;
				FilteredElementCollector val = ((!entireProject) ? new FilteredElementCollector(document, ((Element)document.ActiveView).Id) : new FilteredElementCollector(document));
				List<FamilyInstance> list = ((IEnumerable)val.OfCategory((BuiltInCategory)(-2008049)).OfClass(typeof(FamilyInstance))).Cast<FamilyInstance>().ToList();
				List<UnconnectedFittingData> list2 = new List<UnconnectedFittingData>();
				foreach (FamilyInstance current in list)
				{
					if (current.Symbol != null && current.Symbol.Family != null)
					{
						Parameter val2 = ((Element)current.Symbol.Family).get_Parameter((BuiltInParameter)(-1114206));
						if (val2 != null)
						{
							int num = val2.AsInteger();
							PartType val3 = (PartType)num;
							string text = "";
							bool flag = false;
							if ((int)val3 == 5)
							{
								text = "Elbow";
								flag = true;
							}
							else if ((int)val3 == 6 || (int)val3 == 25)
							{
								text = "Tee";
								flag = true;
							}
							else if ((int)val3 == 7)
							{
								text = "Reducer";
								flag = true;
							}
							else if ((int)val3 == 9 || (int)val3 == 53)
							{
								text = "Endcap";
								flag = true;
							}
							else if ((int)val3 == 11 || (int)val3 == 10 || (int)val3 == 22 || (int)val3 == 21 || val3.ToString().IndexOf("Tap", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								text = "Tap";
								flag = true;
							}
							if (flag && (targetFittingType == null || text.Equals(targetFittingType, StringComparison.OrdinalIgnoreCase)))
							{
								if (targetSystemTypeId != ElementId.InvalidElementId)
								{
									ElementId fittingSystemTypeId = GetFittingSystemTypeId(current);
									if (fittingSystemTypeId != targetSystemTypeId)
									{
										continue;
									}
								}
								bool flag2 = false;
								MEPModel mEPModel = current.MEPModel;
								ConnectorManager val4 = ((mEPModel != null) ? mEPModel.ConnectorManager : null);
								if (val4 != null)
								{
									foreach (Connector connector in val4.Connectors)
									{
										Connector val5 = connector;
										if ((int)val5.ConnectorType != 4 && !val5.IsConnected)
										{
											flag2 = true;
											break;
										}
									}
								}
								if (flag2)
								{
									list2.Add(new UnconnectedFittingData
									{
										Id = ((object)((Element)current).Id).ToString(),
										FittingType = text
									});
								}
							}
						}
					}
				}
				List<UnconnectedFittingData> unconnectedData = new List<UnconnectedFittingData>();
				for (int i = 0; i < list2.Count; i++)
				{
					UnconnectedFittingData unconnectedFittingData = list2[i];
					unconnectedFittingData.Index = i + 1;
					unconnectedFittingData.IsResolved = _resolvedIds.Contains(unconnectedFittingData.Id);
					unconnectedFittingData.PropertyChanged += FittingData_PropertyChanged;
					unconnectedData.Add(unconnectedFittingData);
				}
				base.Dispatcher.Invoke(delegate
				{
					txtTotalCount.Text = "Tổng cộng: " + unconnectedData.Count + " đối tượng";
					if (unconnectedData.Count == 0)
					{
						MessageBox.Show("Tất cả Pipe Fittings trong bộ lọc đã chọn đã được kết nối đầy đủ", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
						dgPipeFittings.ItemsSource = null;
					}
					else
					{
						dgPipeFittings.ItemsSource = unconnectedData;
					}
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

	private void DgPipeFittings_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		if (!(dgPipeFittings.SelectedItem is UnconnectedFittingData { Id: var selectedIdString }) || !int.TryParse(selectedIdString, out var idValueInt))
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
				string dedicatedViewName = "CheckPipeFitting Nav - " + username;
				Transaction val = new Transaction(document, "Zoom to Pipe Fitting");
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
								double num = 1.5;
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

	private void FittingData_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		if (!(e.PropertyName == "IsResolved") || !(sender is UnconnectedFittingData { Id: var currentId } item))
		{
			return;
		}
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
				Transaction val = new Transaction(document, "Save CheckPipeFitting Status");
				try
				{
					val.Start();
					CheckPipeFittingStorageUtil.SaveResolvedFittings(document, copyToSave);
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
			Uri resourceLocater = new Uri("/BIN;component/59.%20check%20pipe%20fitting/checkpipefittingwindow.xaml", UriKind.Relative);
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
			cbSystemType = (ComboBox)target;
			break;
		case 4:
			cbFittingType = (ComboBox)target;
			break;
		case 5:
			btnCheck = (Button)target;
			btnCheck.Click += BtnCheck_Click;
			break;
		case 6:
			dgPipeFittings = (DataGrid)target;
			dgPipeFittings.MouseDoubleClick += DgPipeFittings_MouseDoubleClick;
			break;
		case 7:
			txtTotalCount = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
