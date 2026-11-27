using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WinTweaker.Services;

internal static class RestorePointService
{
    internal const string Created = "Новая точка восстановления Windows создана.";
    private static readonly SemaphoreSlim CreationGate = new(1, 1);

    public static async Task<string> CreateAsync()
    {
        if (!await CreationGate.WaitAsync(0))
            return "Запрос на создание точки уже выполняется.";
        try { return await RunCreationAsync(); }
        finally { CreationGate.Release(); }
    }

    private static async Task<string> RunCreationAsync()
    {
        // A successful cmdlet invocation is not proof that Windows created a new point.
        const string script = """
            $ErrorActionPreference = 'Stop'
            $description = 'WinTweaker ' + [Guid]::NewGuid().ToString('N')
            try {
                Checkpoint-Computer -Description $description -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
            } catch {
                exit 2
            }
            try {
                $point = Get-ComputerRestorePoint -ErrorAction Stop | Where-Object { $_.Description -eq $description }
                if ($point) { exit 0 }
                exit 3
            } catch {
                exit 4
            }
            """;
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var start = new ProcessStartInfo(
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить PowerShell.");
            await process.WaitForExitAsync();
            return process.ExitCode switch
            {
                0 => Created,
                2 => "Windows отклонила запрос. Проверьте, включена ли защита системного диска и не достигнут ли лимит частоты.",
                3 => "Новая точка не найдена. Windows может пропустить создание, если недавно уже была создана точка.",
                4 => "Не удалось проверить список точек Windows. Создание новой точки не подтверждено.",
                _ => $"Не удалось подтвердить создание точки (код {process.ExitCode})."
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return "Создание точки отменено в запросе администратора.";
        }
    }

    public static void OpenWizard()
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "rstrui.exe"))
        {
            UseShellExecute = true
        });
    }
}
