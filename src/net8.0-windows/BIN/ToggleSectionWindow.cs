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
using Autodesk.Revit.DB;

namespace BIN;

public class ToggleSectionWindow : Window, IComponentConnector
{
	private Document _doc;

	internal ComboBox cmbHead;

	internal ComboBox cmbTail;

	internal Button btnApplyInstance;

	internal Button btnApplyType;

	private bool _contentLoaded;

	public ElementId SelectedHeadId { get; private set; }

	public ElementId SelectedTailId { get; private set; }

	public bool ApplyToInstance { get; private set; }

	public ToggleSectionWindow(Document doc)
	{
		InitializeComponent();
		_doc = doc;
		LoadData();
	}

	private void LoadData()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		List<LabelItem> symbols = (from FamilySymbol x in (IEnumerable)new FilteredElementCollector(_doc).OfClass(typeof(FamilySymbol)).OfCategory((BuiltInCategory)(-2000400))
			select new LabelItem
			{
				Id = ((Element)x).Id,
				Name = ((ElementType)x).FamilyName + " : " + ((Element)x).Name
			} into x
			orderby x.Name
			select x).ToList();
		List<LabelItem> headList = new List<LabelItem>
		{
			new LabelItem
			{
				Id = ElementId.InvalidElementId,
				Name = "<None>"
			}
		};
		headList.AddRange(symbols);
		List<LabelItem> tailList = new List<LabelItem>
		{
			new LabelItem
			{
				Id = ElementId.InvalidElementId,
				Name = "<None>"
			}
		};
		tailList.AddRange(symbols);
		cmbHead.ItemsSource = headList;
		cmbTail.ItemsSource = tailList;
		cmbHead.SelectedIndex = 0;
		cmbTail.SelectedIndex = 0;
	}

	private void btnApplyInstance_Click(object sender, RoutedEventArgs e)
	{
		SelectedHeadId = ((LabelItem)cmbHead.SelectedItem).Id;
		SelectedTailId = ((LabelItem)cmbTail.SelectedItem).Id;
		ApplyToInstance = true;
		base.DialogResult = true;
		Close();
	}

	private void btnApplyType_Click(object sender, RoutedEventArgs e)
	{
		SelectedHeadId = ((LabelItem)cmbHead.SelectedItem).Id;
		SelectedTailId = ((LabelItem)cmbTail.SelectedItem).Id;
		ApplyToInstance = false;
		base.DialogResult = true;
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.27.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocater = new Uri("/BIN;component/42.%20toggle%20section%20head/togglesectionwindow.xaml", UriKind.Relative);
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
			cmbHead = (ComboBox)target;
			break;
		case 2:
			cmbTail = (ComboBox)target;
			break;
		case 3:
			btnApplyInstance = (Button)target;
			btnApplyInstance.Click += btnApplyInstance_Click;
			break;
		case 4:
			btnApplyType = (Button)target;
			btnApplyType.Click += btnApplyType_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
