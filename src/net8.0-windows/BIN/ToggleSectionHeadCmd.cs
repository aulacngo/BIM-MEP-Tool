using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
public class ToggleSectionHeadCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_05b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Expected O, but got Unknown
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0588: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Unknown result type (might be due to invalid IL or missing references)
		//IL_040c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0413: Expected O, but got Unknown
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		ToggleSectionWindow dialog = new ToggleSectionWindow(doc);
		if (dialog.ShowDialog() == true)
		{
			ElementId headId = dialog.SelectedHeadId;
			ElementId tailId = dialog.SelectedTailId;
			bool isInstance = dialog.ApplyToInstance;
			IList<Reference> refs;
			try
			{
				refs = uidoc.Selection.PickObjects((ObjectType)1, (ISelectionFilter)(object)new SectionSelectionFilter(), "Please select Section lines.");
			}
			catch (Autodesk.Revit.Exceptions.OperationCanceledException)
			{
				return Result.Cancelled;
			}
			if (refs == null || refs.Count == 0)
			{
				return Result.Cancelled;
			}
			List<ViewSection> sections = refs.Select((Reference r) => doc.GetElement(r)).OfType<ViewSection>().ToList();
			if (sections.Count == 0)
			{
				foreach (Reference r2 in refs)
				{
					Element el = doc.GetElement(r2);
					Element obj = el;
					ViewSection vs = (ViewSection)(object)((obj is ViewSection) ? obj : null);
					if (vs != null)
					{
						sections.Add(vs);
					}
					else if (!string.IsNullOrEmpty(el.Name))
					{
						ViewSection viewSection = ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(ViewSection))).Cast<ViewSection>().FirstOrDefault((ViewSection v) => ((Element)v).Name == el.Name);
						if (viewSection != null)
						{
							sections.Add(viewSection);
						}
					}
				}
				if (sections.Count == 0)
				{
					TaskDialog.Show("BIM Tool", "No valid Section lines were selected.");
					return Result.Failed;
				}
			}
			try
			{
				Transaction t = new Transaction(doc, isInstance ? "Toggle Section Head - Instance" : "Toggle Section Head - Type");
				try
				{
					t.Start();
					if (isInstance)
					{
						foreach (ViewSection section in sections)
						{
							Element element = doc.GetElement(((Element)section).GetTypeId());
							ViewFamilyType type = (ViewFamilyType)(object)((element is ViewFamilyType) ? element : null);
							if (type == null)
							{
								continue;
							}
							Parameter obj2 = ((Element)type).get_Parameter((BuiltInParameter)(-1008205));
							ElementId tagId = ((obj2 != null) ? obj2.AsElementId() : null);
							ElementType currentTag = null;
							if (tagId != (ElementId)null && tagId != ElementId.InvalidElementId)
							{
								Element element2 = doc.GetElement(tagId);
								currentTag = (ElementType)(object)((element2 is ElementType) ? element2 : null);
							}
							string newTagName = "Tag_Head_" + ((object)headId).ToString() + "_Tail_" + ((object)tailId).ToString();
							ElementType newTag = FindSectionTagByName(doc, newTagName);
							if (newTag == null)
							{
								if (currentTag != null)
								{
									newTag = currentTag.Duplicate(newTagName);
								}
								else
								{
									ElementType defaultTag = ((IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(ElementType))).Where((Element x) => x is ElementType && x.get_Parameter((BuiltInParameter)(-1006600)) != null).Cast<ElementType>().FirstOrDefault();
									if (defaultTag != null)
									{
										newTag = defaultTag.Duplicate(newTagName);
									}
								}
								if (newTag != null)
								{
									Parameter obj3 = ((Element)newTag).get_Parameter((BuiltInParameter)(-1006600));
									if (obj3 != null)
									{
										obj3.Set(headId);
									}
									Parameter obj4 = ((Element)newTag).get_Parameter((BuiltInParameter)(-1006608));
									if (obj4 != null)
									{
										obj4.Set(tailId);
									}
								}
							}
							if (newTag == null)
							{
								continue;
							}
							string newTypeName = ((Element)type).Name + "_H" + ((object)headId).ToString() + "_T" + ((object)tailId).ToString();
							ViewFamilyType newType = FindViewFamilyTypeByName(doc, newTypeName);
							if (newType == null)
							{
								newType = (ViewFamilyType)((ElementType)type).Duplicate(newTypeName);
								Parameter obj5 = ((Element)newType).get_Parameter((BuiltInParameter)(-1008205));
								if (obj5 != null)
								{
									obj5.Set(((Element)newTag).Id);
								}
							}
							((Element)section).ChangeTypeId(((Element)newType).Id);
						}
					}
					else
					{
						List<ElementId> typesToModify = sections.Select((ViewSection x) => ((Element)x).GetTypeId()).Distinct().ToList();
						foreach (ElementId typeId in typesToModify)
						{
							Element element3 = doc.GetElement(typeId);
							ViewFamilyType type2 = (ViewFamilyType)(object)((element3 is ViewFamilyType) ? element3 : null);
							if (type2 == null)
							{
								continue;
							}
							Parameter obj6 = ((Element)type2).get_Parameter((BuiltInParameter)(-1008205));
							ElementId tagId2 = ((obj6 != null) ? obj6.AsElementId() : null);
							if (!(tagId2 != (ElementId)null) || !(tagId2 != ElementId.InvalidElementId))
							{
								continue;
							}
							Element element4 = doc.GetElement(tagId2);
							ElementType currentTag2 = (ElementType)(object)((element4 is ElementType) ? element4 : null);
							if (currentTag2 != null)
							{
								Parameter obj7 = ((Element)currentTag2).get_Parameter((BuiltInParameter)(-1006600));
								if (obj7 != null)
								{
									obj7.Set(headId);
								}
								Parameter obj8 = ((Element)currentTag2).get_Parameter((BuiltInParameter)(-1006608));
								if (obj8 != null)
								{
									obj8.Set(tailId);
								}
							}
						}
					}
					t.Commit();
				}
				finally
				{
					((IDisposable)t)?.Dispose();
				}
			}
			catch (Exception ex)
			{
				TaskDialog.Show("Error", ex.Message);
				return Result.Failed;
			}
			return Result.Succeeded;
		}
		return Result.Cancelled;
	}

	private ElementType FindSectionTagByName(Document doc, string name)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return ((IEnumerable<Element>)new FilteredElementCollector(doc).OfClass(typeof(ElementType))).Where((Element x) => x.get_Parameter((BuiltInParameter)(-1006600)) != null).Cast<ElementType>().FirstOrDefault((ElementType x) => ((Element)x).Name == name);
	}

	private ViewFamilyType FindViewFamilyTypeByName(Document doc, string name)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		return ((IEnumerable)new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))).Cast<ViewFamilyType>().FirstOrDefault((ViewFamilyType x) => ((Element)x).Name == name);
	}
}
