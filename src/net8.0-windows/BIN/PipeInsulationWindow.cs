using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace BIN;

public enum PipeInsulationScope
{
    AllInView = 0,
    SelectedPipes = 1,
    BySystemType = 2
}

/// <summary>One pipe system in the active view, with its pipe count kept separate from the display label.</summary>
public sealed class PipeInsulationSystemOption
{
    public string SystemName { get; set; }
    public int PipeCount { get; set; }

    public string DisplayName
    {
        get { return string.Format(CultureInfo.CurrentCulture, "{0} ({1} ống)", SystemName, PipeCount); }
    }
}

internal sealed class PipeInsulationPresetItem
{
    public string Key { get; set; }
    public string DisplayName { get; set; }
    public bool IsUserCustom { get; set; }

    public override string ToString()
    {
        return DisplayName;
    }
}

/// <summary>Rule editor built entirely in C# WPF; it has no XAML/BAML dependency.</summary>
public sealed class PipeInsulationWindow : Window
{
    private const string EmptySystemTypeMessage = "Không có hệ thống nào trong view";
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BIN_PipeInsulation");
    private static readonly string PresetFileName = Path.Combine(SettingsDirectory, "presets.json");
    private static readonly string DuctPresetFileName = Path.Combine(SettingsDirectory, "duct-presets.json");
    private static readonly string LegacyRulesFileName = Path.Combine(SettingsDirectory, "system_rules.json");

    private readonly ComboBox insulationTypeComboBox;
    private readonly ComboBox systemTypeComboBox;
    private readonly ComboBox presetComboBox;
    private readonly RadioButton pipeModeRadioButton;
    private readonly RadioButton ductModeRadioButton;
    private readonly CheckBox removeExistingCheckBox;
    private readonly CheckBox smoothTeesCheckBox;
    private readonly RadioButton allInViewRadioButton;
    private readonly RadioButton selectedPipesRadioButton;
    private readonly RadioButton bySystemTypeRadioButton;
    private readonly DataGrid rulesGrid;
    private readonly TextBlock presetStatusText;
    private DataGridTextColumn minSizeColumn;
    private DataGridTextColumn maxSizeColumn;
    private List<PipeInsulationPresetItem> presetItems;
    private readonly List<InsulationTypeItem> pipeInsulationTypes;
    private readonly List<InsulationTypeItem> ductInsulationTypes;
    private readonly List<PipeInsulationSystemOption> pipeSystems;
    private readonly List<PipeInsulationSystemOption> ductSystems;

    private List<PipeInsulationRule> savedUserCustomRules;
    private List<PipeInsulationRule> savedDuctUserCustomRules;
    private bool isLoadingPreset;

    public ObservableCollection<PipeInsulationRule> Rules { get; private set; }
    public InsulationTypeItem SelectedInsulationType { get; private set; }
    public bool RemoveExisting { get; private set; }
    public bool SmoothTees { get; private set; }
    public PipeInsulationScope SelectedScope { get; private set; }
    public string SelectedSystemType { get; private set; }
    public string SelectedPresetName { get; private set; }
    public MepTargetKind SelectedMepTarget { get; private set; } = MepTargetKind.Pipe;

