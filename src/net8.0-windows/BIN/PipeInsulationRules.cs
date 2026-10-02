using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;

namespace BIN;

public sealed class PipeInsulationRule
{
    public string System { get; set; }
    public double MinDN { get; set; }
    public double MaxDN { get; set; }
    public double ThicknessMM { get; set; }
}

public static class PipeInsulationRules
{
    public static List<PipeInsulationRule> DefaultC1()
    {
        return new List<PipeInsulationRule>
        {
            new PipeInsulationRule { System = "CHWS/CHWR", MinDN = 20, MaxDN = 40, ThicknessMM = 32 },
            new PipeInsulationRule { System = "CHWS/CHWR", MinDN = 50, MaxDN = 150, ThicknessMM = 40 },
            new PipeInsulationRule { System = "CHWS/CHWR", MinDN = 200, MaxDN = 500, ThicknessMM = 50 },
            new PipeInsulationRule { System = "CDP/CONDENSATE", MinDN = 20, MaxDN = 65, ThicknessMM = 19 },
            new PipeInsulationRule { System = "CDP/CONDENSATE", MinDN = 80, MaxDN = 500, ThicknessMM = 25 }
        };
    }

    public static double GetThicknessMm(Pipe pipe)
    {
        return GetThicknessMm(pipe, DefaultC1());
    }

    public static double GetThicknessMm(Pipe pipe, IEnumerable<PipeInsulationRule> rules)
    {
        return GetThicknessMm(SystemName(pipe), pipe == null ? 0.0 : pipe.Diameter * 304.8, rules);
    }

    public static double GetFittingThicknessMm(Element fitting, double fittingSizeFt, IEnumerable<PipeInsulationRule> rules)
    {
        return GetThicknessMm(FittingSystemName(fitting), fittingSizeFt * 304.8, rules);
    }

    /// <summary>
    /// Matches a system name against a rule pattern. A rule may list alternatives
    /// with comma, semicolon, or slash. ALL and OTHER are fallback wildcards; the
    /// caller resolves specific rules before considering them.
    /// </summary>
    public static bool Matches(string actual, string pattern)
    {
        if (IsFallbackPattern(pattern))
        {
            return true;
        }

        string actualName = (actual ?? string.Empty).Trim();
        string[] alternatives = (pattern ?? string.Empty).Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string alternative in alternatives)
        {
            string candidate = alternative.Trim();
            if (candidate.Length > 0 &&
                (actualName.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
                 actualName.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
        }

        return false;
    }

    public static string SystemName(Pipe pipe)
    {
        return SystemNameFromParameter(pipe, pipe == null ? null : pipe.Document);
    }

    public static string FittingSystemName(Element fitting)
    {
        string systemName = SystemNameFromParameter(fitting, fitting == null ? null : fitting.Document);
        if (!string.Equals(systemName, "Other", StringComparison.OrdinalIgnoreCase))
        {
            return systemName;
        }

        FamilyInstance familyInstance = fitting as FamilyInstance;
        ConnectorManager connectorManager = familyInstance == null || familyInstance.MEPModel == null
            ? null
            : familyInstance.MEPModel.ConnectorManager;
        if (connectorManager != null)
        {
            foreach (Connector connector in connectorManager.Connectors)
            {
                foreach (Connector referencedConnector in connector.AllRefs)
                {
                    Pipe connectedPipe = referencedConnector.Owner as Pipe;
                    if (connectedPipe != null)
                    {
                        return SystemName(connectedPipe);
                    }
                }
            }
        }

        return systemName;
    }

    private static double GetThicknessMm(string systemName, double diameterMm, IEnumerable<PipeInsulationRule> rules)
    {
        if (diameterMm <= 0.0 || rules == null)
        {
            return 0.0;
        }

        List<PipeInsulationRule> candidates = new List<PipeInsulationRule>(rules);

        // A named system rule always wins over an ALL/OTHER fallback rule.
        foreach (PipeInsulationRule rule in candidates)
        {
            if (IsUsableForDiameter(rule, diameterMm) && !IsFallbackPattern(rule.System) && Matches(systemName, rule.System))
            {
                return rule.ThicknessMM;
            }
        }

        foreach (PipeInsulationRule rule in candidates)
        {
            if (IsUsableForDiameter(rule, diameterMm) && IsFallbackPattern(rule.System) && Matches(systemName, rule.System))
            {
                return rule.ThicknessMM;
            }
        }

        return 0.0;
    }

    private static bool IsUsableForDiameter(PipeInsulationRule rule, double diameterMm)
    {
        return rule != null && rule.ThicknessMM > 0.0 &&
               diameterMm >= rule.MinDN - 0.1 && diameterMm <= rule.MaxDN + 0.1;
    }

    private static bool IsFallbackPattern(string pattern)
    {
        string normalized = (pattern ?? string.Empty).Trim();
        return normalized.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("OTHER", StringComparison.OrdinalIgnoreCase);
    }

    private static string SystemNameFromParameter(Element element, Document document)
    {
        if (element == null)
        {
            return "Other";
        }

        Parameter parameter = element.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM);
        if (parameter != null && parameter.StorageType == StorageType.ElementId)
        {
            Element systemType = document == null ? null : document.GetElement(parameter.AsElementId());
            if (systemType != null && !string.IsNullOrWhiteSpace(systemType.Name))
            {
                return systemType.Name;
            }
        }

        string name = parameter == null ? null : parameter.AsValueString() ?? parameter.AsString();
        return string.IsNullOrWhiteSpace(name) ? "Other" : name;
    }
}
