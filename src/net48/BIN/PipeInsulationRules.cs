using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
namespace BIN;
public sealed class PipeInsulationRule { public string System { get; set; } public double MinDN { get; set; } public double MaxDN { get; set; } public double ThicknessMM { get; set; } }
public static class PipeInsulationRules {
 public static List<PipeInsulationRule> DefaultC1() { return new List<PipeInsulationRule> { new PipeInsulationRule { System="CHWS/CHWR", MinDN=20, MaxDN=40, ThicknessMM=32 }, new PipeInsulationRule { System="CHWS/CHWR", MinDN=50, MaxDN=150, ThicknessMM=40 }, new PipeInsulationRule { System="CHWS/CHWR", MinDN=200, MaxDN=500, ThicknessMM=50 }, new PipeInsulationRule { System="CDP/CONDENSATE", MinDN=20, MaxDN=65, ThicknessMM=19 }, new PipeInsulationRule { System="CDP/CONDENSATE", MinDN=80, MaxDN=500, ThicknessMM=25 } }; }
 public static double GetThicknessMm(Pipe pipe) { return GetThicknessMm(pipe, DefaultC1()); }
 public static double GetThicknessMm(Pipe pipe, IEnumerable<PipeInsulationRule> rules) { string system=SystemName(pipe); double dn=pipe.Diameter*304.8; foreach(var r in rules) if(dn>=r.MinDN-.1 && dn<=r.MaxDN+.1 && Matches(system,r.System)) return r.ThicknessMM; return 0; }
 public static double GetFittingThicknessMm(Element fitting, double fittingSizeFt, IEnumerable<PipeInsulationRule> rules) { string system=FittingSystemName(fitting); double dn=fittingSizeFt*304.8; foreach(var r in rules) if(dn>=r.MinDN-.1 && dn<=r.MaxDN+.1 && Matches(system,r.System)) return r.ThicknessMM; foreach(var r in rules) if(dn>=r.MinDN-.1 && dn<=r.MaxDN+.1) return r.ThicknessMM; return 0; }
 public static string SystemName(Pipe pipe) { Parameter p=pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM); if(p!=null && p.StorageType==StorageType.ElementId) { Element e=pipe.Document.GetElement(p.AsElementId()); if(e!=null)return e.Name; } return p!=null ? (p.AsValueString() ?? p.AsString() ?? "Other") : "Other"; }
 public static string FittingSystemName(Element fitting) { Parameter p=fitting.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM); if(p!=null && p.StorageType==StorageType.ElementId) { Element e=fitting.Document.GetElement(p.AsElementId()); if(e!=null)return e.Name; } if(fitting is FamilyInstance fi && fi.MEPModel?.ConnectorManager?.Connectors!=null) { foreach(Connector c in fi.MEPModel.ConnectorManager.Connectors) { foreach(Connector refC in c.AllRefs) { if(refC.Owner is Pipe pOwner) return SystemName(pOwner); } } } return p!=null ? (p.AsValueString() ?? p.AsString() ?? "Other") : "Other"; }
 private static bool Matches(string actual,string pattern) { actual=(actual??"").ToUpperInvariant(); foreach(string w in (pattern??"OTHER").ToUpperInvariant().Split('/')) if(actual.Contains(w)) return true; return (pattern??"").ToUpperInvariant().Contains("OTHER"); }
}