    public PipeInsulationWindow(
        List<InsulationTypeItem> pipeInsulationTypes,
        List<InsulationTypeItem> ductInsulationTypes,
        List<PipeInsulationSystemOption> pipeSystems,
        List<PipeInsulationSystemOption> ductSystems)
    {
        Title = "BIM | Pipe & Duct Insulation";
        Width = 960;
        Height = 720;
        MinWidth = 880;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.CanResize;
        Background = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");

        this.pipeInsulationTypes = pipeInsulationTypes ?? new List<InsulationTypeItem>();
        this.ductInsulationTypes = ductInsulationTypes ?? new List<InsulationTypeItem>();
        this.pipeSystems = pipeSystems ?? new List<PipeInsulationSystemOption>();
        this.ductSystems = ductSystems ?? new List<PipeInsulationSystemOption>();
        savedUserCustomRules = LoadUserCustomRules(PresetFileName);
        savedDuctUserCustomRules = LoadUserCustomRules(DuctPresetFileName);
        Rules = new ObservableCollection<PipeInsulationRule>();
        presetItems = new List<PipeInsulationPresetItem>();

        Grid root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Content = root;

        Border header = CreateHeader();
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        Border setupCard = CreateCard(new Grid(), new Thickness(0, 12, 0, 12));
        Grid setupGrid = (Grid)setupCard.Child;
        setupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(285) });
        setupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        setupGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        setupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        setupGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        StackPanel topBar = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        StackPanel targetModeBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };
        targetModeBar.Children.Add(CreateFieldLabel("Đối tượng áp dụng"));
        pipeModeRadioButton = new RadioButton
        {
            Content = "Ống Nước (Pipes & Fittings)",
            GroupName = "mepInsulationTarget",
            IsChecked = true,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(16, 0, 12, 0)
        };
        targetModeBar.Children.Add(pipeModeRadioButton);
        ductModeRadioButton = new RadioButton
        {
            Content = "Ống Gió (Ducts & Fittings)",
            GroupName = "mepInsulationTarget"
        };
        targetModeBar.Children.Add(ductModeRadioButton);
        topBar.Children.Add(targetModeBar);
        removeExistingCheckBox = new CheckBox
        {
            Content = "Thay thế insulation hiện có trong phạm vi",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(58, 69, 84))
        };
        topBar.Children.Add(removeExistingCheckBox);
        smoothTeesCheckBox = new CheckBox
        {
            Content = "Khử khối vuông ở Tê (Làm mượt ngã ba chữ T)",
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 2, 0, 2),
            ToolTip = "Bỏ qua tạo khối hộp thô của Tê trong Revit để lớp bảo ôn đường tự đâm vào nhau trơn láng, tự nhiên."
        };
        topBar.Children.Add(smoothTeesCheckBox);
        Grid.SetColumnSpan(topBar, 3);
        setupGrid.Children.Add(topBar);

        StackPanel materialPanel = new StackPanel();
        materialPanel.Children.Add(CreateSectionTitle("Thiết lập insulation"));
        materialPanel.Children.Add(CreateFieldLabel("Loại insulation"));
        insulationTypeComboBox = new ComboBox
        {
            MinHeight = 32,
            ItemsSource = this.pipeInsulationTypes,
            DisplayMemberPath = "Name",
            Margin = new Thickness(0, 0, 0, 10)
        };
        if (insulationTypeComboBox.Items.Count > 0)
        {
            insulationTypeComboBox.SelectedIndex = 0;
        }
        materialPanel.Children.Add(insulationTypeComboBox);
        Grid.SetRow(materialPanel, 1);
        Grid.SetColumn(materialPanel, 0);
        setupGrid.Children.Add(materialPanel);

        StackPanel scopePanel = new StackPanel();
        scopePanel.Children.Add(CreateSectionTitle("Phạm vi áp dụng"));
        bySystemTypeRadioButton = new RadioButton
        {
            Content = "Theo System Type (Khuyến dùng)",
            GroupName = "pipeInsulationScope",
            Margin = new Thickness(0, 0, 0, 5),
            IsChecked = true,
            FontWeight = FontWeights.SemiBold
        };
        scopePanel.Children.Add(bySystemTypeRadioButton);

        systemTypeComboBox = new ComboBox
        {
            MinHeight = 32,
            Margin = new Thickness(23, 0, 0, 8),
            DisplayMemberPath = "DisplayName"
        };
        scopePanel.Children.Add(systemTypeComboBox);

        selectedPipesRadioButton = new RadioButton
        {
            Content = "Đường ống đang chọn (hoặc chọn trực tiếp nếu chưa chọn)",
            GroupName = "pipeInsulationScope",
            Margin = new Thickness(0, 0, 0, 5)
        };
        scopePanel.Children.Add(selectedPipesRadioButton);
        allInViewRadioButton = new RadioButton
        {
            Content = "Tất cả đường ống trong View",
            GroupName = "pipeInsulationScope"
        };
        scopePanel.Children.Add(allInViewRadioButton);
        Grid.SetRow(scopePanel, 1);
        Grid.SetColumn(scopePanel, 2);
        setupGrid.Children.Add(scopePanel);

        Grid.SetRow(setupCard, 1);
        root.Children.Add(setupCard);

        Grid presetGrid = new Grid();
        presetGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        presetGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        presetGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        presetStatusText = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(83, 96, 112)),
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 8),
            Text = "Chọn một preset theo hệ thống, sau đó tinh chỉnh bảng khi cần. Lưu sẽ cập nhật preset Tùy chỉnh của người dùng."
        };

        Grid presetToolbar = new Grid();
        presetToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        presetToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        presetToolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        presetToolbar.Children.Add(CreateSectionTitle("Bảng quy tắc / Preset", new Thickness(0, 0, 12, 0)));
        presetComboBox = new ComboBox
        {
            MinHeight = 32,
            MinWidth = 220,
            ItemsSource = presetItems,
            DisplayMemberPath = "DisplayName",
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(presetComboBox, 1);
        presetToolbar.Children.Add(presetComboBox);

        StackPanel presetActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button savePresetButton = CreateButton("Lưu Preset này", ButtonKind.Secondary);
        savePresetButton.Click += SavePresetButtonClicked;
        presetActions.Children.Add(savePresetButton);
        Button reloadPresetButton = CreateButton("Nạp lại chuẩn C1", ButtonKind.Secondary);
        reloadPresetButton.Click += ReloadPresetButtonClicked;
        presetActions.Children.Add(reloadPresetButton);
        Button addRowButton = CreateButton("+ Thêm dòng", ButtonKind.Secondary);
        addRowButton.Click += AddRowButtonClicked;
        presetActions.Children.Add(addRowButton);
        Button deleteRowButton = CreateButton("Xóa dòng", ButtonKind.Secondary);
        deleteRowButton.Click += DeleteRowButtonClicked;
        presetActions.Children.Add(deleteRowButton);
        Grid.SetColumn(presetActions, 2);
        presetToolbar.Children.Add(presetActions);
        presetGrid.Children.Add(presetToolbar);
        Grid.SetRow(presetStatusText, 1);
        presetGrid.Children.Add(presetStatusText);

        rulesGrid = CreateRulesGrid();
        Grid.SetRow(rulesGrid, 2);
        presetGrid.Children.Add(rulesGrid);
        Border presetCard = CreateCard(presetGrid, new Thickness(0));
        Grid.SetRow(presetCard, 2);
        root.Children.Add(presetCard);

        StackPanel bottomBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        Button cancelButton = CreateButton("Hủy", ButtonKind.Secondary);
        cancelButton.IsCancel = true;
        cancelButton.Click += delegate { Close(); };
        bottomBar.Children.Add(cancelButton);
        Button applyButton = CreateButton("Áp dụng insulation", ButtonKind.Primary);
        applyButton.IsDefault = true;
        applyButton.Click += ApplyButtonClicked;
        bottomBar.Children.Add(applyButton);
        Grid.SetRow(bottomBar, 3);
        root.Children.Add(bottomBar);

        allInViewRadioButton.Checked += ScopeRadioButtonChecked;
        selectedPipesRadioButton.Checked += ScopeRadioButtonChecked;
        bySystemTypeRadioButton.Checked += ScopeRadioButtonChecked;
        pipeModeRadioButton.Checked += TargetModeRadioButtonChecked;
        ductModeRadioButton.Checked += TargetModeRadioButtonChecked;
        systemTypeComboBox.SelectionChanged += SystemTypeComboBoxSelectionChanged;
        presetComboBox.SelectionChanged += PresetComboBoxSelectionChanged;

        SwitchTarget(MepTargetKind.Pipe, false);
    }

    private static Border CreateHeader()
    {
        Border header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(16, 67, 122)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 15, 20, 15)
        };
        StackPanel headerContent = new StackPanel();
        headerContent.Children.Add(new TextBlock
        {
            Text = "PIPE & DUCT INSULATION",
            Foreground = Brushes.White,
            FontSize = 21,
            FontWeight = FontWeights.SemiBold
        });
        headerContent.Children.Add(new TextBlock
        {
            Text = "Chọn phạm vi • chọn preset tiêu chuẩn • kiểm tra và áp dụng",
            Foreground = new SolidColorBrush(Color.FromRgb(218, 233, 250)),
            FontSize = 12,
            Margin = new Thickness(0, 3, 0, 0)
        });
        header.Child = headerContent;
        return header;
    }

    private static Border CreateCard(UIElement child, Thickness margin)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(251, 252, 254)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 225, 234)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(16),
            Margin = margin,
            Child = child
        };
    }

    private static TextBlock CreateSectionTitle(string text)
    {
        return CreateSectionTitle(text, new Thickness(0, 0, 0, 8));
    }

    private static TextBlock CreateSectionTitle(string text, Thickness margin)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 45, 61)),
            Margin = margin,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock CreateFieldLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(83, 96, 112)),
            Margin = new Thickness(0, 0, 0, 4)
        };
    }

    private enum ButtonKind
    {
        Primary,
        Secondary
    }

    private static Button CreateButton(string content, ButtonKind kind)
    {
        bool primary = kind == ButtonKind.Primary;
        SolidColorBrush primaryBrush = new SolidColorBrush(Color.FromRgb(0, 103, 192));
        return new Button
        {
            Content = content,
            Padding = new Thickness(13, 6, 13, 6),
            Margin = new Thickness(5, 0, 0, 0),
            MinHeight = 32,
            Background = primary ? primaryBrush : new SolidColorBrush(Color.FromRgb(242, 245, 248)),
            Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(42, 55, 70)),
            BorderBrush = primary ? primaryBrush : new SolidColorBrush(Color.FromRgb(199, 209, 220))
        };
    }

    private static List<PipeInsulationPresetItem> CreatePresetItems(MepTargetKind targetKind)
    {
        if (targetKind == MepTargetKind.Duct)
        {
            return new List<PipeInsulationPresetItem>
            {
                new PipeInsulationPresetItem { Key = PipeInsulationRules.SupplyAirPresetKey, DisplayName = "Gió Cấp (SA)" },
                new PipeInsulationPresetItem { Key = PipeInsulationRules.ReturnAirPresetKey, DisplayName = "Gió Hồi (RA)" },
                new PipeInsulationPresetItem { Key = PipeInsulationRules.SmokeExhaustPresetKey, DisplayName = "Hút Khói Chống Cháy EI (SE)" },
                new PipeInsulationPresetItem { Key = PipeInsulationRules.AllDuctsPresetKey, DisplayName = "Tất cả hệ thống gió (SA / RA / SE)" },
                new PipeInsulationPresetItem { Key = PipeInsulationRules.UserCustomPresetKey, DisplayName = "Tùy chỉnh của người dùng (User Custom)", IsUserCustom = true }
            };
        }

        return new List<PipeInsulationPresetItem>
        {
            new PipeInsulationPresetItem { Key = PipeInsulationRules.ChillerC1PresetKey, DisplayName = "Hệ Chiller (CHWS/CHWR) - Chuẩn C1" },
            new PipeInsulationPresetItem { Key = PipeInsulationRules.CondensateC1PresetKey, DisplayName = "Hệ Nước Ngưng (Condensate/CDP) - Chuẩn C1" },
            new PipeInsulationPresetItem { Key = PipeInsulationRules.HotWaterPresetKey, DisplayName = "Hệ Nước Nóng (DHW/Hot Water)" },
            new PipeInsulationPresetItem { Key = PipeInsulationRules.AllC1PresetKey, DisplayName = "Tất cả các hệ thống (Multi-System)" },
            new PipeInsulationPresetItem { Key = PipeInsulationRules.UserCustomPresetKey, DisplayName = "Tùy chỉnh của người dùng (User Custom)", IsUserCustom = true }
        };
    }

    private DataGrid CreateRulesGrid()
    {
        DataGrid grid = new DataGrid
        {
            ItemsSource = Rules,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(246, 249, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 225, 234)),
            BorderThickness = new Thickness(1),
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            RowHeight = 30
        };
        Style headerStyle = new Style(typeof(DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(232, 239, 247))));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(37, 56, 76))));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
        grid.ColumnHeaderStyle = headerStyle;
        grid.Columns.Add(CreateTextColumn("Hệ thống", "System", 2.4));
        grid.Columns.Add(CreateTextColumn("Từ DN (mm)", "MinDN", 1));
        grid.Columns.Add(CreateTextColumn("Đến DN (mm)", "MaxDN", 1));
        grid.Columns.Add(CreateTextColumn("Độ dày (mm)", "ThicknessMM", 1));
        return grid;
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

    private void TargetModeRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        SwitchTarget(ductModeRadioButton.IsChecked == true ? MepTargetKind.Duct : MepTargetKind.Pipe, true);
    }

    private void SwitchTarget(MepTargetKind targetKind, bool changedByUser)
    {
        SelectedMepTarget = targetKind;
        bool isDuct = targetKind == MepTargetKind.Duct;
        Title = isDuct ? "BIM | Duct Insulation" : "BIM | Pipe Insulation";

        isLoadingPreset = true;
        insulationTypeComboBox.ItemsSource = isDuct ? ductInsulationTypes : pipeInsulationTypes;
        insulationTypeComboBox.SelectedIndex = insulationTypeComboBox.Items.Count > 0 ? 0 : -1;
        presetItems = CreatePresetItems(targetKind);
        presetComboBox.ItemsSource = null;
        presetComboBox.ItemsSource = presetItems;
        isLoadingPreset = false;

        selectedPipesRadioButton.Content = isDuct
            ? "Ống gió đang chọn (hoặc chọn trực tiếp nếu chưa chọn)"
            : "Đường ống đang chọn (hoặc chọn trực tiếp nếu chưa chọn)";
        allInViewRadioButton.Content = isDuct
            ? "Tất cả ống gió trong View"
            : "Tất cả đường ống trong View";
        if (rulesGrid.Columns.Count >= 3)
        {
            minSizeColumn = rulesGrid.Columns[1] as DataGridTextColumn;
            maxSizeColumn = rulesGrid.Columns[2] as DataGridTextColumn;
            if (minSizeColumn != null)
            {
                minSizeColumn.Header = isDuct ? "Từ cạnh max / đường kính (mm)" : "Từ DN (mm)";
            }
            if (maxSizeColumn != null)
            {
                maxSizeColumn.Header = isDuct ? "Đến cạnh max / đường kính (mm)" : "Đến DN (mm)";
            }
        }

        UpdateSystemOptions();
        List<PipeInsulationRule> customRules = isDuct ? savedDuctUserCustomRules : savedUserCustomRules;
        string defaultPresetKey = isDuct ? PipeInsulationRules.AllDuctsPresetKey : PipeInsulationRules.AllC1PresetKey;
        PipeInsulationPresetItem initialPreset = customRules.Count > 0
            ? FindPreset(PipeInsulationRules.UserCustomPresetKey)
            : FindPreset(defaultPresetKey);
        SelectPreset(initialPreset, false);
        AutoSwitchPresetForSelectedSystem();
        UpdateSystemTypeEnabledState();

        if (changedByUser)
        {
            presetStatusText.Text = isDuct
                ? "Đã chuyển sang Ống Gió. Danh sách type, system và preset đã được cập nhật."
                : "Đã chuyển sang Ống Nước. Danh sách type, system và preset đã được cập nhật.";
        }
    }

    private void UpdateSystemOptions()
    {
        IEnumerable<PipeInsulationSystemOption> source = SelectedMepTarget == MepTargetKind.Duct ? ductSystems : pipeSystems;
        List<PipeInsulationSystemOption> systemOptions = source
            .Where(option => option != null && !string.IsNullOrWhiteSpace(option.SystemName))
            .GroupBy(option => option.SystemName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PipeInsulationSystemOption
            {
                SystemName = group.First().SystemName,
                PipeCount = group.Sum(option => option.PipeCount)
            })
            .OrderBy(option => option.SystemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (systemOptions.Count == 0)
        {
            systemOptions.Add(new PipeInsulationSystemOption { SystemName = EmptySystemTypeMessage, PipeCount = 0 });
        }

        systemTypeComboBox.ItemsSource = systemOptions;
        systemTypeComboBox.SelectedIndex = 0;
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

    private void SystemTypeComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (bySystemTypeRadioButton != null && bySystemTypeRadioButton.IsChecked == true)
        {
            AutoSwitchPresetForSelectedSystem();
        }
    }

    private void AutoSwitchPresetForSelectedSystem()
    {
        PipeInsulationSystemOption selectedSystem = systemTypeComboBox == null
            ? null
            : systemTypeComboBox.SelectedItem as PipeInsulationSystemOption;
        if (selectedSystem == null || string.Equals(selectedSystem.SystemName, EmptySystemTypeMessage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string suggestedPresetKey = PipeInsulationRules.SuggestedPresetKey(SelectedMepTarget, selectedSystem.SystemName);
        if (!string.IsNullOrWhiteSpace(suggestedPresetKey))
        {
            SelectPreset(FindPreset(suggestedPresetKey), true);
        }
    }

    private void PresetComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isLoadingPreset)
        {
            return;
        }

        PipeInsulationPresetItem selectedPreset = presetComboBox.SelectedItem as PipeInsulationPresetItem;
        if (selectedPreset != null)
        {
            LoadPreset(selectedPreset, false);
        }
    }

    private void SelectPreset(PipeInsulationPresetItem preset, bool wasAutoSelected)
    {
        if (preset == null)
        {
            return;
        }

        isLoadingPreset = true;
        presetComboBox.SelectedItem = preset;
        isLoadingPreset = false;
        LoadPreset(preset, wasAutoSelected);
    }

    private void LoadPreset(PipeInsulationPresetItem preset, bool wasAutoSelected)
    {
        List<PipeInsulationRule> customRules = SelectedMepTarget == MepTargetKind.Duct
            ? savedDuctUserCustomRules
            : savedUserCustomRules;
        List<PipeInsulationRule> rules = preset.IsUserCustom && customRules.Count > 0
            ? CloneRules(customRules)
            : PipeInsulationRules.DefaultForPreset(SelectedMepTarget, preset.Key);
        ReplaceRules(rules);

        if (wasAutoSelected)
        {
            presetStatusText.Text = "Đã tự động chọn “" + preset.DisplayName + "” theo System Type trong view.";
        }
        else if (preset.IsUserCustom && customRules.Count == 0)
        {
            presetStatusText.Text = "Chưa có preset người dùng đã lưu. Bảng đang bắt đầu từ chuẩn Multi-System C1.";
        }
        else
        {
            presetStatusText.Text = "Đang dùng “" + preset.DisplayName + "”. Chỉnh sửa bảng và chọn Lưu Preset này để giữ lại làm User Custom.";
        }
    }

    private void ReloadPresetButtonClicked(object sender, RoutedEventArgs e)
    {
        PipeInsulationPresetItem selectedPreset = presetComboBox.SelectedItem as PipeInsulationPresetItem;
        if (selectedPreset == null)
        {
            return;
        }

        if (selectedPreset.IsUserCustom)
        {
            ReplaceRules(PipeInsulationRules.DefaultForPreset(
                SelectedMepTarget,
                SelectedMepTarget == MepTargetKind.Duct ? PipeInsulationRules.AllDuctsPresetKey : PipeInsulationRules.AllC1PresetKey));
            presetStatusText.Text = "Đã nạp lại chuẩn C1 Multi-System. Chọn Lưu Preset này nếu muốn thay User Custom.";
            return;
        }

        ReplaceRules(PipeInsulationRules.DefaultForPreset(SelectedMepTarget, selectedPreset.Key));
        presetStatusText.Text = "Đã nạp lại “" + selectedPreset.DisplayName + "” theo chuẩn C1.";
    }

    private void AddRowButtonClicked(object sender, RoutedEventArgs e)
    {
        CommitGridEdits();
        PipeInsulationSystemOption selectedSystem = systemTypeComboBox.SelectedItem as PipeInsulationSystemOption;
        string systemName = selectedSystem == null || string.Equals(selectedSystem.SystemName, EmptySystemTypeMessage, StringComparison.OrdinalIgnoreCase)
            ? "OTHER"
            : selectedSystem.SystemName;
        PipeInsulationRule newRule = new PipeInsulationRule
        {
            System = systemName,
            MinDN = 20,
            MaxDN = 40,
            ThicknessMM = 25
        };
        Rules.Add(newRule);
        rulesGrid.SelectedItem = newRule;
        rulesGrid.ScrollIntoView(newRule);
        presetStatusText.Text = "Đã thêm một dòng mới. Nhập dải DN và độ dày phù hợp.";
    }

    private void DeleteRowButtonClicked(object sender, RoutedEventArgs e)
    {
        PipeInsulationRule selectedRule = rulesGrid.SelectedItem as PipeInsulationRule;
        if (selectedRule == null)
        {
            presetStatusText.Text = "Chọn một dòng trong bảng trước khi xóa.";
            return;
        }

        Rules.Remove(selectedRule);
        presetStatusText.Text = "Đã xóa dòng quy tắc được chọn.";
    }

    private void SavePresetButtonClicked(object sender, RoutedEventArgs e)
    {
        List<PipeInsulationRule> validRules = GetValidRules();
        if (validRules.Count == 0)
        {
            MessageBox.Show("Vui lòng nhập ít nhất một rule hợp lệ trước khi lưu.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string saveError;
        string presetFileName = SelectedMepTarget == MepTargetKind.Duct ? DuctPresetFileName : PresetFileName;
        if (!SaveUserCustomRules(validRules, presetFileName, out saveError))
        {
            MessageBox.Show("Không thể lưu preset người dùng.\n" + saveError, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (SelectedMepTarget == MepTargetKind.Duct)
        {
            savedDuctUserCustomRules = CloneRules(validRules);
        }
        else
        {
            savedUserCustomRules = CloneRules(validRules);
        }
        SelectPreset(FindPreset(PipeInsulationRules.UserCustomPresetKey), false);
        presetStatusText.Text = "Đã lưu vào %APPDATA%\\BIN_PipeInsulation\\presets.json dưới tên User Custom.";
    }

    private void ApplyButtonClicked(object sender, RoutedEventArgs e)
    {
        SelectedMepTarget = ductModeRadioButton.IsChecked == true ? MepTargetKind.Duct : MepTargetKind.Pipe;
        List<PipeInsulationRule> validRules = GetValidRules();
        if (insulationTypeComboBox.SelectedItem == null)
        {
            MessageBox.Show("Vui lòng chọn Loại insulation.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (validRules.Count == 0)
        {
            MessageBox.Show("Vui lòng nhập ít nhất một rule hợp lệ (DN bắt đầu, DN kết thúc và độ dày phải lớn hơn 0).", Title, MessageBoxButton.OK, MessageBoxImage.Information);
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

        PipeInsulationSystemOption selectedSystem = systemTypeComboBox.SelectedItem as PipeInsulationSystemOption;
        SelectedSystemType = selectedSystem == null ? null : selectedSystem.SystemName;
        if (SelectedScope == PipeInsulationScope.BySystemType &&
            (string.IsNullOrWhiteSpace(SelectedSystemType) ||
             string.Equals(SelectedSystemType, EmptySystemTypeMessage, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("View hiện tại không có System Type để áp dụng.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedInsulationType = insulationTypeComboBox.SelectedItem as InsulationTypeItem;
        RemoveExisting = removeExistingCheckBox.IsChecked == true;
        SmoothTees = smoothTeesCheckBox.IsChecked == true;
        SelectedPresetName = (presetComboBox.SelectedItem as PipeInsulationPresetItem)?.DisplayName ?? "Tùy chỉnh";

        ReplaceRules(validRules);
        DialogResult = true;
        Close();
    }

    private List<PipeInsulationRule> GetValidRules()
    {
        CommitGridEdits();
        return Rules.Where(IsValidRule).Select(CloneRule).ToList();
    }

    private void CommitGridEdits()
    {
        rulesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        rulesGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void ReplaceRules(IEnumerable<PipeInsulationRule> rules)
    {
        Rules.Clear();
        foreach (PipeInsulationRule rule in rules ?? Enumerable.Empty<PipeInsulationRule>())
        {
            Rules.Add(CloneRule(rule));
        }
    }

    private PipeInsulationPresetItem FindPreset(string key)
    {
        return presetItems.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidRule(PipeInsulationRule rule)
    {
        return rule != null && !string.IsNullOrWhiteSpace(rule.System) && rule.MinDN >= 0 &&
               rule.MaxDN >= rule.MinDN && rule.ThicknessMM > 0;
    }

    private static PipeInsulationRule CloneRule(PipeInsulationRule rule)
    {
        return new PipeInsulationRule
        {
            System = rule == null ? "OTHER" : rule.System,
            MinDN = rule == null ? 0 : rule.MinDN,
            MaxDN = rule == null ? 0 : rule.MaxDN,
            ThicknessMM = rule == null ? 0 : rule.ThicknessMM
        };
    }

    private static List<PipeInsulationRule> CloneRules(IEnumerable<PipeInsulationRule> rules)
    {
        return (rules ?? Enumerable.Empty<PipeInsulationRule>()).Select(CloneRule).ToList();
    }

    private static List<PipeInsulationRule> LoadUserCustomRules(string fileName)
    {
        List<PipeInsulationRule> rules = LoadRulesFromFile(fileName);
        if (rules.Count > 0)
        {
            return rules;
        }

        // Legacy system_rules.json belongs only to the original pipe tool.
        return string.Equals(fileName, PresetFileName, StringComparison.OrdinalIgnoreCase)
            ? LoadRulesFromFile(LegacyRulesFileName)
            : new List<PipeInsulationRule>();
    }

    private static List<PipeInsulationRule> LoadRulesFromFile(string fileName)
    {
        try
        {
            if (!File.Exists(fileName))
            {
                return new List<PipeInsulationRule>();
            }

            string json = File.ReadAllText(fileName);
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
                    PipeInsulationRule rule = new PipeInsulationRule
                    {
                        System = JsonUnescape(match.Groups["system"].Value),
                        MinDN = min,
                        MaxDN = max,
                        ThicknessMM = thickness
                    };
                    if (IsValidRule(rule))
                    {
                        rules.Add(rule);
                    }
                }
            }
            return rules;
        }
        catch
        {
            // A malformed local settings file must not prevent the command from opening.
            return new List<PipeInsulationRule>();
        }
    }

    private static bool SaveUserCustomRules(IEnumerable<PipeInsulationRule> rules, string fileName, out string error)
    {
        try
        {
            List<PipeInsulationRule> rows = rules.Where(IsValidRule).ToList();
            Directory.CreateDirectory(SettingsDirectory);
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"schemaVersion\": 1,");
            json.AppendLine("  \"userCustom\": [");
            for (int index = 0; index < rows.Count; index++)
            {
                PipeInsulationRule rule = rows[index];
                string comma = index == rows.Count - 1 ? string.Empty : ",";
                json.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "    {{\"System\":\"{0}\",\"MinDN\":{1},\"MaxDN\":{2},\"ThicknessMM\":{3}}}{4}{5}",
                    JsonEscape(rule.System),
                    rule.MinDN,
                    rule.MaxDN,
                    rule.ThicknessMM,
                    comma,
                    Environment.NewLine);
            }
            json.AppendLine("  ]");
            json.AppendLine("}");
            File.WriteAllText(fileName, json.ToString(), new UTF8Encoding(false));
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
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static string JsonUnescape(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");
    }
}
