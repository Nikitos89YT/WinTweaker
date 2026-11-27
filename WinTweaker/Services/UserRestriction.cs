using Microsoft.Win32;
using System.IO;

namespace WinTweaker.Services;

internal sealed record UserRestriction(string Title, string Description, string Path, string ValueName)
{
    private const string Policies = @"Software\Microsoft\Windows\CurrentVersion\Policies\";
    private const string Explorer = Policies + "Explorer";

    internal static readonly UserRestriction[] All =
    [
        new("Командная строка (CMD)", "Запрет запуска интерактивной командной строки.", @"Software\Policies\Microsoft\Windows\System", "DisableCMD"),
        new("PowerShell", "Запрет запуска powershell.exe и pwsh.exe через Проводник.", Explorer, "DisallowRun"),
        new("Выполнить (Win+R)", "Ограничение доступа к окну «Выполнить».", Explorer, "NoRun"),
        new("Панель управления и Параметры Windows", "Общий запрет на открытие панели управления и приложения «Параметры».", Explorer, "NoControlPanel"),
        new("Редактор реестра", "Запрет запуска редактора реестра Windows (regedit.exe).", Policies + "System", "DisableRegistryTools")
    ];

    private bool UsesApplicationList => ValueName == "DisallowRun";

    internal bool IsRestricted(RegistryKey root)
    {
        using var key = root.OpenSubKey(Path);
        var flag = key?.GetValue(ValueName);
        if (flag is null or 0)
            return false;
        if (flag is not int)
            throw new InvalidDataException($"Неожиданный тип параметра {ValueName}.");

        if (!UsesApplicationList)
            return true;

        using var list = key!.OpenSubKey(ValueName);
        return list != null && PowerShellEntries(list).Any();
    }

    internal void Restore(RegistryKey root)
    {
        if (!IsRestricted(root))
            return;

        if (UsesApplicationList)
        {
            using var list = root.OpenSubKey(Path + @"\" + ValueName, writable: true);
            if (list != null)
                foreach (var entry in PowerShellEntries(list))
                    list.DeleteValue(entry, throwOnMissingValue: false);
        }
        else
        {
            using var key = root.OpenSubKey(Path, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static IEnumerable<string> PowerShellEntries(RegistryKey list)
    {
        foreach (var name in list.GetValueNames())
        {
            if (list.GetValue(name) is string exe &&
                (exe.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                 exe.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase)))
                yield return name;
        }
    }
}
