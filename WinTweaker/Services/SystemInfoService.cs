using System;
using System.Diagnostics;
using System.Management;
using System.Linq;

namespace WinTweaker.Services
{
    public static class SystemInfoService
    {
        private static PerformanceCounter? _cpuCounter;

        private static void EnsureCpuCounter()
        {
            if (_cpuCounter != null)
                return;

            _cpuCounter = new PerformanceCounter(
                "Processor",
                "% Processor Time",
                "_Total",
                true);

            // Первый замер PerformanceCounter может вернуть 0.
            _cpuCounter.NextValue();
        }

        // =========================
        // STATIC SYSTEM INFORMATION
        // =========================

        public static string GetCpu()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Name FROM Win32_Processor");

                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj["Name"]?.ToString()?.Trim()
                           ?? "Неизвестно";
                }
            }
            catch
            {
                return "Не удалось определить";
            }

            return "Неизвестно";
        }

        public static string GetGpu()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Name, AdapterRAM, PNPDeviceID FROM Win32_VideoController");

                string? bestGpu = null;
                ulong bestRam = 0;

                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString()?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string pnpDeviceId =
                        obj["PNPDeviceID"]?.ToString()?.ToUpperInvariant() ?? "";

                    ulong ram = 0;

                    if (obj["AdapterRAM"] != null)
                    {
                        try
                        {
                            ram = Convert.ToUInt64(obj["AdapterRAM"]);
                        }
                        catch
                        {
                            ram = 0;
                        }
                    }

                    bool isVirtual =
                        name.Contains("Parsec", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
                        pnpDeviceId.Contains("ROOT\\", StringComparison.OrdinalIgnoreCase);

                    if (isVirtual)
                        continue;

                    if (bestGpu == null || ram > bestRam)
                    {
                        bestGpu = name;
                        bestRam = ram;
                    }
                }

                return bestGpu ?? "Не удалось определить";
            }
            catch
            {
                return "Не удалось определить";
            }
        }

        public static string GetRam()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");

                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["TotalPhysicalMemory"] is ulong bytes)
                    {
                        double gb = bytes / 1024.0 / 1024.0 / 1024.0;
                        return $"{gb:F1} ГБ";
                    }
                }
            }
            catch
            {
                return "Не удалось определить";
            }

            return "Неизвестно";
        }

        public static string GetWindows()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem");

                foreach (ManagementObject obj in searcher.Get())
                {
                    string caption =
                        obj["Caption"]?.ToString()?.Trim() ?? "Windows";

                    string version =
                        obj["Version"]?.ToString()?.Trim() ?? "";

                    string build =
                        obj["BuildNumber"]?.ToString()?.Trim() ?? "";

                    if (!string.IsNullOrWhiteSpace(version) &&
                        !string.IsNullOrWhiteSpace(build))
                    {
                        return $"{caption}\n{version} (Build {build})";
                    }

                    return caption;
                }
            }
            catch
            {
                return "Не удалось определить";
            }

            return "Неизвестно";
        }

        public static string GetMotherboard()
        {
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT Manufacturer, Product FROM Win32_BaseBoard");

                foreach (ManagementObject obj in searcher.Get())
                {
                    string manufacturer =
                        obj["Manufacturer"]?.ToString()?.Trim() ?? "";

                    string product =
                        obj["Product"]?.ToString()?.Trim() ?? "";

                    if (!string.IsNullOrWhiteSpace(manufacturer) &&
                        !string.IsNullOrWhiteSpace(product))
                    {
                        return $"{manufacturer} {product}";
                    }

                    if (!string.IsNullOrWhiteSpace(product))
                        return product;
                }
            }
            catch
            {
                return "Не удалось определить";
            }

            return "Неизвестно";
        }

        // =========================
        // LIVE MONITORING
        // =========================

        public static double? GetCpuUsage()
        {
            try
            {
                EnsureCpuCounter();

                if (_cpuCounter == null)
                    return null;

                double value = _cpuCounter.NextValue();

                return Math.Clamp(value, 0, 100);
            }
            catch
            {
                return null;
            }
        }

        public static double? GetRamUsage() => GetRamUsage(out _);

        internal static double? GetRamUsage(out double? totalGb)
        {
            totalGb = null;
            try
            {
                using ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(
                        "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");

                foreach (ManagementObject obj in searcher.Get())
                {
                    if (obj["TotalVisibleMemorySize"] == null ||
                        obj["FreePhysicalMemory"] == null)
                    {
                        continue;
                    }

                    double totalKb =
                        Convert.ToDouble(obj["TotalVisibleMemorySize"]);

                    double freeKb =
                        Convert.ToDouble(obj["FreePhysicalMemory"]);

                    if (totalKb <= 0)
                        return null;

                    totalGb = totalKb / (1024 * 1024);
                    double used = totalKb - freeKb;
                    double percent = used / totalKb * 100;

                    return Math.Clamp(percent, 0, 100);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static readonly Dictionary<string, PerformanceCounter> GpuCounters = new();

        public static double? GetGpuUsage()
        {
            try
            {
                var names = new PerformanceCounterCategory("GPU Engine").GetInstanceNames()
                    .Where(name => name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)).ToHashSet();
                foreach (var name in GpuCounters.Keys.Where(name => !names.Contains(name)).ToArray())
                {
                    GpuCounters[name].Dispose();
                    GpuCounters.Remove(name);
                }
                double? max = null;
                foreach (var name in names)
                {
                    try
                    {
                        if (GpuCounters.TryGetValue(name, out var counter))
                            max = Math.Max(max ?? 0, counter.NextValue());
                        else
                        {
                            var fresh = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                            try { fresh.NextValue(); GpuCounters.Add(name, fresh); }
                            catch { fresh.Dispose(); throw; }
                        }
                    }
                    catch
                    {
                        if (GpuCounters.Remove(name, out var failed)) failed.Dispose();
                    }
                }
                return max is double value && double.IsFinite(value) ? Math.Clamp(value, 0, 100) : null;
            }
            catch { return null; }
        }
    }
}
