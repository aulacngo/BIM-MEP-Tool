using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;

namespace BIN;

internal sealed class DuctProfileData
{
    internal ConnectorProfileType Shape { get; private set; }
    internal double Width { get; private set; }
    internal double Height { get; private set; }
    internal double Diameter { get; private set; }
    internal double ExtensionLength => Math.Max(1.0, 4.0 *
        (Shape == ConnectorProfileType.Round ? Diameter : Math.Max(Width, Height)));

    internal static DuctProfileData Read(Duct duct, Connector connector)
    {
        var profile = new DuctProfileData { Shape = connector.Shape };
        if (duct.DuctType.Shape != profile.Shape)
            throw new InvalidOperationException("Duct type and connector profile shapes do not match.");
        if (profile.Shape == ConnectorProfileType.Round)
            profile.Diameter = ReadSize(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
        else if (profile.Shape == ConnectorProfileType.Rectangular || profile.Shape == ConnectorProfileType.Oval)
        {
            profile.Width = ReadSize(duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
            profile.Height = ReadSize(duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
        }
        else
            throw new InvalidOperationException("Unsupported duct profile: " + profile.Shape);
        return profile;
    }

    internal void Apply(Duct duct)
    {
        if (Shape == ConnectorProfileType.Round)
            SetSize(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM, Diameter);
        else
        {
            SetSize(duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM, Width);
            SetSize(duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM, Height);
        }
    }

    internal void Verify(Duct duct, Connector connector)
    {
        DuctProfileData actual = Read(duct, connector);
        if (actual.Shape != Shape || Math.Abs(actual.Width - Width) > 1e-6 ||
            Math.Abs(actual.Height - Height) > 1e-6 || Math.Abs(actual.Diameter - Diameter) > 1e-6)
            throw new InvalidOperationException("Duct shape or size changed (including possible Width/Height swap).");
    }

    internal static ElementId SystemTypeId(Document doc, Duct duct, Connector connector)
    {
        Parameter parameter = duct.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
        ElementId id = parameter != null && parameter.StorageType == StorageType.ElementId
            ? parameter.AsElementId() : ElementId.InvalidElementId;
        if (doc.GetElement(id) is MechanicalSystemType) return id;
        MEPSystem system = duct.MEPSystem ?? connector.MEPSystem;
        if (system != null && doc.GetElement(system.GetTypeId()) is MechanicalSystemType)
            return system.GetTypeId();
        throw new InvalidOperationException("Cannot resolve the source MechanicalSystemType. Assign a system type to the duct first.");
    }

    internal static ElementId LevelId(Document doc, Duct duct, double elevation)
    {
        Level level = duct.ReferenceLevel ?? doc.GetElement(duct.LevelId) as Level;
        if (level == null)
            level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .OrderBy(candidate => Math.Abs(candidate.ProjectElevation - elevation)).FirstOrDefault();
        if (level == null) throw new InvalidOperationException("No valid reference level is available.");
        return level.Id;
    }

    private static double ReadSize(Duct duct, BuiltInParameter name)
    {
        Parameter parameter = duct.get_Parameter(name);
        if (parameter == null || parameter.StorageType != StorageType.Double)
            throw new InvalidOperationException("Missing duct size parameter: " + name);
        double value = parameter.AsDouble();
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            throw new InvalidOperationException("Invalid duct size parameter: " + name);
        return value;
    }

    private static void SetSize(Duct duct, BuiltInParameter name, double value)
    {
        Parameter parameter = duct.get_Parameter(name);
        if (parameter == null || parameter.StorageType != StorageType.Double || parameter.IsReadOnly)
            throw new InvalidOperationException("Cannot write duct size parameter: " + name);
        // Set can return false when the requested value is already present.
        if (Math.Abs(parameter.AsDouble() - value) <= 1e-6) return;
        if (!parameter.Set(value) || Math.Abs(ReadSize(duct, name) - value) > 1e-6)
            throw new InvalidOperationException("Duct size was not accepted: " + name);
    }

    public override string ToString() => Shape == ConnectorProfileType.Round
        ? $"{Shape}, D={Diameter:R} ft" : $"{Shape}, W={Width:R}, H={Height:R} ft";
}
