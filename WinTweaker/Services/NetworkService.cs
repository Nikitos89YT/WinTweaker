using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
                "\"Get-NetAdapter | " +
                "Select-Object Name, InterfaceDescription, Status | " +
                "ConvertTo-Csv -NoTypeInformation\"");

            if (string.IsNullOrWhiteSpace(output))
                return result;

            string[] lines = output
                .Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= 1)
                return result;

            foreach (string line in lines.Skip(1))
            {
                string[] parts = ParseCsvLine(line);

                if (parts.Length < 3)
                    continue;

                string name = parts[0];
                string description = parts[1];
                string status = parts[2];

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

        public static string GetCurrentDns()
        {
            try
            {
                var result = new List<string>();

                foreach (NetworkInterface networkInterface
                         in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (networkInterface.NetworkInterfaceType ==
                        NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    string interfaceName =
                        networkInterface.Name;

                    string description =
                        networkInterface.Description;

                    string combinedName =
                        $"{interfaceName} {description}";

                    if (combinedName.Contains(
                            "Radmin",
                            StringComparison.OrdinalIgnoreCase) ||
                        combinedName.Contains(
                            "Virtual",
                            StringComparison.OrdinalIgnoreCase) ||
                        combinedName.Contains(
                            "TAP",
                            StringComparison.OrdinalIgnoreCase) ||
                        combinedName.Contains(
                            "Loopback",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    IPInterfaceProperties properties;

                    try
                    {
                        properties =
                            networkInterface.GetIPProperties();
                    }
                    catch
                    {
                        continue;
                    }

                    var ipv4Dns = properties
                        .DnsAddresses
                        .Where(address =>
                            address.AddressFamily ==
                            AddressFamily.InterNetwork)
                        .Select(address => address.ToString())
                        .Distinct()
                        .ToList();

                    if (ipv4Dns.Count == 0)
                        continue;

                    result.Add(
                        $"{interfaceName} : {string.Join(", ", ipv4Dns)}");
                }

                if (result.Count == 0)
                    return "DNS не настроен";

                return string.Join(
                    Environment.NewLine,
                    result);
            }
            catch
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
            int exitCode = RunProcess(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"");

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
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                process.Start();

                string output =
                    process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                return output;
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

        private static string[] ParseCsvLine(
            string line)
        {
            var values =
                new List<string>();

            var current =
                new StringBuilder();

            bool insideQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    insideQuotes = !insideQuotes;
                    continue;
                }

                if (c == ',' && !insideQuotes)
                {
                    values.Add(
                        current.ToString());

                    current.Clear();

                    continue;
                }

                current.Append(c);
            }

            values.Add(
                current.ToString());

            return values.ToArray();
        }
    }
}