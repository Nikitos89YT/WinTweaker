using System;
using System.Windows;
using System.Windows.Threading;
using WinTweaker.Services;

namespace WinTweaker.Pages
{
    public partial class HomePage
    {
        private readonly DispatcherTimer _monitorTimer;

        public HomePage()
        {
            InitializeComponent();

            LoadSystemInfo();

            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _monitorTimer.Tick += MonitorTimer_Tick;

            _monitorTimer.Start();

            // Первый живой замер
            UpdateMonitor();
        }

        private void LoadSystemInfo()
        {
            CpuText.Text = SystemInfoService.GetCpu();
            GpuText.Text = SystemInfoService.GetGpu();
            RamText.Text = SystemInfoService.GetRam();
            WindowsText.Text = SystemInfoService.GetWindows();
            MotherboardText.Text = SystemInfoService.GetMotherboard();
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            UpdateMonitor();
        }

        private void UpdateMonitor()
        {
            double cpu = SystemInfoService.GetCpuUsage();
            double ram = SystemInfoService.GetRamUsage();
            double gpu = SystemInfoService.GetGpuUsage();

            CpuUsageText.Text = $"{cpu:F0}%";
            CpuProgress.Value = cpu;

            RamUsageText.Text = $"{ram:F0}%";
            RamProgress.Value = ram;

            GpuUsageText.Text = $"{gpu:F0}%";
            GpuProgress.Value = gpu;
        }

        public void StopMonitoring()
        {
            _monitorTimer.Stop();
        }
    }
}