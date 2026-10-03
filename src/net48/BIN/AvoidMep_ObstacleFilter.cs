using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace BIN;

public class AvoidMep_ObstacleFilter : ISelectionFilter
{
    private readonly Document _document;
    public AvoidMep_ObstacleFilter(Document document) { _document = document; }

    public bool AllowElement(Element element)
        => element is RevitLinkInstance link ? link.GetLinkDocument() != null : IsObstacle(element);

    public bool AllowReference(Reference reference, XYZ position)
    {
        if (reference == null) return false;
        if (reference.LinkedElementId != null && !ElementId.InvalidElementId.Equals(reference.LinkedElementId))
        {
            var link = _document.GetElement(reference.ElementId) as RevitLinkInstance;
            return IsObstacle(link?.GetLinkDocument()?.GetElement(reference.LinkedElementId));
        }
        return IsObstacle(_document.GetElement(reference.ElementId));
    }

    // Solid extraction subsequently requires closed 3D geometry. Annotation,
    // datum, links themselves and element types are not physical obstacles.
    internal static bool IsObstacle(Element element)
        => element != null && !(element is RevitLinkInstance) && !(element is ElementType)
            && !element.ViewSpecific && element.Category?.CategoryType == CategoryType.Model;
}
