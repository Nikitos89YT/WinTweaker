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

        public static double GetCpuUsage()
        {
            try
            {
                EnsureCpuCounter();

                if (_cpuCounter == null)
                    return 0;

                double value = _cpuCounter.NextValue();

                return Math.Clamp(value, 0, 100);
            }
            catch
            {
                return 0;
            }
        }

        public static double GetRamUsage()
        {
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
                        return 0;

                    double used = totalKb - freeKb;
                    double percent = used / totalKb * 100;

                    return Math.Clamp(percent, 0, 100);
                }
            }
            catch
            {
                return 0;
            }

            return 0;
        }

        public static double GetGpuUsage()
        {
            try
            {
                PerformanceCounterCategory category =
                    new PerformanceCounterCategory("GPU Engine");

                string[] instances = category.GetInstanceNames();

                var gpuInstances = instances
                    .Where(instance =>
                        instance.Contains(
                            "engtype_3D",
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (gpuInstances.Length == 0)
                    return 0;

                double maxUsage = 0;

                foreach (string instance in gpuInstances)
                {
                    try
                    {
                        using PerformanceCounter counter =
                            new PerformanceCounter(
                                "GPU Engine",
                                "Utilization Percentage",
                                instance,
                                true);

                        // Первый замер прогревает счётчик.
                        counter.NextValue();

                        // Небольшая задержка здесь специально не нужна:
                        // наш общий таймер делает новый замер раз в секунду.
                        double value = counter.NextValue();

                        if (value > maxUsage)
                            maxUsage = value;
                    }
                    catch
                    {
                        // Один недоступный GPU engine не ломает весь мониторинг.
                    }
                }

                return Math.Clamp(maxUsage, 0, 100);
            }
            catch
            {
                return 0;
            }
        }
    }
}