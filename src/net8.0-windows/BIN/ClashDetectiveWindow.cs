using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;

namespace BIN;

public class ClashDetectiveWindow : Window, INotifyPropertyChanged, IComponentConnector
{
	private UIApplication _uiapp;

	private ClashActionHandler _handler;

	private ExternalEvent _externalEvent;

	private ObservableCollection<ClashGroupNode> _clashGroups = new ObservableCollection<ClashGroupNode>();

	private ClashResultData _selectedClashDetails;

	private List<string> _resolvedClashes;

	internal TextBlock tbFilePath;

	internal TreeView ClashesTreeView;

	internal GroupBox gbDetails;

	private bool _contentLoaded;

	public ClashResultData SelectedClashDetails
	{
		get
		{
			return _selectedClashDetails;
		}
		set
		{
			if (_selectedClashDetails != value)
			{
				_selectedClashDetails = value;
				OnPropertyChanged("SelectedClashDetails");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public ClashDetectiveWindow(UIApplication uiapp, ClashActionHandler handler, ExternalEvent externalEvent, List<string> resolvedClashes)
	{
		InitializeComponent();
		_uiapp = uiapp;
		_handler = handler;
		_externalEvent = externalEvent;
		_resolvedClashes = resolvedClashes ?? new List<string>();
		gbDetails.DataContext = this;
	}

	private void LoadXmlButton_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog dialog = new OpenFileDialog
		{
			Filter = "XML Files (*.xml)|*.xml|All files (*.*)|*.*",
			Title = "Select Navisworks clash report XML file"
		};
		if (dialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			tbFilePath.Text = dialog.FileName;
			List<ClashGroupNode> parsedGroups = ParseClashXml(dialog.FileName, _resolvedClashes);
			_clashGroups.Clear();
			foreach (ClashGroupNode group in parsedGroups)
			{
				foreach (ClashResultData item in group.Items)
				{
					item.PropertyChanged += ClashData_PropertyChanged;
				}
				_clashGroups.Add(group);
			}
			ClashesTreeView.ItemsSource = _clashGroups;
			if (_clashGroups.Any())
			{
				FetchTypeNames(parsedGroups);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("An error occurred while reading the XML file:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Hand);
			tbFilePath.Text = "No XML file selected...";
			_clashGroups.Clear();
		}
	}

	private void FetchTypeNames(List<ClashGroupNode> groups)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		List<ClashResultData> allClashItems = groups.SelectMany((ClashGroupNode g) => g.Items).ToList();
		if (!allClashItems.Any())
		{
			return;
		}
		_handler.Action = delegate(UIApplication uiapp)
		{
			Document document = uiapp.ActiveUIDocument.Document;
			Dictionary<string, string> cache = new Dictionary<string, string>();
			foreach (ClashResultData current in allClashItems)
			{
				current.Element1_Type = GetTypeName(document, current.Element1_Id, cache);
				current.Element2_Type = GetTypeName(document, current.Element2_Id, cache);
			}
		};
		_externalEvent.Raise();
	}

	private string GetTypeName(Document doc, string stringId, Dictionary<string, string> cache)
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		if (string.IsNullOrEmpty(stringId))
		{
			return "N/A";
		}
		if (cache.TryGetValue(stringId, out var cachedName))
		{
			return cachedName;
		}
		string foundName = "Not found";
		if (int.TryParse(stringId, out var id))
		{
			try
			{
				Element elem = doc.GetElement(new ElementId(id));
				if (elem != null)
				{
					if (elem is ElementType)
					{
						foundName = elem.Name;
					}
					else
					{
						Element element = doc.GetElement(elem.GetTypeId());
						ElementType type = (ElementType)(object)((element is ElementType) ? element : null);
						foundName = ((type != null) ? ((Element)type).Name : null) ?? "N/A (No Type)";
					}
				}
			}
			catch
			{
				foundName = "ID lookup error";
			}
		}
		else
		{
			foundName = "Invalid ID";
		}
		cache[stringId] = foundName;
		return foundName;
	}

	private void ClashesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		object selectedItem = ClashesTreeView.SelectedItem;
		ClashResultData selectedClash = selectedItem as ClashResultData;
		if (selectedClash == null || selectedClash.ClashPoint == null)
		{
			return;
		}
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_0346: Unknown result type (might be due to invalid IL or missing references)
			//IL_003c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0071: Unknown result type (might be due to invalid IL or missing references)
			//IL_0078: Expected O, but got Unknown
			//IL_007b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0095: Unknown result type (might be due to invalid IL or missing references)
			//IL_017f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0186: Expected O, but got Unknown
			//IL_01df: Unknown result type (might be due to invalid IL or missing references)
			//IL_01e9: Expected O, but got Unknown
			//IL_0243: Unknown result type (might be due to invalid IL or missing references)
			//IL_024d: Expected O, but got Unknown
			//IL_026b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0169: Unknown result type (might be due to invalid IL or missing references)
			//IL_010b: Unknown result type (might be due to invalid IL or missing references)
			//IL_02dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_02e7: Expected O, but got Unknown
			//IL_0304: Unknown result type (might be due to invalid IL or missing references)
			//IL_030e: Expected O, but got Unknown
			UIDocument activeUIDocument = uiapp.ActiveUIDocument;
			Document document = activeUIDocument.Document;
			View3D view3D = null;
			string username = uiapp.Application.Username;
			string dedicatedViewName = "Clash Nav - " + username;
			try
			{
				view3D = ((IEnumerable)new FilteredElementCollector(document).OfClass(typeof(View3D))).Cast<View3D>().FirstOrDefault((View3D v) => !((View)v).IsTemplate && ((Element)v).Name == dedicatedViewName);
				Transaction val = new Transaction(document, "Zoom to Clash (Personal View)");
				try
				{
					val.Start();
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
					if (view3D == null)
					{
						TaskDialog.Show("Error", "No 3D View found in the project.");
						return;
					}
					double num = 10.0;
					BoundingBoxXYZ val3 = new BoundingBoxXYZ();
					val3.Min = new XYZ(selectedClash.ClashPoint.X - num / 2.0, selectedClash.ClashPoint.Y - num / 2.0, selectedClash.ClashPoint.Z - num / 2.0);
					val3.Max = new XYZ(selectedClash.ClashPoint.X + num / 2.0, selectedClash.ClashPoint.Y + num / 2.0, selectedClash.ClashPoint.Z + num / 2.0);
					view3D.IsSectionBoxActive = true;
					view3D.SetSectionBox(val3);
					val.Commit();
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
				activeUIDocument.ActiveView = (View)(object)view3D;
				UIView val4 = activeUIDocument.GetOpenUIViews().FirstOrDefault((UIView v) => ((object)v.ViewId).Equals((object)((Element)view3D).Id));
				if (val4 != null)
				{
					val4.ZoomToFit();
				}
				List<ElementId> list = new List<ElementId>();
				if (int.TryParse(selectedClash.Element1_Id, out var result))
				{
					list.Add(new ElementId(result));
				}
				if (int.TryParse(selectedClash.Element2_Id, out var result2))
				{
					list.Add(new ElementId(result2));
				}
				if (list.Any())
				{
					activeUIDocument.Selection.SetElementIds((ICollection<ElementId>)list);
				}
			}
			catch (Exception ex)
			{
				TaskDialog.Show("Revit API Error", "An error occurred:\n\n" + ex.Message);
			}
		};
		_externalEvent.Raise();
	}

	private void ClashesTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	{
		if (e.NewValue is ClashResultData selectedClash)
		{
			SelectedClashDetails = selectedClash;
		}
		else
		{
			SelectedClashDetails = null;
		}
	}

	private void ClashData_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		if (!(e.PropertyName == "IsResolved"))
		{
			return;
		}
		ClashResultData clashData = sender as ClashResultData;
		string key = clashData.GroupName + "|" + clashData.Name;
		bool changed = false;
		if (clashData.IsResolved && !_resolvedClashes.Contains(key))
		{
			_resolvedClashes.Add(key);
			changed = true;
		}
		else if (!clashData.IsResolved && _resolvedClashes.Contains(key))
		{
			_resolvedClashes.Remove(key);
			changed = true;
		}
		if (!changed)
		{
			return;
		}
		List<string> copyToSave = _resolvedClashes.ToList();
		_handler.Action = delegate(UIApplication uiapp)
		{
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Expected O, but got Unknown
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_002f: Unknown result type (might be due to invalid IL or missing references)
			Document document = uiapp.ActiveUIDocument.Document;
			Transaction val = new Transaction(document, "Save Clash Status");
			try
			{
				val.Start();
				ClashStorageUtil.SaveResolvedClashes(document, copyToSave);
				val.Commit();
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		};
		_externalEvent.Raise();
	}

	public static List<ClashGroupNode> ParseClashXml(string filePath, List<string> resolvedClashes)
	{
		List<ClashGroupNode> clashGroups = new List<ClashGroupNode>();
		XDocument doc = XDocument.Load(filePath);
		IEnumerable<XElement> clashTestElements = doc.Descendants("clashtest");
		foreach (XElement testElement in clashTestElements)
		{
			ClashGroupNode testNode = new ClashGroupNode
			{
				GroupName = (testElement.Attribute("name")?.Value ?? "Unnamed Test")
			};
			IEnumerable<XElement> clashResultElements = testElement.Descendants("clashresult");
			if (clashResultElements != null)
			{
				foreach (XElement resultElement in clashResultElements)
				{
					ClashResultData clashData = ParseClashResult(resultElement);
					if (clashData != null)
					{
						clashData.GroupName = testNode.GroupName;
						string key = clashData.GroupName + "|" + clashData.Name;
						clashData.IsResolved = resolvedClashes?.Contains(key) ?? false;
						testNode.Items.Add(clashData);
					}
				}
			}
			if (testNode.Items.Any())
			{
				clashGroups.Add(testNode);
			}
		}
		return clashGroups;
	}

	private static ClashResultData ParseClashResult(XElement element)
	{
		//IL_01c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Expected O, but got Unknown
		ClashResultData clashData = new ClashResultData
		{
			Name = element.Attribute("name")?.Value,
			Status = element.Element("resultstatus")?.Value,
			GridLocation = element.Element("gridlocation")?.Value
		};
		List<XElement> clashObjects = element.Descendants("clashobject").ToList();
		if (clashObjects.Count == 2)
		{
			clashData.Element1_Id = clashObjects[0].Descendants("value").FirstOrDefault()?.Value;
			clashData.Element2_Id = clashObjects[1].Descendants("value").FirstOrDefault()?.Value;
		}
		XElement pos3f = element.Descendants("clashpoint").FirstOrDefault()?.Element("pos3f");
		if (pos3f != null)
		{
			try
			{
				double x = double.Parse(pos3f.Attribute("x")?.Value, CultureInfo.InvariantCulture) * 3.28084;
				double y = double.Parse(pos3f.Attribute("y")?.Value, CultureInfo.InvariantCulture) * 3.28084;
				double z = double.Parse(pos3f.Attribute("z")?.Value, CultureInfo.InvariantCulture) * 3.28084;
				clashData.ClashPoint = new XYZ(x, y, z);
				return clashData;
			}
			catch
			{
			}
		}
		return null;
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/37.%20clash%20detective/clashdetectivewindow.xaml", UriKind.Relative);
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
			((Button)target).Click += LoadXmlButton_Click;
			break;
		case 2:
			tbFilePath = (TextBlock)target;
			break;
		case 3:
			ClashesTreeView = (TreeView)target;
			ClashesTreeView.MouseDoubleClick += ClashesTreeView_MouseDoubleClick;
			ClashesTreeView.SelectedItemChanged += ClashesTreeView_SelectedItemChanged;
			break;
		case 4:
			gbDetails = (GroupBox)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
