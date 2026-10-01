using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class FlangeSelectionFilter : ISelectionFilter
{
	public bool AllowElement(Element elem)
	{
		FamilyInstance instance = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
		if (instance != null && ((Element)instance).Category.GetIdInt() == -2008049)
		{
			Family family = instance.Symbol.Family;
			Parameter partTypeParam = ((Element)family).get_Parameter((BuiltInParameter)(-1114206));
			if (partTypeParam != null)
			{
				int pType = partTypeParam.AsInteger();
				return pType == 32 || pType == 32;
			}
		}
		return false;
	}

	public bool AllowReference(Reference reference, XYZ position)
	{
		return false;
	}
}
