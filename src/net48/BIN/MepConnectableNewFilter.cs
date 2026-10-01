using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class MepConnectableNewFilter : ISelectionFilter
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
		if (num2 <= -2008044)
		{
			if (num2 <= -2008126)
			{
				if (num2 != -2008208 && num2 != -2008193)
				{
					long num3 = num2 - -2008132;
					if ((ulong)num3 > 6uL)
					{
						goto IL_018a;
					}
					switch (num3)
					{
					case 0L:
					case 2L:
					case 4L:
					case 6L:
						break;
					default:
						goto IL_018a;
					}
				}
				goto IL_0140;
			}
			if (num2 <= -2008055)
			{
				if (num2 == -2008099)
				{
					return PreviousElementID == (ElementId)null || e.Id != PreviousElementID;
				}
				if (num2 == -2008055)
				{
					goto IL_0140;
				}
			}
			else if ((ulong)(num2 - -2008050) <= 1uL || num2 == -2008044)
			{
				goto IL_0140;
			}
		}
		else if (num2 <= -2008013)
		{
			if (num2 == -2008020 || num2 == -2008016 || num2 == -2008013)
			{
				goto IL_0140;
			}
		}
		else if (num2 <= -2008000)
		{
			if (num2 == -2008010 || num2 == -2008000)
			{
				goto IL_0140;
			}
		}
		else if (num2 == -2001160 || num2 == -2001140)
		{
			goto IL_0140;
		}
		goto IL_018a;
		IL_018a:
		return false;
		IL_0140:
		return PreviousElementID == (ElementId)null || e.Id != PreviousElementID;
	}

	public bool AllowReference(Reference refer, XYZ point)
	{
		return false;
	}
}
