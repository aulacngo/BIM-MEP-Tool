using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;

namespace BIN;

public class CompareElementWindow : Window, IComponentConnector, IStyleConnector
{
	private UIDocument _uidoc;

	private Document _doc;

	private CompareElementEventHandler _handler;

	private ExternalEvent _exEvent;

	private List<SystemCompareItem> _compareItems = new List<SystemCompareItem>();

	internal DataGrid dgCompare;

	private bool _contentLoaded;

	public new bool IsLoaded { get; private set; }

	public CompareElementWindow(UIDocument uidoc, CompareElementEventHandler handler, ExternalEvent exEvent)
	{
		InitializeComponent();
		_uidoc = uidoc;
		_doc = uidoc.Document;
		_handler = handler;
		_exEvent = exEvent;
		IsLoaded = true;
		LoadProjectSystemTypes();
	}

	protected override void OnClosed(EventArgs e)
	{
		IsLoaded = false;
		base.OnClosed(e);
	}

	private void ExecuteApi(Action action)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		_handler.Action = action;
		_exEvent.Raise();
	}

	private void LoadProjectSystemTypes()
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		_compareItems.Clear();
		List<Element> sysTypes = new List<Element>();
		sysTypes.AddRange(new FilteredElementCollector(_doc).OfClass(typeof(PipingSystemType)).ToElements());
		sysTypes.AddRange(new FilteredElementCollector(_doc).OfClass(typeof(MechanicalSystemType)).ToElements());
		sysTypes.AddRange(new FilteredElementCollector(_doc).OfClass(typeof(CableTrayType)).ToElements());
		foreach (Element st in sysTypes)
		{
			bool isCableTray = st is CableTrayType;
			_compareItems.Add(new SystemCompareItem
			{
				ElementType = st,
				ProjectSystemTypeName = (isCableTray ? "" : st.Name),
				ProjectCableTrayType = (isCableTray ? st.Name : ""),
				ProjectAbbreviation = GetAbbreviationFromSystemType(st),
				ProjectServiceType = GetServiceTypeFromSystemType(st),
				TemplateSystemTypeName = "",
				TemplateAbbreviation = "",
				TemplateServiceType = "",
				TemplateCableTrayType = ""
			});
		}
		dgCompare.ItemsSource = null;
		dgCompare.ItemsSource = _compareItems;
	}

	private Parameter GetAbbreviationParameter(Element elem)
	{
		Parameter p = elem.get_Parameter((BuiltInParameter)(-1140332));
		if (p == null)
		{
			p = elem.LookupParameter("Abbreviation");
		}
		return p;
	}

	private string GetAbbreviationFromSystemType(Element systemType)
	{
		try
		{
			Parameter p = GetAbbreviationParameter(systemType);
			if (p != null && p.HasValue && !string.IsNullOrEmpty(p.AsString()))
			{
				return p.AsString();
			}
		}
		catch
		{
		}
		return "";
	}

	private string GetServiceTypeFromSystemType(Element systemType)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Invalid comparison between Unknown and I4
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Invalid comparison between Unknown and I4
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Invalid comparison between Unknown and I4
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Invalid comparison between Unknown and I4
		try
		{
			Parameter p = systemType.LookupParameter("Service Type");
			if (p != null && p.HasValue)
			{
				if ((int)p.StorageType == 1)
				{
					return p.AsValueString() ?? "";
				}
				if ((int)p.StorageType == 3)
				{
					return p.AsString() ?? "";
				}
			}
			if (systemType is CableTrayType)
			{
				Element instance = ((IEnumerable<Element>)new FilteredElementCollector(systemType.Document).OfClass(typeof(CableTray))).Where((Element e) => e.GetTypeId() == systemType.Id).FirstOrDefault();
				if (instance != null)
				{
					Parameter pInst = instance.LookupParameter("Service Type");
					if (pInst != null && pInst.HasValue)
					{
						if ((int)pInst.StorageType == 1)
						{
							return pInst.AsValueString() ?? "";
						}
						if ((int)pInst.StorageType == 3)
						{
							return pInst.AsString() ?? "";
						}
					}
				}
			}
		}
		catch
		{
		}
		return "";
	}

	private void BtnPaste_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string clipboardData = Clipboard.GetText();
			if (string.IsNullOrEmpty(clipboardData))
			{
				return;
			}
			string[] lines = clipboardData.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			List<Tuple<string, string, string, string>> templateData = new List<Tuple<string, string, string, string>>();
			string[] array = lines;
			foreach (string line in array)
			{
				string[] cols = line.Split('\t');
				if (cols.Length != 0)
				{
					string name = cols[0].Trim();
					string abbr = ((cols.Length > 1) ? cols[1].Trim() : "");
					string svc = ((cols.Length > 2) ? cols[2].Trim() : "");
					string cableTray = ((cols.Length > 3) ? cols[3].Trim() : "");
					templateData.Add(new Tuple<string, string, string, string>(name, abbr, svc, cableTray));
				}
			}
			_compareItems.RemoveAll((SystemCompareItem x) => x.ElementType == null);
			HashSet<Tuple<string, string, string, string>> usedTemplateData = new HashSet<Tuple<string, string, string, string>>();
			foreach (SystemCompareItem item in _compareItems)
			{
				item.TemplateSystemTypeName = "";
				item.TemplateAbbreviation = "";
				item.TemplateServiceType = "";
				item.TemplateCableTrayType = "";
				Tuple<string, string, string, string> bestMatch = null;
				double maxScore = 0.0;
				foreach (Tuple<string, string, string, string> t in templateData)
				{
					bool isCableTray = item.ElementType is CableTrayType;
					string projTarget = (isCableTray ? item.ProjectCableTrayType : item.ProjectSystemTypeName);
					string tempTarget = (isCableTray ? t.Item4 : t.Item1);
					double score = CalculateSimilarity(projTarget, tempTarget);
					if (score > maxScore && !string.IsNullOrEmpty(tempTarget))
					{
						maxScore = score;
						bestMatch = t;
					}
				}
				if (maxScore >= 0.9 && bestMatch != null)
				{
					item.TemplateSystemTypeName = bestMatch.Item1;
					item.TemplateAbbreviation = bestMatch.Item2;
					item.TemplateServiceType = bestMatch.Item3;
					item.TemplateCableTrayType = bestMatch.Item4;
					usedTemplateData.Add(bestMatch);
				}
				item.CheckMatch();
			}
			foreach (Tuple<string, string, string, string> t2 in templateData)
			{
				if (!usedTemplateData.Contains(t2))
				{
					SystemCompareItem newItem = new SystemCompareItem
					{
						ElementType = null,
						ProjectSystemTypeName = "",
						ProjectAbbreviation = "",
						ProjectServiceType = "",
						ProjectCableTrayType = "",
						TemplateSystemTypeName = t2.Item1,
						TemplateAbbreviation = t2.Item2,
						TemplateServiceType = t2.Item3,
						TemplateCableTrayType = t2.Item4
					};
					newItem.CheckMatch();
					_compareItems.Add(newItem);
				}
			}
			dgCompare.ItemsSource = null;
			dgCompare.ItemsSource = _compareItems;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error pasting data: " + ex.Message);
		}
	}

	private double CalculateSimilarity(string s1, string s2)
	{
		if (string.IsNullOrEmpty(s1) && string.IsNullOrEmpty(s2))
		{
			return 1.0;
		}
		if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2))
		{
			return 0.0;
		}
		s1 = s1.Trim().ToLower();
		s2 = s2.Trim().ToLower();
		int len1 = s1.Length;
		int len2 = s2.Length;
		int[,] d = new int[len1 + 1, len2 + 1];
		for (int i = 0; i <= len1; i++)
		{
			d[i, 0] = i;
		}
		for (int j = 0; j <= len2; j++)
		{
			d[0, j] = j;
		}
		for (int k = 1; k <= len1; k++)
		{
			for (int l = 1; l <= len2; l++)
			{
				int cost = ((s2[l - 1] != s1[k - 1]) ? 1 : 0);
				d[k, l] = Math.Min(Math.Min(d[k - 1, l] + 1, d[k, l - 1] + 1), d[k - 1, l - 1] + cost);
			}
		}
		int maxLen = Math.Max(len1, len2);
		return 1.0 - (double)d[len1, len2] / (double)maxLen;
	}

	private void ApplyChange(SystemCompareItem item)
	{
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cd: Invalid comparison between Unknown and I4
		//IL_034f: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03bd: Invalid comparison between Unknown and I4
		try
		{
			if (item.ElementType == null)
			{
				ElementType newType = null;
				if (!string.IsNullOrEmpty(item.TemplateCableTrayType))
				{
					Element obj = new FilteredElementCollector(_doc).OfClass(typeof(CableTrayType)).FirstElement();
					ElementType existingCb = (ElementType)(object)((obj is ElementType) ? obj : null);
					if (existingCb != null)
					{
						newType = existingCb.Duplicate(item.TemplateCableTrayType);
					}
				}
				else if (!string.IsNullOrEmpty(item.TemplateSystemTypeName))
				{
					bool isMech = item.TemplateSystemTypeName.StartsWith("M-", StringComparison.OrdinalIgnoreCase);
					ElementType existing = null;
					if (isMech)
					{
						Element obj2 = new FilteredElementCollector(_doc).OfClass(typeof(MechanicalSystemType)).FirstElement();
						existing = (ElementType)(object)((obj2 is ElementType) ? obj2 : null);
					}
					if (existing == null)
					{
						Element obj3 = new FilteredElementCollector(_doc).OfClass(typeof(PipingSystemType)).FirstElement();
						existing = (ElementType)(object)((obj3 is ElementType) ? obj3 : null);
					}
					if (existing != null)
					{
						newType = existing.Duplicate(item.TemplateSystemTypeName);
					}
				}
				if (newType == null)
				{
					MessageBox.Show("Cannot create because no base types found in the project.");
					return;
				}
				item.ElementType = (Element)(object)newType;
				if (newType is CableTrayType)
				{
					item.ProjectCableTrayType = ((Element)newType).Name;
				}
				else
				{
					item.ProjectSystemTypeName = ((Element)newType).Name;
				}
			}
			bool isCableTray = item.ElementType is CableTrayType;
			string newName = (isCableTray ? item.TemplateCableTrayType : item.TemplateSystemTypeName);
			if (!string.IsNullOrEmpty(newName) && item.ElementType.Name != newName)
			{
				item.ElementType.Name = newName;
				if (isCableTray)
				{
					item.ProjectCableTrayType = newName;
				}
				else
				{
					item.ProjectSystemTypeName = newName;
				}
			}
			Parameter pAbbr = GetAbbreviationParameter(item.ElementType);
			if (pAbbr != null && !((APIObject)pAbbr).IsReadOnly)
			{
				pAbbr.Set(item.TemplateAbbreviation ?? "");
				item.ProjectAbbreviation = item.TemplateAbbreviation;
			}
			Parameter pSvc = item.ElementType.LookupParameter("Service Type");
			if (pSvc != null && !((APIObject)pSvc).IsReadOnly)
			{
				if ((int)pSvc.StorageType == 1)
				{
					pSvc.SetValueString(item.TemplateServiceType ?? "");
				}
				else
				{
					pSvc.Set(item.TemplateServiceType ?? "");
				}
				item.ProjectServiceType = item.TemplateServiceType;
			}
			else if (item.ElementType is CableTrayType)
			{
				IEnumerable<Element> instances = ((IEnumerable<Element>)new FilteredElementCollector(_doc).OfClass(typeof(CableTray))).Where((Element e) => e.GetTypeId() == item.ElementType.Id);
				bool changedInstance = false;
				foreach (Element inst in instances)
				{
					Parameter pInst = inst.LookupParameter("Service Type");
					if (pInst != null && !((APIObject)pInst).IsReadOnly)
					{
						if ((int)pInst.StorageType == 1)
						{
							pInst.SetValueString(item.TemplateServiceType ?? "");
						}
						else
						{
							pInst.Set(item.TemplateServiceType ?? "");
						}
						changedInstance = true;
					}
				}
				if (changedInstance || !instances.Any())
				{
					item.ProjectServiceType = item.TemplateServiceType;
				}
			}
			item.CheckMatch();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error changing " + item.ProjectSystemTypeName + ": " + ex.Message);
		}
	}

	private void BtnChange_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button { DataContext: var dataContext }))
		{
			return;
		}
		SystemCompareItem item = dataContext as SystemCompareItem;
		if (item == null)
		{
			return;
		}
		ExecuteApi(delegate
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Expected O, but got Unknown
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0032: Unknown result type (might be due to invalid IL or missing references)
			Transaction val = new Transaction(_doc, "Change System Type Attributes");
			try
			{
				val.Start();
				ApplyChange(item);
				val.Commit();
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		});
	}

	private void BtnChangeTo_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button { DataContext: var dataContext } btn))
		{
			return;
		}
		SystemCompareItem item = dataContext as SystemCompareItem;
		if (item == null)
		{
			return;
		}
		ContextMenu cm = new ContextMenu();
		var allTemplateItems = (from x in _compareItems
			where !string.IsNullOrEmpty(x.TemplateSystemTypeName) || !string.IsNullOrEmpty(x.TemplateCableTrayType)
			select new { x.TemplateSystemTypeName, x.TemplateCableTrayType, x.TemplateAbbreviation, x.TemplateServiceType }).Distinct().ToList();
		if (allTemplateItems.Count == 0)
		{
			MenuItem emptyItem = new MenuItem
			{
				Header = "No available Excel targets",
				IsEnabled = false
			};
			cm.Items.Add(emptyItem);
		}
		else
		{
			foreach (var tItem in allTemplateItems)
			{
				string tName = ((!string.IsNullOrEmpty(tItem.TemplateCableTrayType)) ? tItem.TemplateCableTrayType : tItem.TemplateSystemTypeName);
				string tAbbr = tItem.TemplateAbbreviation;
				string tSvc = tItem.TemplateServiceType;
				MenuItem mi = new MenuItem();
				mi.Header = tName + " | " + tAbbr + " | " + tSvc;
				mi.Click += delegate
				{
					item.TemplateSystemTypeName = tItem.TemplateSystemTypeName;
					item.TemplateCableTrayType = tItem.TemplateCableTrayType;
					item.TemplateAbbreviation = tItem.TemplateAbbreviation;
					item.TemplateServiceType = tItem.TemplateServiceType;
					item.CheckMatch();
					SystemCompareItem systemCompareItem = _compareItems.FirstOrDefault((SystemCompareItem x) => x.ElementType == null && x.TemplateSystemTypeName == tItem.TemplateSystemTypeName && x.TemplateCableTrayType == tItem.TemplateCableTrayType && x.TemplateAbbreviation == tItem.TemplateAbbreviation && x.TemplateServiceType == tItem.TemplateServiceType);
					if (systemCompareItem != null)
					{
						_compareItems.Remove(systemCompareItem);
					}
					dgCompare.ItemsSource = null;
					dgCompare.ItemsSource = _compareItems;
				};
				cm.Items.Add(mi);
			}
		}
		cm.PlacementTarget = btn;
		cm.IsOpen = true;
	}

	private void BtnChangeAll_Click(object sender, RoutedEventArgs e)
	{
		List<SystemCompareItem> itemsToChange = _compareItems.Where((SystemCompareItem x) => x.CanChange).ToList();
		if (itemsToChange.Count == 0)
		{
			return;
		}
		ExecuteApi(delegate
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Expected O, but got Unknown
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			Transaction val = new Transaction(_doc, "Change All System Type Attributes");
			try
			{
				val.Start();
				foreach (SystemCompareItem current in itemsToChange)
				{
					ApplyChange(current);
				}
				val.Commit();
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		});
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/40.%20compare%20element/compareelementwindow.xaml", UriKind.Relative);
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
			((Button)target).Click += BtnPaste_Click;
			break;
		case 2:
			dgCompare = (DataGrid)target;
			break;
		case 5:
			((Button)target).Click += BtnChangeAll_Click;
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
		case 3:
			((Button)target).Click += BtnChange_Click;
			break;
		case 4:
			((Button)target).Click += BtnChangeTo_Click;
			break;
		}
	}
}
