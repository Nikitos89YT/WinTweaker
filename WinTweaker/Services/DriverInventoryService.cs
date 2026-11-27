using System.Management;

namespace WinTweaker.Services;

internal record DeviceInfo(string? Id, string Name, bool? Present, uint? ProblemCode);
internal record DriverInfo(string? DeviceId, string? Provider, string? Version, string? InfName, string? DeviceName = null);
internal record DriverRow(string Device, string? Provider, string? Version, string? InfName, string Status, bool HasProblem);
internal record DriverSnapshot(IReadOnlyList<DriverRow> Rows, IReadOnlyList<string> Warnings);

internal static class DriverInventoryService
{
    internal static Task<DriverSnapshot> LoadAsync() => Task.Run(() => Load(ReadDevices, ReadDrivers));

    internal static DriverSnapshot Load(Func<DeviceInfo[]> readDevices, Func<DriverInfo[]> readDrivers)
    {
        var warnings = new List<string>();
        DeviceInfo[] devices;
        DriverInfo[] drivers;
        try { devices = readDevices(); }
        catch (Exception ex) { warnings.Add($"Win32_PnPEntity: {ex.Message}"); devices = []; }
        try { drivers = readDrivers(); }
        catch (Exception ex) { warnings.Add($"Win32_PnPSignedDriver: {ex.Message}"); drivers = []; }

        var matched = new bool[drivers.Length];
        var rows = new List<DriverRow>();
        foreach (var device in devices)
        {
            int index = Array.FindIndex(drivers, d => !string.IsNullOrWhiteSpace(device.Id)
                && !string.IsNullOrWhiteSpace(d.DeviceId)
                && string.Equals(device.Id, d.DeviceId, StringComparison.OrdinalIgnoreCase));
            DriverInfo? driver = index >= 0 ? drivers[index] : null;
            if (index >= 0) matched[index] = true;
            var (status, problem) = GetStatus(device.Present, device.ProblemCode);
            rows.Add(new(device.Name, driver?.Provider, driver?.Version, driver?.InfName, status, problem));
        }
        for (int i = 0; i < drivers.Length; i++)
        {
            if (!matched[i])
            {
                var driver = drivers[i];
                rows.Add(new(driver.DeviceName ?? driver.DeviceId ?? "Неизвестное устройство", driver.Provider, driver.Version, driver.InfName, "Статус неизвестен", false));
            }
        }
        return new(rows, warnings);
    }

    private static (string, bool) GetStatus(bool? present, uint? code) => (present, code) switch
    {
        (false, _) => ("Отсутствует", false),
        (_, 22) => ("Отключено", true),
        (_, 28) => ("Драйверы не установлены", true),
        (_, > 0) => ($"Код Windows: {code}", true),
        (true, 0) => ("Исправно", false),
        _ => ("Статус неизвестен", false)
    };

    private static DeviceInfo[] ReadDevices()
    {
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT PNPDeviceID, Name, Present, ConfigManagerErrorCode FROM Win32_PnPEntity");
        using var results = searcher.Get();
        var devices = new List<DeviceInfo>();
        foreach (ManagementObject item in results)
        {
            using (item)
                devices.Add(new(item["PNPDeviceID"] as string, item["Name"] as string ?? "Неизвестное устройство", item["Present"] as bool?, item["ConfigManagerErrorCode"] as uint?));
        }
        return devices.ToArray();
    }

    private static DriverInfo[] ReadDrivers()
    {
        using var searcher = new ManagementObjectSearcher(@"root\cimv2", "SELECT DeviceID, DeviceName, DriverProviderName, DriverVersion, InfName FROM Win32_PnPSignedDriver");
        using var results = searcher.Get();
        var drivers = new List<DriverInfo>();
        foreach (ManagementObject item in results)
        {
            using (item)
                drivers.Add(new(item["DeviceID"] as string, item["DriverProviderName"] as string, item["DriverVersion"] as string, item["InfName"] as string, item["DeviceName"] as string));
        }
        return drivers.ToArray();
    }
}
