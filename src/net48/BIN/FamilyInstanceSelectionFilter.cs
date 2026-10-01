using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class FamilyInstanceSelectionFilter : ISelectionFilter
{
	private string _fam = "";

	private string _typ = "";

	public FamilyInstanceSelectionFilter()
	{
	}

	public FamilyInstanceSelectionFilter(string f, string t)
	{
		_fam = f;
		_typ = t;
	}

	public bool AllowElement(Element elem)
	{
		FamilyInstance fi = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
		if (fi == null)
		{
			return false;
		}
		if (string.IsNullOrEmpty(_fam))
		{
			return true;
		}
		return ((Element)fi.Symbol.Family).Name == _fam && ((Element)fi.Symbol).Name == _typ;
	}

	public bool AllowReference(Reference r, XYZ p)
	{
		return false;
	}
}
