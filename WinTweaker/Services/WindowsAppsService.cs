using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinTweaker.Services;

internal sealed record WindowsApp(string Title, string Package, string StoreId)
{
    public string Family => Package + "_8wekyb3d8bbwe";
}

internal static class WindowsAppsService
{
    private static readonly WindowsApp[] Catalog =
    [
        new("Блокнот", "Microsoft.WindowsNotepad", "9MSMLRH6LZF3"),
        new("Диктофон", "Microsoft.WindowsSoundRecorder", "9WZDNCRFHWKN"),
        new("Калькулятор", "Microsoft.WindowsCalculator", "9WZDNCRFHVN5"),
        new("Камера", "Microsoft.WindowsCamera", "9WZDNCRFJBBG"),
        new("Пасьянсы Microsoft", "Microsoft.MicrosoftSolitaireCollection", "9WZDNCRFHWD2"),
        new("Погода", "Microsoft.BingWeather", "9WZDNCRFJ3Q2"),
        new("Фотографии", "Microsoft.Windows.Photos", "9WZDNCRFJBH4"),
        new("Часы", "Microsoft.WindowsAlarms", "9WZDNCRFJ3PR")
    ];

    internal static async Task<IReadOnlyList<WindowsAppRow>> ListAsync()
    {
        var output = await RunAsync("Get-AppxPackage -PackageTypeFilter Main | Select-Object -ExpandProperty PackageFamilyName");
        var installed = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                              .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Catalog.Select(app => new WindowsAppRow(app, installed.Contains(app.Family))).ToArray();
    }

    internal static async Task ChangeAsync(WindowsApp app, bool install)
    {
        if (!Catalog.Contains(app)) throw new ArgumentException("Неизвестное приложение.");
        const string lookup = "Get-AppxPackage -Name $env:WT_PACKAGE | Where-Object PackageFamilyName -eq $env:WT_FAMILY";
        string action = install
            ? $"if (!({lookup})) {{ Add-AppxPackage -RegisterByFamilyName -MainPackage $env:WT_FAMILY }}"
            : $"{lookup} | Remove-AppxPackage";
        await RunAsync(action, app);
    }

    private static async Task<string> RunAsync(string command, WindowsApp? app = null)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command",
            "$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false); " + command })
            start.ArgumentList.Add(arg);
        if (app != null)
        {
            start.Environment["WT_PACKAGE"] = app.Package;
            start.Environment["WT_FAMILY"] = app.Family;
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить Windows PowerShell.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(output, error, process.WaitForExitAsync());
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error.Result)
                ? $"Windows PowerShell завершился с кодом {process.ExitCode}." : error.Result.Trim());
        return output.Result;
    }
}

internal sealed record WindowsAppRow(WindowsApp App, bool Installed)
{
    public string Title => App.Title;
    public string Status => Installed ? "Установлено" : "Отсутствует";
}
