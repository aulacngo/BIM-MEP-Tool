using System;
using System.Collections.Generic;
using System.Linq;
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
    public const string ChillerC1PresetKey = "chiller-c1";
    public const string CondensateC1PresetKey = "condensate-c1";
    public const string HotWaterPresetKey = "hot-water";
    public const string AllC1PresetKey = "all-c1";
    public const string UserCustomPresetKey = "user-custom";

    // DN 10000 is the editable representation of "DN200+". It covers practical
    // project pipe sizes without requiring a special value in the rule grid.
    private const double OpenEndedDn = 10000.0;

    public static List<PipeInsulationRule> DefaultChillerC1()
    {
        return new List<PipeInsulationRule>
        {
            new PipeInsulationRule { System = "CHWS/CHWR/CHILLER/CHILLED WATER", MinDN = 20, MaxDN = 40, ThicknessMM = 32 },
            new PipeInsulationRule { System = "CHWS/CHWR/CHILLER/CHILLED WATER", MinDN = 50, MaxDN = 150, ThicknessMM = 40 },
            new PipeInsulationRule { System = "CHWS/CHWR/CHILLER/CHILLED WATER", MinDN = 200, MaxDN = OpenEndedDn, ThicknessMM = 50 }
        };
    }

    public static List<PipeInsulationRule> DefaultCondensateC1()
    {
        return new List<PipeInsulationRule>
        {
            new PipeInsulationRule { System = "CDP/CONDENSATE/DRAIN", MinDN = 20, MaxDN = 65, ThicknessMM = 19 },
            new PipeInsulationRule { System = "CDP/CONDENSATE/DRAIN", MinDN = 80, MaxDN = OpenEndedDn, ThicknessMM = 25 }
        };
    }

    public static List<PipeInsulationRule> DefaultHotWater()
    {
        return new List<PipeInsulationRule>
        {
            new PipeInsulationRule { System = "DHW/HOT WATER", MinDN = 15, MaxDN = 32, ThicknessMM = 25 },
            new PipeInsulationRule { System = "DHW/HOT WATER", MinDN = 40, MaxDN = 65, ThicknessMM = 32 },
            new PipeInsulationRule { System = "DHW/HOT WATER", MinDN = 80, MaxDN = OpenEndedDn, ThicknessMM = 40 }
        };
    }

    public static List<PipeInsulationRule> DefaultAllC1()
    {
        List<PipeInsulationRule> rules = DefaultChillerC1();
        rules.AddRange(DefaultCondensateC1());
        rules.AddRange(DefaultHotWater());
        return rules;
    }

    // Kept for callers and existing user settings created before the preset UI.
    public static List<PipeInsulationRule> DefaultC1()
    {
        return DefaultAllC1();
    }

    public static List<PipeInsulationRule> DefaultForPreset(string presetKey)
    {
        if (string.Equals(presetKey, ChillerC1PresetKey, StringComparison.OrdinalIgnoreCase))
        {
            return DefaultChillerC1();
        }

        if (string.Equals(presetKey, CondensateC1PresetKey, StringComparison.OrdinalIgnoreCase))
        {
            return DefaultCondensateC1();
        }

        if (string.Equals(presetKey, HotWaterPresetKey, StringComparison.OrdinalIgnoreCase))
        {
            return DefaultHotWater();
        }

        return DefaultAllC1();
    }

    public static string SuggestedPresetKey(string systemName)
    {
        if (Matches(systemName, "CHWS/CHWR/CHILLER/CHILLED WATER"))
        {
            return ChillerC1PresetKey;
        }

        if (Matches(systemName, "CDP/CONDENSATE/DRAIN"))
        {
            return CondensateC1PresetKey;
        }

        if (Matches(systemName, "DHW/HOT WATER"))
        {
            return HotWaterPresetKey;
        }

        return null;
    }

    public static double GetThicknessMm(Pipe pipe)
    {
        return GetThicknessMm(pipe, DefaultAllC1());
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
    /// Matches a Revit system name against one or more rule alternatives. Besides
    /// comma/slash/semicolon alternatives, common MEP aliases are treated as the
    /// same system family: CHWS/CHWR/Chilled Water, CDP/Condensate/Drain, and
    /// DHW/Hot Water. This keeps factory presets useful with project-specific names.
    /// </summary>
    public static bool Matches(string actual, string pattern)
    {
        if (IsFallbackPattern(pattern))
        {
            return true;
        }

        List<string> actualAlternatives = SplitAlternatives(actual).ToList();
        List<string> patternAlternatives = SplitAlternatives(pattern).ToList();
        if (actualAlternatives.Count == 0 || patternAlternatives.Count == 0)
        {
            return false;
        }

        foreach (string actualAlternative in actualAlternatives)
        {
            foreach (string patternAlternative in patternAlternatives)
            {
                if (IsFuzzyMatch(actualAlternative, patternAlternative))
                {
                    return true;
                }
            }
        }

        foreach (string[] aliases in SystemAliasGroups)
        {
            if (ContainsAnyAlias(actualAlternatives, aliases) && ContainsAnyAlias(patternAlternatives, aliases))
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

    private static readonly string[][] SystemAliasGroups =
    {
        new[] { "CHWS", "CHWR", "CHW", "CHILLER", "CHILLED WATER", "CHILLED" },
        new[] { "CDP", "CONDENSATE", "CONDENSATION", "DRAIN", "DRAINAGE" },
        new[] { "DHW", "HOT WATER", "HOTWATER" }
    };

    private static IEnumerable<string> SplitAlternatives(string value)
    {
        return (value ?? string.Empty)
            .Split(new[] { ',', ';', '/', '|', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0);
    }

    private static bool IsFuzzyMatch(string actual, string pattern)
    {
        string normalizedActual = Normalize(actual);
        string normalizedPattern = Normalize(pattern);
        if (normalizedActual.Length == 0 || normalizedPattern.Length == 0)
        {
            return false;
        }

        return normalizedActual.Equals(normalizedPattern, StringComparison.Ordinal) ||
               normalizedActual.IndexOf(normalizedPattern, StringComparison.Ordinal) >= 0 ||
               normalizedPattern.IndexOf(normalizedActual, StringComparison.Ordinal) >= 0;
    }

    private static bool ContainsAnyAlias(IEnumerable<string> values, IEnumerable<string> aliases)
    {
        foreach (string value in values)
        {
            foreach (string alias in aliases)
            {
                if (IsFuzzyMatch(value, alias))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
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
