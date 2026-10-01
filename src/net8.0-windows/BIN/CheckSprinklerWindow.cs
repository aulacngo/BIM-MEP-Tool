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

public class CheckSprinklerWindow : Window, IComponentConnector
{
	private UIApplication _uiapp;

	private CheckSprinklerHandler _handler;

	private ExternalEvent _externalEvent;

	private List<string> _resolvedIds;

	internal RadioButton rbActiveView;

	internal RadioButton rbEntireProject;

	internal Button btnCheck;

	internal DataGrid dgSprinklers;

	internal TextBlock txtTotalCount;

	private bool _contentLoaded;

	public CheckSprinklerWindow(UIApplication uiapp, CheckSprinklerHandler handler, ExternalEvent externalEvent, List<string> resolvedIds)
	{
		InitializeComponent();
		_uiapp = uiapp;
		_handler = handler;
		_externalEvent = externalEvent;
		_resolvedIds = resolvedIds ?? new List<string>();
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
			//IL_0119: Unknown result type (might be due to invalid IL or missing references)
			//IL_0120: Expected O, but got Unknown
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00d8: Expected O, but got Unknown
			//IL_018f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0199: Expected O, but got Unknown
			//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
			//IL_01b6: Expected O, but got Unknown
			//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
			//IL_01d3: Expected O, but got Unknown
			//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
			//IL_01f0: Expected O, but got Unknown
			try
			{
				UIDocument activeUIDocument = uiapp.ActiveUIDocument;
				Document document = activeUIDocument.Document;
				FilteredElementCollector val = ((!entireProject) ? new FilteredElementCollector(document, ((Element)document.ActiveView).Id) : new FilteredElementCollector(document));
				List<FamilyInstance> list = ((IEnumerable)val.OfCategory((BuiltInCategory)(-2008099)).OfClass(typeof(FamilyInstance))).Cast<FamilyInstance>().ToList();
				List<string> list2 = new List<string>();
				foreach (FamilyInstance current in list)
				{
					bool flag = false;
					MEPModel mEPModel = current.MEPModel;
					ConnectorManager val2 = ((mEPModel != null) ? mEPModel.ConnectorManager : null);
					if (val2 != null)
					{
						try
						{
							foreach (Connector connector in val2.Connectors)
							{
								Connector val3 = connector;
								if (val3.IsConnected)
								{
									try
									{
										ConnectorSet allRefs = val3.AllRefs;
										if (allRefs != null)
										{
											foreach (Connector item in allRefs)
											{
												Connector val4 = item;
												try
												{
													if (val4 != null && val4.Owner != null && val4.Owner.Id != ((Element)current).Id)
													{
														Element owner = val4.Owner;
														if (owner is Pipe || owner is FlexPipe || (owner.Category != null && (((object)owner.Category.Id).Equals((object)new ElementId((BuiltInCategory)(-2008044))) || ((object)owner.Category.Id).Equals((object)new ElementId((BuiltInCategory)(-2008050))) || ((object)owner.Category.Id).Equals((object)new ElementId((BuiltInCategory)(-2008049))) || ((object)owner.Category.Id).Equals((object)new ElementId((BuiltInCategory)(-2008055))))))
														{
															flag = true;
															break;
														}
													}
												}
												catch
												{
												}
											}
										}
									}
									catch
									{
									}
								}
								if (flag)
								{
									break;
								}
							}
						}
						catch
						{
						}
					}
					if (!flag)
					{
						list2.Add(((object)((Element)current).Id).ToString());
					}
				}
				List<UnconnectedSprinklerData> unconnectedData = new List<UnconnectedSprinklerData>();
				for (int i = 0; i < list2.Count; i++)
				{
					string text = list2[i];
					UnconnectedSprinklerData unconnectedSprinklerData = new UnconnectedSprinklerData
					{
						Index = i + 1,
						Id = text,
						IsResolved = _resolvedIds.Contains(text)
					};
					unconnectedSprinklerData.PropertyChanged += SprinklerData_PropertyChanged;
					unconnectedData.Add(unconnectedSprinklerData);
				}
				base.Dispatcher.Invoke(delegate
				{
					txtTotalCount.Text = "Tổng cộng: " + unconnectedData.Count + " đối tượng";
					if (unconnectedData.Count == 0)
					{
						MessageBox.Show("Tất cả Sprinkler đã được kết nối với fitting hoặc pipe", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Asterisk);
						dgSprinklers.ItemsSource = null;
					}
					else
					{
						dgSprinklers.ItemsSource = unconnectedData;
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

	private void DgSprinklers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		if (!(dgSprinklers.SelectedItem is UnconnectedSprinklerData { Id: var selectedIdString }) || !int.TryParse(selectedIdString, out var idValueInt))
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
				string dedicatedViewName = "CheckSprinkler Nav - " + username;
				Transaction val = new Transaction(document, "Zoom to Sprinkler");
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

	private void SprinklerData_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		if (!(e.PropertyName == "IsResolved") || !(sender is UnconnectedSprinklerData { Id: var currentId } item))
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
				Transaction val = new Transaction(document, "Save CheckSprinkler Status");
				try
				{
					val.Start();
					CheckSprinklerStorageUtil.SaveResolvedSprinklers(document, copyToSave);
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
			Uri resourceLocater = new Uri("/BIN;component/158.%20check%20sprinkler/checksprinklerwindow.xaml", UriKind.Relative);
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
			dgSprinklers = (DataGrid)target;
			dgSprinklers.MouseDoubleClick += DgSprinklers_MouseDoubleClick;
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
