using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BIN;

internal sealed class FlexAvoidSettings
{
    internal double ClearanceMm, InsulationMm;
    internal bool Up;
}
internal sealed class FlexAvoidReport
{
    internal string Text;
    internal bool Warning;
}

// All controls and vector assets are built in C#: no BAML/XAML loader.
public class AvoidMepWindow : Window
{
    private readonly Func<FlexAvoidSettings, bool, FlexAvoidReport> _execute;
    private readonly double _diameterMm, _detectedInsulationMm;
    private TextBox _clearance, _insulation;
    private ComboBox _direction;
    private TextBlock _status;
    private Border _statusCard;
    private Canvas _canvas;
    private Button _check, _avoid;
    private bool _running;
    internal bool HasExecuted { get; private set; }
    internal string LastError { get; private set; }

    internal AvoidMepWindow(string flexKind, string obstacle, double diameterMm, double insulationMm,
        string insulationSource, Func<FlexAvoidSettings, bool, FlexAvoidReport> execute)
    {
        _execute = execute;
        _diameterMm = diameterMm;
        _detectedInsulationMm = insulationMm;
        Title = "BIM TOOL · FLEX TRÁNH DẦM & BẢO ÔN";
        Width = 610;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height * 0.94;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Segoe UI");
        Background = Brush("#F1F5F9");
        var root = new StackPanel();
        var header = new StackPanel();
        var badge = new Border { Background = Brush("#1D4ED8"), CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 4, 9, 4), HorizontalAlignment = HorizontalAlignment.Left };
        badge.Child = Label(flexKind + "  /  HOST + LINK", 11, "#DBEAFE", FontWeights.SemiBold);
        header.Children.Add(badge);
        header.Children.Add(Label("Kiểm tra & né dầm", 25, "#FFFFFF", FontWeights.SemiBold, new Thickness(0, 9, 0, 0)));
        header.Children.Add(Label("Tính cả vỏ bảo ôn · kiểm tra Solid 3D và spline thực", 12, "#CBD5E1", FontWeights.Normal, new Thickness(0, 4, 0, 0)));
        header.Children.Add(Label(obstacle, 12, "#BFDBFE", FontWeights.Normal, new Thickness(0, 8, 0, 0)));
        root.Children.Add(new Border { Background = new LinearGradientBrush(Color.FromRgb(15, 23, 42),
            Color.FromRgb(30, 58, 138), 25), Padding = new Thickness(22, 18, 22, 18), Child = header });

        var body = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        _canvas = new Canvas { Width = 550, Height = 205, ClipToBounds = true };
        body.Children.Add(Card(new Viewbox { Child = _canvas, Stretch = Stretch.Uniform }, new Thickness(0, 0, 0, 12)));

