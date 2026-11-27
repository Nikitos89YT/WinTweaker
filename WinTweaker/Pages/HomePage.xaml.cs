using System.Windows;
using WinTweaker.Services;

namespace WinTweaker.Pages;

public partial class HomePage
{
    public HomePage()
    {
        InitializeComponent();
        CpuText.Text = SystemInfoService.GetCpu();
        GpuText.Text = SystemInfoService.GetGpu();
        RamText.Text = SystemInfoService.GetRam();
        WindowsText.Text = SystemInfoService.GetWindows();
        MotherboardText.Text = SystemInfoService.GetMotherboard();
        Loaded += (_, _) =>
        {
            LiveMonitorWindow.LockChanged += UpdateUnlockButton;
            UpdateUnlockButton();
            LiveMetrics.Subscribe(UpdateMonitor);
        };
        Unloaded += (_, _) =>
        {
            LiveMonitorWindow.LockChanged -= UpdateUnlockButton;
            LiveMetrics.Unsubscribe(UpdateMonitor);
        };
    }

    private void UpdateMonitor(Metrics sample)
    {
        CpuUsageText.Text = Format(sample.Cpu);
        RamUsageText.Text = Format(sample.Ram);
        GpuUsageText.Text = Format(sample.Gpu);
        CpuProgress.Value = sample.Cpu ?? 0;
        RamProgress.Value = sample.Ram ?? 0;
        GpuProgress.Value = sample.Gpu ?? 0;
    }

    private static string Format(double? value) => value is double n ? $"{n:F0}%" : "—";

    private void ShowOverlay_Click(object sender, RoutedEventArgs e) => LiveMonitorWindow.Toggle();
    private void UnlockOverlay_Click(object sender, RoutedEventArgs e) => LiveMonitorWindow.Unlock();
    private void UpdateUnlockButton() => UnlockOverlayButton.Visibility =
        LiveMonitorWindow.IsLocked ? Visibility.Visible : Visibility.Collapsed;
}
