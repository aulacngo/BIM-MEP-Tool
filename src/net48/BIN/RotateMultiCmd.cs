using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace BIN;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class RotateMultiCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
			List<Element> list = new List<Element>();

			if (selectedIds.Count > 0)
			{
				list = selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
			}
			else
			{
				IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, "Qu\u00e9t ch\u1ecdn c\u00e1c C\u00fat / Ph\u1ee5 ki\u1ec7n tr\u00ean c\u00e1c tuy\u1ebfn \u1ed1ng song song \u0111\u1ec3 xoay \u0111\u1ed3ng lo\u1ea1t");
				if (refs == null || refs.Count == 0) return Result.Cancelled;
				list = refs.Select(r => doc.GetElement(r)).Where(e => e != null).ToList();
			}

			if (list.Count == 0) return Result.Cancelled;

			List<RotationGroup> groups = DetectLocalRotationGroups(doc, list);

			if (groups.Count == 0)
			{
				TaskDialog.Show("Th\u00f4ng b\u00e1o", "Kh\u00f4ng t\u1ef1 \u0111\u1ed9ng t\u00ecm th\u1ea5y tuy\u1ebfn \u1ed1ng tr\u1ee5c li\u00ean k\u1ebft cho c\u00e1c c\u00fat \u0111\u01b0\u1ee3c ch\u1ecdn. Vui l\u00f2ng th\u1eed d\u00f9ng l\u1ec7nh Rotate \u0111\u01a1n l\u1ebb.");
				return Result.Cancelled;
			}

			RotateElementsWindow window = new RotateElementsWindow(uidoc, groups, true);
			IntPtr mainHandle = commandData.Application.MainWindowHandle;
			if (mainHandle != IntPtr.Zero)
			{
				new WindowInteropHelper(window).Owner = mainHandle;
			}
			window.ShowDialog();

			return Result.Succeeded;
		}
		catch (Autodesk.Revit.Exceptions.OperationCanceledException)
		{
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return Result.Failed;
		}
	}

	private List<RotationGroup> DetectLocalRotationGroups(Document doc, List<Element> selectedElements)
	{
		List<RotationGroup> groups = new List<RotationGroup>();
		HashSet<ElementId> processedElements = new HashSet<ElementId>();

		List<FamilyInstance> rotatingFittings = selectedElements.OfType<FamilyInstance>().ToList();

		if (rotatingFittings.Count == 0)
		{
			foreach (MEPCurve p in selectedElements.OfType<MEPCurve>())
			{
				Connector[] conns = NaviateHelper.ConnectorArray(p);
				if (conns != null)
				{
					foreach (Connector c in conns)
					{
						foreach (Connector other in c.AllRefs)
						{
							if (other.Owner is FamilyInstance fi && !rotatingFittings.Contains(fi))
							{
								rotatingFittings.Add(fi);
							}
						}
					}
				}
			}
		}

		foreach (FamilyInstance fi in rotatingFittings)
		{
			if (processedElements.Contains(fi.Id)) continue;

			List<Element> connectedHosts = new List<Element>();
			if (fi.MEPModel?.ConnectorManager != null)
			{
				foreach (Connector c in fi.MEPModel.ConnectorManager.Connectors)
				{
					if (c.IsConnected)
					{
						foreach (Connector other in c.AllRefs)
						{
							if (other.Owner != null && other.Owner.Id != fi.Id && !connectedHosts.Contains(other.Owner))
							{
								connectedHosts.Add(other.Owner);
							}
						}
					}
				}
			}

			if (connectedHosts.Count == 0) continue;

			Element hostElem = connectedHosts.OfType<MEPCurve>()
				.OrderByDescending(p => (p.Location as LocationCurve)?.Curve?.Length ?? 0)
				.FirstOrDefault();

			if (hostElem == null)
			{
				hostElem = connectedHosts.OfType<FamilyInstance>().FirstOrDefault();
			}

			if (hostElem != null)
			{
				Line axisLine = AxisHelper.GetAxisLine(hostElem);
				if (axisLine != null)
				{
					List<ElementId> branchIds = CollectConnectedBranch(doc, fi, hostElem.Id);
					foreach (var bid in branchIds) processedElements.Add(bid);

					groups.Add(new RotationGroup
					{
						AxisHostId = hostElem.Id,
						AxisLine = axisLine,
						ElementIds = branchIds
					});
				}
			}
		}

		if (groups.Count > 1)
		{
			XYZ baseDir = groups[0].AxisLine.Direction;
			foreach (var g in groups)
			{
				if (g.AxisLine.Direction.DotProduct(baseDir) < 0)
				{
					g.AxisLine = Line.CreateUnbound(g.AxisLine.Origin, -g.AxisLine.Direction);
				}
			}
		}

		return groups;
	}

	private List<ElementId> CollectConnectedBranch(Document doc, Element rootElem, ElementId axisPipeId)
	{
		HashSet<ElementId> visited = new HashSet<ElementId> { rootElem.Id };
		Queue<Element> queue = new Queue<Element>();
		queue.Enqueue(rootElem);

		while (queue.Count > 0)
		{
			Element current = queue.Dequeue();
			ConnectorSet conns = null;
			if (current is MEPCurve mepCurve)
			{
				conns = mepCurve.ConnectorManager?.Connectors;
			}
			else if (current is FamilyInstance fi)
			{
				conns = fi.MEPModel?.ConnectorManager?.Connectors;
			}

			if (conns == null) continue;

			foreach (Connector c in conns)
			{
				if (!c.IsConnected) continue;
				foreach (Connector other in c.AllRefs)
				{
					if (other.Owner == null) continue;
					Element neighbor = other.Owner;
					if (neighbor.Id == axisPipeId) continue;
					if (!visited.Contains(neighbor.Id) && (neighbor is MEPCurve || neighbor is FamilyInstance))
					{
						visited.Add(neighbor.Id);
						queue.Enqueue(neighbor);
					}
				}
			}
		}
		return visited.ToList();
	}
}