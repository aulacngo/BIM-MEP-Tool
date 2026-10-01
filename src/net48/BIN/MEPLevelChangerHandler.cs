using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BIN;

public class MEPLevelChangerHandler : IExternalEventHandler
{
	private MEPLevelChangerViewModel _vm;

	public void SetViewModel(MEPLevelChangerViewModel vm)
	{
		_vm = vm;
	}

	public void Execute(UIApplication uiapp)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		Document doc = uiapp.ActiveUIDocument.Document;
		List<BuiltInCategory> selectedCats = (from c in _vm.Categories
			where c.IsChecked
			select c.CategoryId).ToList();
		if (!selectedCats.Any() || _vm.SelectedNewLevel == null)
		{
			return;
		}
		List<Element> collector = CollectElements(uiapp.ActiveUIDocument, selectedCats);
		if (!collector.Any())
		{
			MessageBox.Show("Kh\ufffdng t\ufffdm th?y d?i tu?ng n\ufffdo.");
			return;
		}
		int count = 0;
		Transaction t = new Transaction(doc, "BIN_ChangeLevel");
		try
		{
			t.Start();
			foreach (Element el in collector)
			{
				if (ProcessElement(doc, el, _vm.SelectedNewLevel))
				{
					count++;
				}
			}
			t.Commit();
		}
		finally
		{
			((IDisposable)t)?.Dispose();
		}
		MessageBox.Show($"Th\ufffdnh c\ufffdng! \ufffd\ufffd chuy?n {count} d?i tu?ng.");
	}

	private List<Element> CollectElements(UIDocument uidoc, List<BuiltInCategory> cats)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Expected O, but got Unknown
		ElementMulticategoryFilter filter = new ElementMulticategoryFilter((ICollection<BuiltInCategory>)cats);
		if (_vm.IsScopeSelection)
		{
			return (from id in uidoc.Selection.GetElementIds()
				select uidoc.Document.GetElement(id) into e
				where ((e != null) ? e.Category : null) != null && cats.Contains((BuiltInCategory)e.Category.GetIdInt())
				select e).ToList();
		}
		FilteredElementCollector collector = (_vm.IsScopeActiveView ? new FilteredElementCollector(uidoc.Document, ((Element)uidoc.Document.ActiveView).Id) : new FilteredElementCollector(uidoc.Document));
		if (_vm.IsScopeSpecificLevel && _vm.SelectedSourceLevel != null)
		{
			collector.WherePasses((ElementFilter)new ElementLevelFilter(((Element)_vm.SelectedSourceLevel).Id));
		}
		return collector.WherePasses((ElementFilter)(object)filter).WhereElementIsNotElementType().ToElements()
			.ToList();
	}

	private bool ProcessElement(Document doc, Element e, Level newLevel)
	{
		try
		{
			BuiltInParameter[] array = new BuiltInParameter[]
			{
				BuiltInParameter.RBS_START_LEVEL_PARAM,
				BuiltInParameter.LEVEL_PARAM,
				BuiltInParameter.SCHEDULE_LEVEL_PARAM
			};
			Parameter pLevel = GetParam(e, array);
			if (pLevel == null || ((APIObject)pLevel).IsReadOnly)
			{
				return false;
			}
			ElementId oldLevelId = pLevel.AsElementId();
			Element element = doc.GetElement(oldLevelId);
			Level oldLevel = (Level)(object)((element is Level) ? element : null);
			if (oldLevel == null || ((Element)oldLevel).Id == ((Element)newLevel).Id)
			{
				return false;
			}
			double deltaZ = oldLevel.Elevation - newLevel.Elevation;
			BuiltInParameter[] array2 = new BuiltInParameter[]
			{
				BuiltInParameter.RBS_OFFSET_PARAM,
				BuiltInParameter.RBS_START_OFFSET_PARAM,
				BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM
			};
			Parameter pOffset = GetParam(e, array2);
			if (pOffset != null && !((APIObject)pOffset).IsReadOnly)
			{
				double newVal = pOffset.AsDouble() + deltaZ;
				pLevel.Set(((Element)newLevel).Id);
				pOffset.Set(newVal);
				Parameter pEnd = e.get_Parameter((BuiltInParameter)(-1114003));
				if (pEnd != null && !((APIObject)pEnd).IsReadOnly)
				{
					pEnd.Set(pEnd.AsDouble() + deltaZ);
				}
				return true;
			}
			return pLevel.Set(((Element)newLevel).Id);
		}
		catch
		{
			return false;
		}
	}

	private Parameter GetParam(Element e, params BuiltInParameter[] bis)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		foreach (BuiltInParameter bi in bis)
		{
			Parameter p = e.get_Parameter(bi);
			if (p != null)
			{
				return p;
			}
		}
		return null;
	}

	public string GetName()
	{
		return "BIN_MEPLevelHandler";
	}
}
