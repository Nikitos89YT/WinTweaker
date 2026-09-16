using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace WinTweaker.Services
{
    public class StartupSystemItem
    {
        public string Name { get; set; } = "";

        public string Value { get; set; } = "";

        public string Source { get; set; } = "";

        public string Location { get; set; } = "";

        public string StatusText { get; set; } = "";

        public bool CanEdit { get; set; }

        public bool IsCritical { get; set; }

        public string DisplayName => Name;
    }

    public static class StartupSystemService
    {
        // =========================================================
        // WINLOGON
        // =========================================================

        private const string WinlogonSubKey =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

        // =========================================================
        // WINLOGON / SHELL / USERINIT
        // =========================================================

        public static List<StartupSystemItem> GetWinlogonItems()
        {
            var result =
                new List<StartupSystemItem>();

            try
            {
                using RegistryKey? key =
                    Registry.LocalMachine.OpenSubKey(
                        WinlogonSubKey,
                        writable: false);

                if (key == null)
                    return result;

                // -----------------------------------------------------
                // SHELL
                // -----------------------------------------------------

                string? shell =
                    key.GetValue(
                        "Shell",
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames)
                    as string;

                if (!string.IsNullOrWhiteSpace(shell))
                {
                    result.Add(
                        new StartupSystemItem
                        {
                            Name =
                                "Winlogon → Shell",

                            Value =
                                shell,

                            Source =
                                "Winlogon",

                            Location =
                                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell",

                            StatusText =
                                "🔒 Системный",

                            CanEdit =
                                false,

                            IsCritical =
                                true
                        });
                }

                // -----------------------------------------------------
                // USERINIT
                // -----------------------------------------------------

                string? userinit =
                    key.GetValue(
                        "Userinit",
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames)
                    as string;

                if (!string.IsNullOrWhiteSpace(userinit))
                {
                    result.Add(
                        new StartupSystemItem
                        {
                            Name =
                                "Winlogon → Userinit",

                            Value =
                                userinit,

                            Source =
                                "Winlogon",

                            Location =
                                @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Userinit",

                            StatusText =
                                "🔒 Системный",

                            CanEdit =
                                false,

                            IsCritical =
                                true
                        });
                }
            }
            catch
            {
            }

            return result;
        }

        // =========================================================
        // USERINIT
        // =========================================================

        public static StartupSystemItem? GetUserinit()
        {
            try
            {
                using RegistryKey? key =
                    Registry.LocalMachine.OpenSubKey(
                        WinlogonSubKey,
                        writable: false);

                if (key == null)
                    return null;

                string? userinit =
                    key.GetValue(
                        "Userinit",
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames)
                    as string;

                if (string.IsNullOrWhiteSpace(userinit))
                    return null;

                return new StartupSystemItem
                {
                    Name =
                        "Userinit",

                    Value =
                        userinit,

                    Source =
                        "Winlogon",

                    Location =
                        @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Userinit",

                    StatusText =
                        "🔒 Системный",

                    CanEdit =
                        false,

                    IsCritical =
                        true
                };
            }
            catch
            {
                return null;
            }
        }

        // =========================================================
        // LOGON TASKS
        // =========================================================

        /// <summary>
        /// Получает задачи Планировщика, имеющие LogonTrigger.
        /// Не зависит от языка Windows.
        /// </summary>
        public static List<StartupSystemItem> GetLogonTasks()
        {
            var result =
                new List<StartupSystemItem>();

            try
            {
                string script = @"
$tasks = Get-ScheduledTask -ErrorAction SilentlyContinue

$result = foreach ($task in $tasks) {

    $hasLogonTrigger = $false

    foreach ($trigger in @($task.Triggers)) {

        if ($null -eq $trigger) {
            continue
        }

        if ($trigger.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger') {
            $hasLogonTrigger = $true
            break
        }
    }

    if (-not $hasLogonTrigger) {
        continue
    }

    $actions = @(
        $task.Actions |
        ForEach-Object {

            $execute = [string]$_.Execute
            $arguments = [string]$_.Arguments

            if ([string]::IsNullOrWhiteSpace($arguments)) {
                $execute
            }
            else {
                $execute + ' ' + $arguments
            }
        }
    )

    [PSCustomObject]@{
        TaskName = [string]$task.TaskName
        TaskPath = [string]$task.TaskPath
        State    = [string]$task.State
        Action   = ($actions -join ' | ')
    }
}

if ($null -eq $result) {
    '[]'
}
else {
    $result | ConvertTo-Json -Compress -Depth 5
}
";

                string output =
                    RunPowerShellEncoded(script);

                if (string.IsNullOrWhiteSpace(output))
                    return result;

                // На случай, если PowerShell вернул
                // лишние пробелы/переводы строк.
                output =
                    output.Trim();

                if (string.IsNullOrWhiteSpace(output))
                    return result;

                using JsonDocument document =
                    JsonDocument.Parse(output);

                JsonElement root =
                    document.RootElement;

                // -----------------------------------------------------
                // Несколько задач
                // -----------------------------------------------------

                if (root.ValueKind ==
                    JsonValueKind.Array)
                {
                    foreach (JsonElement element
                             in root.EnumerateArray())
                    {
                        AddLogonTask(
                            result,
                            element);
                    }
                }

                // -----------------------------------------------------
                // Одна задача
                // -----------------------------------------------------

                else if (root.ValueKind ==
                         JsonValueKind.Object)
                {
                    AddLogonTask(
                        result,
                        root);
                }
            }
            catch
            {
                // Если Планировщик недоступен или
                // PowerShell вернул некорректные данные,
                // просто возвращаем пустой список.
            }

            return result;
        }

        private static void AddLogonTask(
            List<StartupSystemItem> result,
            JsonElement element)
        {
            string taskName =
                GetJsonString(
                    element,
                    "TaskName");

            string taskPath =
                GetJsonString(
                    element,
                    "TaskPath");

            string state =
                GetJsonString(
                    element,
                    "State");

            string action =
                GetJsonString(
                    element,
                    "Action");

            if (string.IsNullOrWhiteSpace(taskName))
                taskName =
                    "Неизвестная задача";

            if (string.IsNullOrWhiteSpace(taskPath))
                taskPath =
                    @"\";

            string fullTaskPath =
                taskPath + taskName;

            bool disabled =
                state.Equals(
                    "Disabled",
                    StringComparison.OrdinalIgnoreCase);

            result.Add(
                new StartupSystemItem
                {
                    Name =
                        $"Планировщик → {taskName}",

                    Value =
                        string.IsNullOrWhiteSpace(action)
                            ? "Команда не определена"
                            : action,

                    Source =
                        "Планировщик задач",

                    Location =
                        fullTaskPath,

                    StatusText =
                        disabled
                            ? "🟠 Отключено"
                            : "🟣 При входе",

                    CanEdit =
                        false,

                    IsCritical =
                        false
                });
        }

        // =========================================================
        // ALL SYSTEM STARTUP
        // =========================================================

        public static List<StartupSystemItem> GetAllSystemStartup()
        {
            var result =
                new List<StartupSystemItem>();

            result.AddRange(
                GetWinlogonItems());

            result.AddRange(
                GetLogonTasks());

            return result;
        }

        // =========================================================
        // OPEN WINLOGON LOCATION
        // =========================================================

        public static bool OpenWinlogonLocation()
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName =
                            "regedit.exe",

                        UseShellExecute =
                            true
                    });

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // POWERSHELL — ENCODED COMMAND
        // =========================================================

        private static string RunPowerShellEncoded(
            string script)
        {
            try
            {
                byte[] scriptBytes =
                    Encoding.Unicode.GetBytes(
                        script);

                string encodedCommand =
                    Convert.ToBase64String(
                        scriptBytes);

                using var process =
                    new Process();

                process.StartInfo =
                    new ProcessStartInfo
                    {
                        FileName =
                            "powershell.exe",

                        Arguments =
                            "-NoProfile " +
                            "-ExecutionPolicy Bypass " +
                            "-EncodedCommand " +
                            encodedCommand,

                        UseShellExecute =
                            false,

                        CreateNoWindow =
                            true,

                        RedirectStandardOutput =
                            true,

                        RedirectStandardError =
                            true,

                        StandardOutputEncoding =
                            Encoding.UTF8
                    };

                process.Start();

                string output =
                    process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                return output.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        // =========================================================
        // JSON
        // =========================================================

        private static string GetJsonString(
            JsonElement element,
            string propertyName)
        {
            if (!element.TryGetProperty(
                    propertyName,
                    out JsonElement property))
            {
                return "";
            }

            if (property.ValueKind ==
                JsonValueKind.String)
            {
                return property.GetString() ?? "";
            }

            return property.ToString();
        }
    }
}