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

public class RotationGroup
{
	public List<ElementId> ElementIds { get; set; } = new List<ElementId>();
	public Line AxisLine { get; set; }
	public ElementId AxisHostId { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class RotateElementsCmd : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		UIDocument uidoc = commandData.Application.ActiveUIDocument;
		Document doc = uidoc.Document;
		try
		{
			ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
			List<Element> list = new List<Element>();

			if (selectedIds.Count > 1)
			{
				list = selectedIds.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
				List<RotationGroup> multiGroups = DetectLocalRotationGroups(doc, list);
				if (multiGroups.Count > 0)
				{
					RotateElementsWindow multiWin = new RotateElementsWindow(uidoc, multiGroups, true);
					IntPtr h = commandData.Application.MainWindowHandle;
					if (h != IntPtr.Zero) new WindowInteropHelper(multiWin).Owner = h;
					multiWin.ShowDialog();
					return Result.Succeeded;
				}
			}

			Element targetRotatingElem = null;
			if (selectedIds.Count == 1)
			{
				targetRotatingElem = doc.GetElement(selectedIds.First());
			}
			else
			{
				Reference rElem = uidoc.Selection.PickObject(ObjectType.Element, "1. Click ch\u1ecdn C\u00fat / Ph\u1ee5 ki\u1ec7n / \u1ed0ng c\u1ea7n xoay");
				if (rElem == null) return Result.Cancelled;
				targetRotatingElem = doc.GetElement(rElem);
			}

			if (targetRotatingElem == null) return Result.Cancelled;

			Reference rAxis = uidoc.Selection.PickObject(ObjectType.Element, new AxisSelectionFilter(), "2. Click ch\u1ecdn \u1ed0NG ho\u1eb7c FITTING l\u00e0m TR\u1ee4C XOAY (Axis)");
			if (rAxis == null) return Result.Cancelled;

			Element axisElem = doc.GetElement(rAxis);
			Line axisLine = AxisHelper.GetAxisLine(axisElem, rAxis.GlobalPoint);

			if (axisLine == null)
			{
				TaskDialog.Show("Error", "Kh\u00f4ng th\u1ec3 x\u00e1c \u0111\u1ecbnh \u0111\u01b0\u1eddng t\u00e2m tr\u1ee5c xoay c\u1ee7a \u0111\u1ed1i t\u01b0\u1ee3ng \u0111\u01b0\u1ee3c ch\u1ecdn.");
				return Result.Failed;
			}

			List<ElementId> branchIds = CollectConnectedBranch(doc, targetRotatingElem, axisElem.Id);
			if (branchIds.Count == 0)
			{
				TaskDialog.Show("Th\u00f4ng b\u00e1o", "Kh\u00f4ng c\u00f3 \u0111\u1ed1i t\u01b0\u1ee3ng n\u00e0o h\u1ee3p l\u1ec7 \u0111\u1ec3 xoay quanh tr\u1ee5c.");
				return Result.Cancelled;
			}

			var singleGroups = new List<RotationGroup>
			{
				new RotationGroup
				{
					AxisHostId = axisElem.Id,
					AxisLine = axisLine,
					ElementIds = branchIds
				}
			};

			RotateElementsWindow window = new RotateElementsWindow(uidoc, singleGroups, false);
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