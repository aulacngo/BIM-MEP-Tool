using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BIN;

/// <summary>Pure C# WPF window: intentionally contains no XAML/BAML dependency.</summary>
public sealed class CheckPipeClashWindow : Window
{
    private readonly DataGrid grid;
    private readonly TextBox clearance;
    private readonly Func<PipeClearanceMode, double, IList<PipeClashResult>> calculate;
    public PipeClearanceMode Mode { get { return mode.SelectedIndex == 0 ? PipeClearanceMode.Bare : mode.SelectedIndex == 1 ? PipeClearanceMode.Insulated : PipeClearanceMode.Construction; } }
    private readonly ComboBox mode;
    public double RequiredMm { get { double v; return double.TryParse(clearance.Text, out v) && v >= 0 ? v : 25; } }
    public PipeClashResult SelectedResult { get { return grid.SelectedItem as PipeClashResult; } }
    public bool ZoomRequested { get; private set; }
    public bool MarkerRequested { get; private set; }
    public CheckPipeClashWindow(Func<PipeClearanceMode, double, IList<PipeClashResult>> calculate, PipeClearanceMode initialMode, double requiredMm)
    {
        this.calculate = calculate; Title = "BIM - Local 3-Pipe Clash & Clearance"; Width = 920; Height = 480; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new DockPanel { Margin = new Thickness(12) }; Content = root; var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Che do:", VerticalAlignment = VerticalAlignment.Center }); mode = new ComboBox { Width = 185, Margin = new Thickness(6, 0, 16, 0) }; mode.Items.Add(new ComboBoxItem { Content = "Ong tran (Bare)" }); mode.Items.Add(new ComboBoxItem { Content = "Co bao on (Insulated)" }); mode.Items.Add(new ComboBoxItem { Content = "Khoang ho thi cong" }); mode.SelectedIndex = (int)initialMode; top.Children.Add(mode);
        top.Children.Add(new TextBlock { Text = "Yeu cau (mm):", VerticalAlignment = VerticalAlignment.Center }); clearance = new TextBox { Width = 55, Text = requiredMm.ToString("F0"), Margin = new Thickness(6, 0, 0, 0) }; top.Children.Add(clearance);
        grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true, SelectionMode = DataGridSelectionMode.Single, Margin = new Thickness(0, 0, 0, 8) }; grid.AutoGeneratingColumn += (s, e) => { if (e.PropertyName == "FirstPoint" || e.PropertyName == "SecondPoint" || e.PropertyName == "FirstId" || e.PropertyName == "SecondId" || e.PropertyName == "Status") e.Cancel = true; }; root.Children.Add(grid); mode.SelectionChanged += delegate { Refresh(); }; clearance.LostFocus += delegate { Refresh(); }; Refresh();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons); buttons.Children.Add(Button("Zoom + Highlight cap", delegate { ZoomRequested = true; DialogResult = true; Close(); })); buttons.Children.Add(Button("Tao marker diem hep", delegate { MarkerRequested = true; DialogResult = true; Close(); })); buttons.Children.Add(Button("Dong", delegate { DialogResult = false; Close(); }));
    }
    private void Refresh() { if (calculate != null) grid.ItemsSource = calculate(Mode, RequiredMm); }
    private Button Button(string caption, RoutedEventHandler click) { var b = new Button { Content = caption, Margin = new Thickness(5), Padding = new Thickness(10, 4, 10, 4) }; b.Click += click; return b; }
}
