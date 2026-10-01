using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class SelectFloor : ISelectionFilter
{
	public bool AllowElement(Element element)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Invalid comparison between Unknown and I4
		BuiltInCategory builtInCategory = (BuiltInCategory)element.Category.GetIdInt();
		if ((int)builtInCategory == -2000032)
		{
			return true;
		}
		return false;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return false;
	}
}
