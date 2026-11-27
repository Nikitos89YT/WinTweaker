using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WinTweaker.Services;
using ChartPath = System.Windows.Shapes.Path;

namespace WinTweaker;

public partial class LiveMonitorWindow : Window
{
    private static LiveMonitorWindow? _open;
    internal static event Action? LockChanged;
    internal static bool IsLocked => _open is { _lockStyles: not 0 };
    private int _lockStyles;
    private readonly List<MetricRow> _rows;
    private PresentMonCapture? _capture;
    private ProcessFrameCounter? _frameCounter;
    private bool _graphs, _fps;
    private string _gameTitle = "";

    private LiveMonitorWindow()
    {
        InitializeComponent();
        _rows = new()
        {
            new("CPU", "%", 100), new("GPU", "%", 100), new("RAM", "%", 100),
            new("Сеть ↓", " Мбит/с"), new("Сеть ↑", " Мбит/с"), new("Кадры", " FPS")
        };
        foreach (var row in _rows) Rows.Children.Add(row.Panel);
        _rows[5].Panel.Visibility = Visibility.Collapsed;
        FpsStatus.Visibility = Visibility.Collapsed;
        ApplyMetricVisibility();
    }

    private OverlayOptions Options => SettingsService.Current.Overlay ??= new();

    private void ApplyMetricVisibility()
    {
        bool[] visible = [Options.Cpu, Options.Gpu, Options.Ram, Options.Download, Options.Upload];
        for (int i = 0; i < visible.Length; i++)
            _rows[i].Panel.Visibility = visible[i] ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Metrics_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = MetricsButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        void Option(string title, bool enabled, Action<bool> set)
        {
            var item = new MenuItem { Header = title, IsCheckable = true, IsChecked = enabled, StaysOpenOnClick = true };
            item.Click += (_, _) =>
            {
                set(item.IsChecked);
                ApplyMetricVisibility();
                if (!SettingsService.Save())
                    MessageBox.Show(this, "Не удалось сохранить настройки оверлея.", "WinTweaker");
            };
            menu.Items.Add(item);
        }
        Option("CPU", Options.Cpu, value => Options.Cpu = value);
        Option("GPU", Options.Gpu, value => Options.Gpu = value);
        Option("Оперативная память", Options.Ram, value => Options.Ram = value);
        Option("Сеть ↓ (приём)", Options.Download, value => Options.Download = value);
        Option("Сеть ↑ (передача)", Options.Upload, value => Options.Upload = value);
        menu.Items.Add(new Separator());
        Option("Память в ГБ (занято / всего)", Options.RamInGb, value => Options.RamInGb = value);
        menu.IsOpen = true;
    }

    internal static void Toggle()
    {
        if (_open is { } open) { open.Close(); return; }
        var window = new LiveMonitorWindow();
        window.Show();
        _open = window;
        Rect area = SystemParameters.WorkArea;
        window.Left = Math.Max(area.Left, area.Right - window.ActualWidth - 16);
        window.Top = area.Top + 16;
        LiveMetrics.Subscribe(window.UpdateMetrics);
    }

    protected override void OnClosed(EventArgs e)
    {
        UnlockPopup.IsOpen = false;
        LiveMetrics.Unsubscribe(UpdateMetrics);
        StopCapture();
        if (_open == this)
        {
            _open = null;
            LockChanged?.Invoke();
        }
        base.OnClosed(e);
    }

    private void UpdateMetrics(Metrics sample)
    {
        _rows[0].Add(sample.Cpu);
        _rows[1].Add(sample.Gpu);
        string? ramText = Options.RamInGb && sample.Ram is double percent && sample.TotalRamGb is double total
            ? $"{percent * total / 100:F1} ГБ / {total:0.#} ГБ" : null;
        _rows[2].Add(sample.Ram, ramText);
        _rows[3].Add(sample.Down);
        _rows[4].Add(sample.Up);
        if (!_fps) return;
        var fps = _frameCounter?.TakeFps();
        if (_capture is { Exited: true } && _frameCounter is { HasFrames: true, IsRunning: true })
        {
            _capture.Dispose();
            _capture = null;
        }
        if (_capture is { Exited: true } || (_capture == null && _frameCounter is { IsRunning: false }))
        {
            FpsStatus.Text = _capture?.ExitMessage ?? "Игра закрыта.";
            StopCapture();
            _fps = false;
            _rows[5].Reset();
            _rows[5].Panel.Visibility = Visibility.Collapsed;
            FpsButton.Tag = null;
            FpsButton.ToolTip = "Включить измерение FPS";
            return;
        }
        var presentMonFps = _capture?.TakeFps();
        fps ??= presentMonFps;
        _rows[5].Add(fps);
        FpsStatus.Text = fps is not null ? _gameTitle : $"{_gameTitle} — ожидание новых кадров.";
    }

