using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows.Threading;

namespace WinTweaker.Services;

internal sealed record Metrics(double? Cpu, double? Gpu, double? Ram, double? Down, double? Up, double? TotalRamGb);

internal static class LiveMetrics
{
    private static readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private static Action<Metrics>? _listeners;
    private static bool _sampling;
    private static Metrics? _last;
    private static Dictionary<string, (long Rx, long Tx)>? _network;
    private static long _timestamp;

    static LiveMetrics() => Timer.Tick += Tick;

    public static void Subscribe(Action<Metrics> listener)
    {
        _listeners += listener;
        Timer.Start();
        if (_last is { } sample) listener(sample);
    }

    public static void Unsubscribe(Action<Metrics> listener)
    {
        _listeners -= listener;
        if (_listeners is null) { Timer.Stop(); _last = null; }
    }

    private static async void Tick(object? sender, EventArgs e)
    {
        if (_sampling) return;
        _sampling = true;
        try
        {
            var sample = await Task.Run(Sample);
            if (_listeners is { } listeners) { _last = sample; listeners(sample); }
        }
        finally { _sampling = false; }
    }

    private static Metrics Sample()
    {
        double? down = null, up = null;
        try
        {
            var now = Stopwatch.GetTimestamp();
            var next = new Dictionary<string, (long Rx, long Tx)>();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                try { var stats = nic.GetIPStatistics(); next[nic.Id] = (stats.BytesReceived, stats.BytesSent); }
                catch (NetworkInformationException) { }
            }
            var seconds = Stopwatch.GetElapsedTime(_timestamp, now).TotalSeconds;
            if (_network is { } previous && seconds is > 0 and < 5)
            {
                foreach (var (id, current) in next)
                {
                    if (!previous.TryGetValue(id, out var old) || current.Rx < old.Rx || current.Tx < old.Tx) continue;
                    down = (down ?? 0) + (current.Rx - old.Rx) * 0.000008 / seconds;
                    up = (up ?? 0) + (current.Tx - old.Tx) * 0.000008 / seconds;
                }
            }
            _network = next;
            _timestamp = now;
        }
        catch (NetworkInformationException) { _network = null; }
        return new(SystemInfoService.GetCpuUsage(), SystemInfoService.GetGpuUsage(), SystemInfoService.GetRamUsage(out var totalGb), down, up, totalGb);
    }
}
