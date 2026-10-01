using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class MepConnectableNoFabFilter : ISelectionFilter
{
	public ElementId PreviousElementID { get; set; }

	public bool AllowElement(Element e)
	{
		if (e == null || e.Category == null)
		{
			return false;
		}
		long catId = ElementIdHelper.GetIdValue(e.Category.Id);
		long num = catId;
		long num2 = num;
		if (num2 <= -2008020)
		{
			if (num2 <= -2008055)
			{
				long num3 = num2 - -2008132;
				if ((ulong)num3 <= 6uL)
				{
					switch (num3)
					{
					case 0L:
					case 2L:
					case 4L:
					case 6L:
						goto IL_0103;
					case 1L:
					case 3L:
					case 5L:
						goto IL_012c;
					}
				}
				if (num2 == -2008099)
				{
					return PreviousElementID == (ElementId)null || e.Id != PreviousElementID;
				}
				if (num2 == -2008055)
				{
					goto IL_0103;
				}
			}
			else if ((ulong)(num2 - -2008050) <= 1uL || num2 == -2008044 || num2 == -2008020)
			{
				goto IL_0103;
			}
		}
		else if (num2 <= -2008010)
		{
			if (num2 == -2008016 || num2 == -2008013 || num2 == -2008010)
			{
				goto IL_0103;
			}
		}
		else if (num2 == -2008000 || num2 == -2001160 || num2 == -2001140)
		{
			goto IL_0103;
		}
		goto IL_012c;
		IL_0103:
		return true;
		IL_012c:
		return false;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return false;
	}
}