    private void Graphs_Click(object sender, RoutedEventArgs e)
    {
        _graphs = !_graphs;
        GraphButton.Tag = _graphs ? "Active" : null;
        GraphButton.ToolTip = _graphs ? "Скрыть графики" : "Показать графики";
        foreach (var row in _rows) row.ShowGraph(_graphs);
    }

    private void Fps_Click(object sender, RoutedEventArgs e)
    {
        if (_fps)
        {
            StopCapture();
            _fps = false;
            _rows[5].Reset();
            _rows[5].Panel.Visibility = Visibility.Collapsed;
            FpsStatus.Visibility = Visibility.Collapsed;
            FpsButton.Tag = null;
            FpsButton.ToolTip = "Включить измерение FPS";
            return;
        }

        var games = Process.GetProcesses().Select(process =>
        {
            using (process)
            {
                try { return process.MainWindowHandle != IntPtr.Zero && process.Id != Environment.ProcessId
                    ? new GameChoice(process.Id, process.MainWindowTitle, process.StartTime) : null; }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException) { return null; }
            }
        }).OfType<GameChoice>().OrderBy(x => x.Title).ToArray();
        var list = new ListBox { ItemsSource = games, DisplayMemberPath = nameof(GameChoice.Label), MinHeight = 110, MaxHeight = 280 };
        if (games.Length > 0) list.SelectedIndex = 0;
        var dialog = new Window { Owner = this, Title = "Игра для замера FPS", Width = 460, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize };
        var content = new StackPanel { Margin = new Thickness(16) };
        content.Children.Add(new TextBlock { Text = games.Length > 0 ? "Выберите уже запущенную игру." : "Откройте игру и попробуйте снова.", Margin = new Thickness(0,0,0,8) });
        content.Children.Add(list);
        var confirm = new Button { Content = "Выбрать", IsDefault = true, IsEnabled = games.Length > 0, Margin = new Thickness(0,8,0,0), Padding = new Thickness(12,4,12,4) };
        confirm.Click += (_, _) => { if (list.SelectedItem is GameChoice) dialog.DialogResult = true; };
        content.Children.Add(confirm);
        dialog.Content = content;
        if (dialog.ShowDialog() != true || list.SelectedItem is not GameChoice game) return;

