using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
namespace BIN;
/// <summary>Rule editor built in C#; it has no XAML/BAML dependency.</summary>
public sealed class PipeInsulationWindow : Window {
 private static readonly string FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"BIN_PipeInsulation","system_rules.json");
 private readonly ComboBox types;
 public ObservableCollection<PipeInsulationRule> Rules { get; private set; }
 public InsulationTypeItem SelectedInsulationType { get; private set; }
 public bool RemoveExisting { get; private set; }
 public bool ApplyToAll { get; private set; }
 public PipeInsulationWindow(List<InsulationTypeItem> insulationTypes) {
  Title="BIM - Rule Based Pipe Insulation (Tai Bac C1)"; Width=800; Height=520; WindowStartupLocation=WindowStartupLocation.CenterScreen; Rules=new ObservableCollection<PipeInsulationRule>(LoadRules());
  var root=new DockPanel { Margin=new Thickness(12) }; Content=root; var top=new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(0,0,0,8) }; DockPanel.SetDock(top,Dock.Top); root.Children.Add(top);
  top.Children.Add(new TextBlock { Text="Insulation Type:", VerticalAlignment=VerticalAlignment.Center }); types=new ComboBox { Width=220, Margin=new Thickness(6,0,18,0), ItemsSource=insulationTypes }; types.SelectedIndex=0; top.Children.Add(types);
  var remove=new CheckBox { Content="Xoa insulation cu", Margin=new Thickness(0,0,14,0) }; top.Children.Add(remove); var selected=new RadioButton { Content="Pipes da chon", IsChecked=true, Margin=new Thickness(0,0,10,0), GroupName="scope" }; top.Children.Add(selected); var all=new RadioButton { Content="Tat ca pipes trong view", GroupName="scope" }; top.Children.Add(all);
  var grid=new DataGrid { ItemsSource=Rules, AutoGenerateColumns=true, CanUserAddRows=true, CanUserDeleteRows=true, Margin=new Thickness(0,0,0,8) }; root.Children.Add(grid); var bottom=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right }; DockPanel.SetDock(bottom,Dock.Bottom); root.Children.Add(bottom);
  var reset=new Button { Content="Nap lai C1", Margin=new Thickness(5), Padding=new Thickness(10,4,10,4) }; reset.Click+=delegate { Rules.Clear(); foreach(var r in PipeInsulationRules.DefaultC1()) Rules.Add(r); }; bottom.Children.Add(reset);
  var ok=new Button { Content="Ap dung", Margin=new Thickness(5), Padding=new Thickness(10,4,10,4) }; ok.Click+=delegate { SelectedInsulationType=types.SelectedItem as InsulationTypeItem; if(SelectedInsulationType==null || Rules.Count==0) { MessageBox.Show("Chon Insulation Type va nhap it nhat mot rule."); return; } RemoveExisting=remove.IsChecked==true; ApplyToAll=all.IsChecked==true; SaveRules(); DialogResult=true; Close(); }; bottom.Children.Add(ok);
  var cancel=new Button { Content="Huy", Margin=new Thickness(5), Padding=new Thickness(10,4,10,4) }; cancel.Click+=delegate { DialogResult=false; Close(); }; bottom.Children.Add(cancel);
 }
 private static IEnumerable<PipeInsulationRule> LoadRules() { try { if(File.Exists(FileName)) { string s=File.ReadAllText(FileName); var found=new List<PipeInsulationRule>(); foreach(Match m in Regex.Matches(s,"\\{\\s*\\\"System\\\"\\s*:\\s*\\\"(?<s>[^\\\"]+)\\\"\\s*,\\s*\\\"MinDN\\\"\\s*:\\s*(?<min>[0-9.]+)\\s*,\\s*\\\"MaxDN\\\"\\s*:\\s*(?<max>[0-9.]+)\\s*,\\s*\\\"ThicknessMM\\\"\\s*:\\s*(?<t>[0-9.]+)")) found.Add(new PipeInsulationRule { System=m.Groups["s"].Value, MinDN=double.Parse(m.Groups["min"].Value), MaxDN=double.Parse(m.Groups["max"].Value), ThicknessMM=double.Parse(m.Groups["t"].Value) }); if(found.Count>0) return found; } } catch {} return PipeInsulationRules.DefaultC1(); }
 private void SaveRules() { try { Directory.CreateDirectory(Path.GetDirectoryName(FileName)); var rows=Rules.Where(r=>r.MinDN>0 && r.MaxDN>=r.MinDN && r.ThicknessMM>0).ToList(); File.WriteAllLines(FileName,new[]{"["}.Concat(rows.Select((r,i)=>string.Format("  {{\"System\":\"{0}\",\"MinDN\":{1},\"MaxDN\":{2},\"ThicknessMM\":{3}}}{4}",r.System??"Other",r.MinDN,r.MaxDN,r.ThicknessMM,i<rows.Count-1?",":"")).Concat(new[]{"]"}))); } catch {} }
}
