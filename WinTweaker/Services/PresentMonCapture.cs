using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualBasic.FileIO;

namespace WinTweaker.Services;

internal sealed class PresentMonCapture : IDisposable
{
    private readonly Process _process;
    private readonly FrameIntervals _intervals = new();
    public bool Exited => _process.HasExited;
    private volatile bool _accessDenied;

    public string ExitMessage
    {
        get
        {
            // Called after HasExited. Drain redirected output before reading the diagnostic.
            _process.WaitForExit();
            if (_accessDenied)
                return "Windows запретила измерение FPS. Запустите WinTweaker от имени администратора и нажмите FPS снова.";
            return _process.ExitCode == 0 ? "Захват FPS завершён." : $"Захват FPS завершился с ошибкой (код {_process.ExitCode}).";
        }
    }

    public PresentMonCapture(int pid)
    {
        var executable = System.IO.Path.Combine(AppContext.BaseDirectory, "ThirdParty", "PresentMon", "PresentMon.exe");
        if (!System.IO.File.Exists(executable))
            throw new System.IO.FileNotFoundException("В папке WinTweaker отсутствует компонент FPS. Восстановите файлы приложения.");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "--process_id", pid.ToString(CultureInfo.InvariantCulture), "--output_stdout",
                     "--v1_metrics", "--no_track_display", "--no_track_gpu", "--no_track_input",
                     "--no_console_stats", "--terminate_on_proc_exit", "--session_name", $"WinTweaker-{Guid.NewGuid():N}" })
            start.ArgumentList.Add(arg);
        _process = new Process { StartInfo = start };
        _process.OutputDataReceived += Receive;
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data?.Contains("access denied", StringComparison.OrdinalIgnoreCase) == true)
                _accessDenied = true;
        };
        try { _process.Start(); _process.BeginErrorReadLine(); _process.BeginOutputReadLine(); }
        catch { Dispose(); throw; }
    }

    private void Receive(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is { Length: > 0 } line) _intervals.ReadFrame(line);
    }

    public double? TakeFps() => _intervals.TakeFps();

    public void Dispose()
    {
        try { if (!_process.HasExited) _process.Kill(); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally { _process.Dispose(); }
    }
}

internal sealed class FrameIntervals
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (int Count, double Ms)> _frames = new();
    private int _chain = -1, _time;

    public void ReadFrame(string line)
    {
        using var csv = new TextFieldParser(new System.IO.StringReader(line));
        csv.SetDelimiters(",");
        string[]? fields;
        try { fields = csv.ReadFields(); }
        catch (MalformedLineException) { return; }
        if (fields is null) return;
        if (_chain < 0)
        {
            int chain = Array.IndexOf(fields, "SwapChainAddress");
            int time = Array.IndexOf(fields, "msBetweenPresents");
            if (chain >= 0 && time >= 0) { _chain = chain; _time = time; }
            return;
        }
        if (fields.Length <= Math.Max(_chain, _time) ||
            !double.TryParse(fields[_time], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) ||
            !double.IsFinite(ms) || ms <= 0) return;
        lock (_gate)
        {
            string key = fields[_chain];
            var value = _frames.GetValueOrDefault(key);
            _frames[key] = (value.Count + 1, value.Ms + ms);
        }
    }

    public double? TakeFps()
    {
        lock (_gate)
        {
            var best = _frames.Values.OrderByDescending(x => x.Count).FirstOrDefault();
            _frames.Clear();
            return best.Count > 0 ? 1000 * best.Count / best.Ms : null;
        }
    }

}
