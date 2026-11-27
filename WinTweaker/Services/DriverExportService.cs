using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace WinTweaker.Services;

internal enum DriverExportStatus { Completed, NeedsAdministrator, Failed, Partial, NoFiles }

internal record DriverExportResult(DriverExportStatus Status, string? Destination, int? ExitCode, string Detail);

internal static class DriverExportService
{
    public static Task<DriverExportResult> ExportAsync(string parentFolder)
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return ExportAsync(parentFolder, new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator), RunAsync);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new DriverExportResult(DriverExportStatus.Failed, null, null, ex.Message));
        }
    }

    internal static async Task<DriverExportResult> ExportAsync(string parentFolder, bool isAdministrator, Func<ProcessStartInfo, Task<(int ExitCode, string Detail)>> run)
    {
        string? destination = null;
        int? exitCode = null;
        string detail;
        try
        {
            if (string.IsNullOrWhiteSpace(parentFolder) || !Directory.Exists(parentFolder))
                return new(DriverExportStatus.Failed, null, null, "Недопустимая родительская папка.");
            if (!isAdministrator)
                return new(DriverExportStatus.NeedsAdministrator, null, null, "Для экспорта требуются права администратора.");

            destination = Path.Combine(Path.GetFullPath(parentFolder), "DriverBackup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(destination);
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "pnputil.exe"))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            info.ArgumentList.Add("/export-driver");
            info.ArgumentList.Add("*");
            info.ArgumentList.Add(destination);
            (var code, detail) = await run(info);
            exitCode = code;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
        }

        if (destination is null || !Directory.Exists(destination))
            return new(DriverExportStatus.Failed, destination, exitCode, detail);

        try
        {
            var files = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories).ToArray();
            if (exitCode != 0)
                return new(files.Length > 0 ? DriverExportStatus.Partial : DriverExportStatus.Failed, destination, exitCode, detail);
            return new(files.Any(f => string.Equals(Path.GetExtension(f), ".inf", StringComparison.OrdinalIgnoreCase))
                ? DriverExportStatus.Completed : DriverExportStatus.NoFiles, destination, exitCode, detail);
        }
        catch (Exception ex)
        {
            return new(DriverExportStatus.Failed, destination, exitCode, detail + Environment.NewLine + ex.Message);
        }
    }

    private static async Task<(int ExitCode, string Detail)> RunAsync(ProcessStartInfo info)
    {
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Не удалось запустить pnputil.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, (await stdout) + (await stderr));
    }
}