        var controls = new StackPanel();
        controls.Children.Add(Label("THÔNG SỐ BAO NGOÀI", 11, "#475569", FontWeights.SemiBold));
        controls.Children.Add(Label("Đường kính danh nghĩa: " + Number(diameterMm) + " mm", 12, "#0F172A",
            FontWeights.SemiBold, new Thickness(0, 5, 0, 12)));
        var fields = new Grid();
        fields.ColumnDefinitions.Add(new ColumnDefinition());
        fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        fields.ColumnDefinitions.Add(new ColumnDefinition());
        _clearance = Input("50", "Khoảng hở an toàn (mm)");
        _insulation = Input(Number(insulationMm), "Chiều dày bảo ôn (mm)");
        var clearanceGroup = Field("Khoảng hở an toàn · mm", _clearance);
        var insulationGroup = Field("Chiều dày bảo ôn · mm", _insulation);
        Grid.SetColumn(insulationGroup, 2);
        fields.Children.Add(clearanceGroup); fields.Children.Add(insulationGroup);
        controls.Children.Add(fields);
        controls.Children.Add(Label("Tự nhận diện: " + Number(insulationMm) + " mm · " + insulationSource, 11,
            "#64748B", FontWeights.Normal, new Thickness(0, 7, 0, 0)));
        var reset = new Button { Content = "Dùng lại bảo ôn tự nhận diện", HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent, Foreground = Brush("#2563EB"), BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 4, 0, 4), FontSize = 11, Cursor = Cursors.Hand };
        reset.Click += (s, e) => _insulation.Text = Number(_detectedInsulationMm);
        controls.Children.Add(reset);
        controls.Children.Add(Label("Hướng né", 12, "#334155", FontWeights.SemiBold, new Thickness(0, 8, 0, 5)));
        _direction = new ComboBox { FontSize = 13, Padding = new Thickness(10, 6, 10, 6),
            ItemsSource = new[] { "Xuống dưới đáy dầm (mặc định)", "Lên trên đỉnh dầm" }, SelectedIndex = 0 };
        AutomationProperties.SetName(_direction, "Hướng né dầm");
        controls.Children.Add(_direction);
        controls.Children.Add(Label("R = Ø/2 + bảo ôn + Clearance. Giá trị ghi đè chỉ dùng tính toán, không đổi lớp insulation trong mô hình.",
            11, "#64748B", FontWeights.Normal, new Thickness(0, 10, 0, 0)));
        body.Children.Add(Card(controls, new Thickness(0, 0, 0, 12)));

        _status = Label("Chọn CHECK để xem khoảng hở và độ ăn sâu. AUTO-AVOID giữ nguyên hai đầu, kiểm tra lại spline và rollback nếu không đạt.",
            12, "#334155", FontWeights.Normal);
        _statusCard = Card(_status, new Thickness(0, 0, 0, 12));
        _statusCard.Background = Brush("#EFF6FF");
        body.Children.Add(_statusCard);
        var buttons = new Grid();
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        _check = ActionButton("KIỂM TRA VA CHẠM\nCHECK", "#E2E8F0", "#0F172A");
        _avoid = ActionButton("TỰ ĐỘNG NÉ DẦM\nAUTO-AVOID", "#2563EB", "#FFFFFF");
        _check.Click += (s, e) => Run(false);
        _avoid.Click += (s, e) => Run(true);
        Grid.SetColumn(_avoid, 2);
        buttons.Children.Add(_check); buttons.Children.Add(_avoid);
        body.Children.Add(buttons);
        body.Children.Add(Label("CHECK: vật cản đã chọn · Auto-Avoid: kiểm tra thêm Solid lân cận\nENTER: Auto-Avoid · ESC: đóng", 11, "#64748B",
            FontWeights.Normal, new Thickness(0, 10, 0, 0)));
        root.Children.Add(body);
        Content = new ScrollViewer { Content = root, Background = Brush("#F1F5F9"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        _clearance.TextChanged += (s, e) => SettingsChanged();
        _insulation.TextChanged += (s, e) => SettingsChanged();
        _direction.SelectionChanged += (s, e) => SettingsChanged();
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape && !_running) { e.Handled = true; Close(); }
            else if (e.Key == Key.Enter && !_running) { e.Handled = true; Run(true); }
        };
        UpdateDiagram();
    }

    private void SettingsChanged()
    {
        UpdateDiagram();
        _status.Text = "Thông số đã thay đổi. Chạy CHECK hoặc AUTO-AVOID để xác minh với giá trị hiện tại.";
        _status.Foreground = Brush("#334155");
        _statusCard.Background = Brush("#EFF6FF");
    }

    private void Run(bool auto)
    {
        if (_running) return;
        if (!TryValue(_clearance.Text, 5000, out double clearance) || !TryValue(_insulation.Text, 2000, out double insulation))
        {
            LastError = "Nhập Clearance từ 0–5000 mm và bảo ôn từ 0–2000 mm bằng số hữu hạn.";
            Status(LastError, true);
            return;
        }
        _running = true;
        _check.IsEnabled = _avoid.IsEnabled = false;
        Cursor = Cursors.Wait;
        try
        {
            FlexAvoidReport report = _execute(new FlexAvoidSettings { ClearanceMm = clearance, InsulationMm = insulation,
                Up = _direction.SelectedIndex == 1 }, auto);
            HasExecuted = true;
            LastError = null;
            Status(report.Text, report.Warning);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Status("CHƯA THỰC HIỆN ĐƯỢC\n" + ex.Message, true);
        }
        finally { _running = false; _check.IsEnabled = _avoid.IsEnabled = true; Cursor = Cursors.Arrow; }
    }

    private void Status(string text, bool warning)
    {
        _status.Text = text;
        _status.Foreground = Brush(warning ? "#9A3412" : "#166534");
        _statusCard.Background = Brush(warning ? "#FFF7ED" : "#F0FDF4");
    }

    private void UpdateDiagram()
    {
        _canvas.Children.Clear();
        bool up = _direction?.SelectedIndex == 1;
        double insulation = TryValue(_insulation.Text, 2000, out double i) ? i : _detectedInsulationMm;
        double clearance = TryValue(_clearance.Text, 5000, out double c) ? c : 50;
        void Rect(double x, double y, double width, double height)
        {
            var rectangle = new Rectangle { Width = width, Height = height, Fill = Brush("#475569"), RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(rectangle, x); Canvas.SetTop(rectangle, y); _canvas.Children.Add(rectangle);
        }
        void Caption(string text, double x, double y, string color = "#64748B", double size = 11)
        {
            TextBlock label = Label(text, size, color, FontWeights.Normal);
            Canvas.SetLeft(label, x); Canvas.SetTop(label, y); _canvas.Children.Add(label);
        }
        Caption("MẶT ĐỨNG NGUYÊN LÝ · KHÔNG THEO TỶ LỆ", 10, 8, "#64748B", 10);
        double top = up ? 91 : 38, bottom = top + 62;
        Rect(238, top, 88, 12); Rect(275, top + 12, 14, 38); Rect(238, bottom - 12, 88, 12);
        Caption("Dầm I / Solid 3D", 353, top + 20, "#334155");
        double y = up ? 49 : 148;
        double endpointY = up ? 134 : 61;
        var path = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(30, endpointY), IsClosed = false };
        figure.Segments.Add(new BezierSegment(new Point(120, endpointY), new Point(155, y), new Point(210, y), true));
        figure.Segments.Add(new BezierSegment(new Point(245, y), new Point(317, y), new Point(350, y), true));
        figure.Segments.Add(new BezierSegment(new Point(404, y), new Point(431, endpointY), new Point(515, endpointY), true));
        path.Figures.Add(figure);
        double shell = 11 + Math.Min(11, insulation / Math.Max(1, _diameterMm) * 24);
        _canvas.Children.Add(new Path { Data = path, Stroke = Brush("#BAE6FD"), StrokeThickness = shell,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        _canvas.Children.Add(new Path { Data = path, Stroke = Brush("#0284C7"), StrokeThickness = 8,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        _canvas.Children.Add(new Path { Data = path, Stroke = Brush("#FFFFFF"), StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 3 } });
        double faceY = up ? top : bottom, shellY = up ? y + shell / 2 : y - shell / 2;
        _canvas.Children.Add(new Line { X1 = 282, Y1 = faceY, X2 = 282, Y2 = shellY, Stroke = Brush("#16A34A"), StrokeThickness = 1.5 });
        foreach (double yy in new[] { faceY, shellY })
            _canvas.Children.Add(new Line { X1 = 276, Y1 = yy, X2 = 288, Y2 = yy, Stroke = Brush("#16A34A"), StrokeThickness = 1.5 });
        Caption(Number(clearance) + " mm", 297, (faceY + shellY) / 2 - 8, "#15803D");
        Caption("Tâm ống", 20, 176, "#0284C7");
        Caption("Vỏ bảo ôn: " + Number(insulation) + " mm", 137, 176, "#0369A1");
        Caption(up ? "Né lên trên" : "Né xuống dưới đáy dầm", 354, 176, "#15803D");
    }

    internal static bool TryValue(string text, double max, out double value)
    {
        bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        return parsed && !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= max;
    }
    private static string Number(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
    private static SolidColorBrush Brush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Label(string text, double size, string color, FontWeight weight, Thickness? margin = null)
        => new TextBlock { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = weight,
            TextWrapping = TextWrapping.Wrap, Margin = margin ?? new Thickness(0) };
    private static Border Card(UIElement content, Thickness margin)
        => new Border { Child = content, Background = Brushes.White, CornerRadius = new CornerRadius(10),
            BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1), Padding = new Thickness(14), Margin = margin };
    private static TextBox Input(string value, string accessibleName)
    {
        var box = new TextBox { Text = value, FontSize = 16, Padding = new Thickness(10, 7, 10, 7),
            Background = Brush("#F8FAFC"), BorderBrush = Brush("#CBD5E1"), BorderThickness = new Thickness(1) };
        AutomationProperties.SetName(box, accessibleName);
        return box;
    }
    private static StackPanel Field(string name, TextBox input)
    {
        var group = new StackPanel();
        group.Children.Add(Label(name, 12, "#334155", FontWeights.SemiBold, new Thickness(0, 0, 0, 6)));
        group.Children.Add(input);
        return group;
    }
    private static Button ActionButton(string title, string background, string foreground)
        => new Button { Content = new TextBlock { Text = title, TextAlignment = TextAlignment.Center, FontSize = 12,
            FontWeight = FontWeights.SemiBold }, Background = Brush(background), Foreground = Brush(foreground),
            BorderThickness = new Thickness(0), Padding = new Thickness(9, 12, 9, 12), Cursor = Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Center };
}
