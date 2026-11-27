using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text;

namespace WinTweaker.Services
{
    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public string Status { get; set; } = "";

        public bool IsEnabled { get; set; }

        public string DisplayName => Name;
    }

    public class DnsPreset
    {
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public string PrimaryDns { get; set; } = "";

        public string SecondaryDns { get; set; } = "";

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PrimaryDns))
                    return Name;

                return $"{Name} — {PrimaryDns}, {SecondaryDns}";
            }
        }
    }

    public static class NetworkService
    {
        // =========================================================
        // ADAPTERS
        // =========================================================

        public static List<NetworkAdapterInfo> GetAdapters()
        {
            var result = new List<NetworkAdapterInfo>();

            string output = RunCommand(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command " +
                "\"[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
                "ConvertTo-Json -Compress -InputObject @(Get-NetAdapter | " +
                "Select-Object Name, InterfaceDescription, @{Name='State';Expression={[string]$_.Status}})\"");

            if (string.IsNullOrWhiteSpace(output))
                return result;

            using var adapters = JsonDocument.Parse(output);
            foreach (var item in adapters.RootElement.EnumerateArray())
            {
                string name = item.GetProperty("Name").GetString() ?? "";
                string description = item.GetProperty("InterfaceDescription").GetString() ?? "";
                string status = item.GetProperty("State").GetString() ?? "";

                bool enabled =
                    !status.Equals(
                        "Disabled",
                        StringComparison.OrdinalIgnoreCase);

                result.Add(new NetworkAdapterInfo
                {
                    Name = name,
                    Description = description,
                    Status = status,
                    IsEnabled = enabled
                });
            }

            return result;
        }

        public static bool EnableAdapter(string adapterName)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
                return false;

            string escapedName =
                EscapePowerShellString(adapterName);

            string command =
                $"Enable-NetAdapter -Name '{escapedName}' -Confirm:$false";

            return RunPowerShellCommand(command);
        }

        public static bool DisableAdapter(string adapterName)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
                return false;

            string escapedName =
                EscapePowerShellString(adapterName);

            string command =
                $"Disable-NetAdapter -Name '{escapedName}' -Confirm:$false";

            return RunPowerShellCommand(command);
        }

        // =========================================================
        // DNS
        // =========================================================

        public static bool FlushDns()
        {
            return RunProcess(
                "ipconfig.exe",
                "/flushdns") == 0;
        }

        public static string GetCurrentDns(string? adapterName)
        {
            if (string.IsNullOrEmpty(adapterName))
                return "Выберите сетевой адаптер";

            try
            {
                var adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                    item => string.Equals(item.Name, adapterName, StringComparison.OrdinalIgnoreCase));
                if (adapter == null)
                    return "Адаптер больше не доступен";

                var addresses = adapter.GetIPProperties().DnsAddresses.Distinct().ToArray();
                return addresses.Length == 0
                    ? "DNS-серверы не указаны"
                    : string.Join(", ", addresses.Select(address => address.ToString()));
            }
            catch (NetworkInformationException)
            {
                return "Не удалось определить DNS";
            }
        }

        public static List<DnsPreset> GetDnsPresets()
        {
            return new List<DnsPreset>
            {
                new DnsPreset
                {
                    Name = "Автоматически",
                    Description =
                        "Получать DNS автоматически от DHCP",
                    PrimaryDns = "",
                    SecondaryDns = ""
                },

                new DnsPreset
                {
                    Name = "Cloudflare",
                    Description =
                        "Публичный DNS Cloudflare",
                    PrimaryDns = "1.1.1.1",
                    SecondaryDns = "1.0.0.1"
                },

                new DnsPreset
                {
                    Name = "Google",
                    Description =
                        "Публичный DNS Google",
                    PrimaryDns = "8.8.8.8",
                    SecondaryDns = "8.8.4.4"
                },

                new DnsPreset
                {
                    Name = "Quad9",
                    Description =
                        "Публичный DNS Quad9",
                    PrimaryDns = "9.9.9.9",
                    SecondaryDns = "149.112.112.112"
                }
            };
        }

        public static bool SetDnsPreset(
            string adapterName,
            DnsPreset preset)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
                return false;

            if (preset == null)
                return false;

            string escapedName =
                EscapePowerShellString(adapterName);

            string command;

            if (string.IsNullOrWhiteSpace(
                    preset.PrimaryDns))
            {
                command =
                    $"Set-DnsClientServerAddress " +
                    $"-InterfaceAlias '{escapedName}' " +
                    "-ResetServerAddresses";
            }
            else
            {
                string primary =
                    EscapePowerShellString(
                        preset.PrimaryDns);

                string secondary =
                    EscapePowerShellString(
                        preset.SecondaryDns);

                command =
                    $"Set-DnsClientServerAddress " +
                    $"-InterfaceAlias '{escapedName}' " +
                    $"-ServerAddresses @('{primary}','{secondary}')";
            }

            return RunPowerShellCommand(command);
        }

        // =========================================================
        // WINSOCK / TCP-IP
        // =========================================================

        public static bool ResetWinsock()
        {
            return RunProcess(
                "netsh.exe",
                "winsock reset") == 0;
        }

        public static bool ResetTcpIp()
        {
            return RunProcess(
                "netsh.exe",
                "int ip reset") == 0;
        }

        // =========================================================
        // PROCESS HELPERS
        // =========================================================

        private static bool RunPowerShellCommand(
            string command)
        {
            string script = "$ErrorActionPreference = 'Stop'; " + command;
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            int exitCode = RunProcess(
                "powershell.exe",
                $"-NoProfile -EncodedCommand {encoded}");

            return exitCode == 0;
        }

        private static int RunProcess(
            string fileName,
            string arguments)
        {
            try
            {
                using var process = new Process();

                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };

                process.Start();
                process.WaitForExit();

                return process.ExitCode;
            }
            catch
            {
                return -1;
            }
        }

        private static string RunCommand(
            string fileName,
            string arguments)
        {
            try
            {
                using var process = new Process();

                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    StandardOutputEncoding = Encoding.UTF8
                };

                process.Start();

                string output =
                    process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                return process.ExitCode == 0 ? output : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static string EscapePowerShellString(
            string value)
        {
            return value.Replace(
                "'",
                "''");
        }

    }
}
