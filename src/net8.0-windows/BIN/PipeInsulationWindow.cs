using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BIN;

public enum PipeInsulationScope
{
    AllInView = 0,
    SelectedPipes = 1,
    BySystemType = 2
}

/// <summary>Rule editor built entirely in C# WPF; it has no XAML/BAML dependency.</summary>
public sealed class PipeInsulationWindow : Window
{
    private const string EmptySystemTypeMessage = "Không có hệ thống nào trong view";
    private static readonly string FileName = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BIN_PipeInsulation",
        "system_rules.json");

    private readonly ComboBox insulationTypeComboBox;
    private readonly ComboBox systemTypeComboBox;
    private readonly CheckBox removeExistingCheckBox;
    private readonly RadioButton allInViewRadioButton;
    private readonly RadioButton selectedPipesRadioButton;
    private readonly RadioButton bySystemTypeRadioButton;

    public ObservableCollection<PipeInsulationRule> Rules { get; private set; }
    public InsulationTypeItem SelectedInsulationType { get; private set; }
    public bool RemoveExisting { get; private set; }
    public PipeInsulationScope SelectedScope { get; private set; }
    public string SelectedSystemType { get; private set; }

    public PipeInsulationWindow(List<InsulationTypeItem> insulationTypes, List<string> availableSystemTypes)
    {
        Title = "BIM - Pipe Insulation";
        Width = 860;
        Height = 610;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResize;

        Rules = new ObservableCollection<PipeInsulationRule>(LoadRules());

        Grid root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        WrapPanel topBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        topBar.Children.Add(new TextBlock
        {
            Text = "Insulation Type:",
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 6, 0)
        });

        insulationTypeComboBox = new ComboBox
        {
            Width = 260,
            MinHeight = 28,
            ItemsSource = insulationTypes ?? new List<InsulationTypeItem>(),
            Margin = new Thickness(0, 0, 20, 0)
        };
        if (insulationTypeComboBox.Items.Count > 0)
        {
            insulationTypeComboBox.SelectedIndex = 0;
        }
        topBar.Children.Add(insulationTypeComboBox);

        removeExistingCheckBox = new CheckBox
        {
            Content = "Xóa insulation cũ",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };
        topBar.Children.Add(removeExistingCheckBox);
        Grid.SetRow(topBar, 0);
        root.Children.Add(topBar);

        Border scopeBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 218, 230)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 253)),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 12)
        };
        StackPanel scopePanel = new StackPanel();
        scopePanel.Children.Add(new TextBlock
        {
            Text = "Phạm vi áp dụng (Scope):",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        allInViewRadioButton = new RadioButton
        {
            Content = "Tất cả đường trong View",
            GroupName = "scope",
            Margin = new Thickness(0, 1, 0, 1)
        };
        selectedPipesRadioButton = new RadioButton
        {
            Content = "Đường ống đang chọn",
            GroupName = "scope",
            Margin = new Thickness(0, 1, 0, 1)
        };
        bySystemTypeRadioButton = new RadioButton
        {
            Content = "Theo System Type:",
            GroupName = "scope",
            IsChecked = true,
            Margin = new Thickness(0, 1, 0, 4)
        };
        scopePanel.Children.Add(allInViewRadioButton);
        scopePanel.Children.Add(selectedPipesRadioButton);
        scopePanel.Children.Add(bySystemTypeRadioButton);

        systemTypeComboBox = new ComboBox
        {
            Name = "cboSystemType",
            MinHeight = 28,
            MinWidth = 360,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(22, 0, 0, 0)
        };
        List<string> systemTypes = (availableSystemTypes ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (systemTypes.Count == 0)
        {
            systemTypes.Add(EmptySystemTypeMessage);
        }
        systemTypeComboBox.ItemsSource = systemTypes;
        systemTypeComboBox.SelectedIndex = 0;
        scopePanel.Children.Add(systemTypeComboBox);

        allInViewRadioButton.Checked += ScopeRadioButtonChecked;
        selectedPipesRadioButton.Checked += ScopeRadioButtonChecked;
        bySystemTypeRadioButton.Checked += ScopeRadioButtonChecked;
        UpdateSystemTypeEnabledState();

        scopeBorder.Child = scopePanel;
        Grid.SetRow(scopeBorder, 1);
        root.Children.Add(scopeBorder);

        DataGrid rulesGrid = new DataGrid
        {
            ItemsSource = Rules,
            AutoGenerateColumns = false,
            CanUserAddRows = true,
            CanUserDeleteRows = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(0, 0, 0, 12),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(247, 249, 252))
        };
        rulesGrid.Columns.Add(CreateTextColumn("System", "System", 2));
        rulesGrid.Columns.Add(CreateTextColumn("MinDN (mm)", "MinDN", 1));
        rulesGrid.Columns.Add(CreateTextColumn("MaxDN (mm)", "MaxDN", 1));
        rulesGrid.Columns.Add(CreateTextColumn("ThicknessMM (mm)", "ThicknessMM", 1));
        Grid.SetRow(rulesGrid, 2);
        root.Children.Add(rulesGrid);

        StackPanel bottomBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button resetButton = CreateButton("Nạp lại chuẩn C1", null);
        resetButton.Click += delegate
        {
            Rules.Clear();
            foreach (PipeInsulationRule rule in PipeInsulationRules.DefaultC1())
            {
                Rules.Add(rule);
            }
        };
        bottomBar.Children.Add(resetButton);

        Button applyButton = CreateButton("Áp dụng", new SolidColorBrush(Color.FromRgb(0, 120, 212)));
        applyButton.IsDefault = true;
        applyButton.Click += ApplyButtonClicked;
        bottomBar.Children.Add(applyButton);

        Button cancelButton = CreateButton("Hủy", null);
        cancelButton.IsCancel = true;
        cancelButton.Click += delegate { Close(); };
        bottomBar.Children.Add(cancelButton);
        Grid.SetRow(bottomBar, 3);
        root.Children.Add(bottomBar);
    }

    private static DataGridTextColumn CreateTextColumn(string header, string propertyName, double starWidth)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(propertyName) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(starWidth, DataGridLengthUnitType.Star)
        };
    }

    private static Button CreateButton(string content, Brush background)
    {
        Button button = new Button
        {
            Content = content,
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(4, 0, 0, 0),
            MinHeight = 30
        };
        if (background != null)
        {
            button.Background = background;
            button.Foreground = Brushes.White;
            button.BorderBrush = background;
        }
        return button;
    }

    private void ScopeRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        UpdateSystemTypeEnabledState();
    }

    private void UpdateSystemTypeEnabledState()
    {
        if (systemTypeComboBox != null)
        {
            systemTypeComboBox.IsEnabled = bySystemTypeRadioButton != null && bySystemTypeRadioButton.IsChecked == true;
        }
    }

    private void ApplyButtonClicked(object sender, RoutedEventArgs e)
    {
        SelectedInsulationType = insulationTypeComboBox.SelectedItem as InsulationTypeItem;
        if (SelectedInsulationType == null)
        {
            MessageBox.Show("Vui lòng chọn Insulation Type.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!Rules.Any(rule => rule != null && rule.MinDN > 0 && rule.MaxDN >= rule.MinDN && rule.ThicknessMM > 0))
        {
            MessageBox.Show("Vui lòng nhập ít nhất một rule hợp lệ.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (allInViewRadioButton.IsChecked == true)
        {
            SelectedScope = PipeInsulationScope.AllInView;
        }
        else if (selectedPipesRadioButton.IsChecked == true)
        {
            SelectedScope = PipeInsulationScope.SelectedPipes;
        }
        else
        {
            SelectedScope = PipeInsulationScope.BySystemType;
        }

        SelectedSystemType = systemTypeComboBox.SelectedItem as string;
        if (SelectedScope == PipeInsulationScope.BySystemType &&
            (string.IsNullOrWhiteSpace(SelectedSystemType) ||
             string.Equals(SelectedSystemType, EmptySystemTypeMessage, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("View hiện tại không có System Type để áp dụng.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        RemoveExisting = removeExistingCheckBox.IsChecked == true;
        string saveError;
        if (!SaveRules(out saveError))
        {
            MessageBox.Show("Không thể lưu rule insulation.\n" + saveError, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }

    private static IEnumerable<PipeInsulationRule> LoadRules()
    {
        try
        {
            if (File.Exists(FileName))
            {
                string json = File.ReadAllText(FileName);
                List<PipeInsulationRule> rules = new List<PipeInsulationRule>();
                foreach (Match match in Regex.Matches(json,
                    "\\{\\s*\\\"System\\\"\\s*:\\s*\\\"(?<system>(?:\\\\.|[^\\\"])*)\\\"\\s*,\\s*\\\"MinDN\\\"\\s*:\\s*(?<min>-?[0-9.]+)\\s*,\\s*\\\"MaxDN\\\"\\s*:\\s*(?<max>-?[0-9.]+)\\s*,\\s*\\\"ThicknessMM\\\"\\s*:\\s*(?<thickness>-?[0-9.]+)",
                    RegexOptions.CultureInvariant))
                {
                    double min;
                    double max;
                    double thickness;
                    if (double.TryParse(match.Groups["min"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out min) &&
                        double.TryParse(match.Groups["max"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out max) &&
                        double.TryParse(match.Groups["thickness"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out thickness))
                    {
                        rules.Add(new PipeInsulationRule
                        {
                            System = JsonUnescape(match.Groups["system"].Value),
                            MinDN = min,
                            MaxDN = max,
                            ThicknessMM = thickness
                        });
                    }
                }
                if (rules.Count > 0)
                {
                    return rules;
                }
            }
        }
        catch
        {
            // A malformed local settings file must not prevent the command from opening.
        }

        return PipeInsulationRules.DefaultC1();
    }

    private bool SaveRules(out string error)
    {
        try
        {
            List<PipeInsulationRule> rows = Rules
                .Where(rule => rule != null && rule.MinDN > 0 && rule.MaxDN >= rule.MinDN && rule.ThicknessMM > 0)
                .ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(FileName));
            List<string> lines = new List<string> { "[" };
            for (int index = 0; index < rows.Count; index++)
            {
                PipeInsulationRule rule = rows[index];
                string comma = index == rows.Count - 1 ? string.Empty : ",";
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {{\"System\":\"{0}\",\"MinDN\":{1},\"MaxDN\":{2},\"ThicknessMM\":{3}}}{4}",
                    JsonEscape(rule.System ?? "OTHER"),
                    rule.MinDN,
                    rule.MaxDN,
                    rule.ThicknessMM,
                    comma));
            }
            lines.Add("]");
            File.WriteAllLines(FileName, lines);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string JsonEscape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string JsonUnescape(string value)
    {
        return (value ?? string.Empty).Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}