        try
        {
            using var process = Process.GetProcessById(game.Id);
            if (process.StartTime != game.Started || process.HasExited) throw new InvalidOperationException("Игра уже закрылась.");
            _frameCounter = new ProcessFrameCounter(game.Id);
            if (!_frameCounter.HasFrames) _capture = new PresentMonCapture(game.Id);
            _gameTitle = game.Title;
            FpsStatus.Text = $"{_gameTitle} — ожидание кадров.";
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or System.IO.IOException)
        {
            StopCapture();
            FpsStatus.Text = $"Не удалось запустить захват FPS: {ex.Message}";
            FpsStatus.Visibility = Visibility.Visible;
            return;
        }
        _fps = true;
        _rows[5].Panel.Visibility = Visibility.Visible;
        FpsStatus.Visibility = Visibility.Visible;
        FpsButton.Tag = "Active";
        FpsButton.ToolTip = "Выключить измерение FPS";
    }

    private void StopCapture()
    {
        _capture?.Dispose();
        _capture = null;
        _frameCounter?.Dispose();
        _frameCounter = null;
    }

    private void Drag_Header(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        if (IsLocked) return;
        const int extendedStyle = -20, transparent = 0x20, noActivate = 0x08000000;
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLong(handle, extendedStyle);
        var added = (transparent | noActivate) & ~style;
        if (!TrySetStyle(handle, style | added)) return;
        _lockStyles = added;
        LockedHint.Visibility = Visibility.Visible;
        // The popup has its own HWND, so only the padlock receives mouse clicks.
        UnlockPopup.IsOpen = true;
        LockChanged?.Invoke();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => Unlock();

    internal static void Unlock()
    {
        if (_open is not { } window || !IsLocked) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (!TrySetStyle(handle, GetWindowLong(handle, -20) & ~window._lockStyles)) return;
        window._lockStyles = 0;
        window.UnlockPopup.IsOpen = false;
        window.LockedHint.Visibility = Visibility.Collapsed;
        LockChanged?.Invoke();
    }

    private static bool TrySetStyle(IntPtr handle, int style)
    {
        if (SetWindowLong(handle, -20, style) != 0 || Marshal.GetLastPInvokeError() == 0) return true;
        MessageBox.Show("Не удалось изменить режим оверлея. " + new Win32Exception(Marshal.GetLastPInvokeError()).Message,
            "Live Monitor", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private sealed record GameChoice(int Id, string Title, DateTime Started)
    {
        public string Label => $"{Title} ({Id})";
    }

    private sealed class MetricRow
    {
        private readonly Queue<(long At, double? Value)> _values = new();
        private readonly TextBlock _value;
        private readonly ChartPath _chart;
        private readonly string _unit;
        private readonly double? _ceiling;
        public StackPanel Panel { get; } = new() { Margin = new Thickness(0, 3, 0, 3) };

        public MetricRow(string title, string unit, double? ceiling = null)
        {
            _unit = unit;
            _ceiling = ceiling;
            var header = new DockPanel();
            var ink = new SolidColorBrush(Color.FromRgb(241,242,247));
            _value = new TextBlock { Text = "—", Foreground = ink, FontWeight = FontWeights.SemiBold };
            DockPanel.SetDock(_value, Dock.Right);
            header.Children.Add(_value);
            header.Children.Add(new TextBlock { Text = title, Foreground = ink });
            Panel.Children.Add(header);
            _chart = new ChartPath { Stroke = new SolidColorBrush(Color.FromRgb(185,163,245)), StrokeThickness = 2,
                Width = 280, Height = 25, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed,
                ClipToBounds = true, Margin = new Thickness(0,3,0,1) };
            Panel.Children.Add(_chart);
        }

        public void Add(double? value, string? display = null)
        {
            var now = Stopwatch.GetTimestamp();
            _values.Enqueue((now, value));
            while (_values.TryPeek(out var head) && Stopwatch.GetElapsedTime(head.At, now).TotalSeconds > 60) _values.Dequeue();
            _value.Text = display ?? (value is double n ? $"{n.ToString(_unit.Contains("бит") ? "F1" : "F0")}{_unit}" : "—");
            if (_chart.Visibility == Visibility.Visible) Draw();
        }

        public void Reset() { _values.Clear(); _value.Text = "—"; _chart.Data = null; }
        public void ShowGraph(bool show) { _chart.Visibility = show ? Visibility.Visible : Visibility.Collapsed; if (show) Draw(); }

        private void Draw()
        {
            var latest = Stopwatch.GetTimestamp();
            double max = _ceiling ?? Math.Max(1, _values.Select(x => x.Value ?? 0).DefaultIfEmpty().Max());
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                bool connected = false;
                foreach (var (at, value) in _values)
                {
                    if (value is not double n) { connected = false; continue; }
                    var point = new Point(1 + 278 * Math.Clamp(1 - Stopwatch.GetElapsedTime(at, latest).TotalSeconds / 60, 0, 1), 24 - 23 * Math.Clamp(n / max, 0, 1));
                    if (connected) ctx.LineTo(point, true, false);
                    else ctx.BeginFigure(point, false, false);
                    connected = true;
                }
            }
            geometry.Freeze();
            _chart.Data = geometry;
            _chart.ToolTip = $"Последние 60 с · шкала 0–{max:0.#}{_unit}";
        }
    }
}
