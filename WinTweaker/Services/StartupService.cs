using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using WinTweaker.Models;

namespace WinTweaker.Services
{
    public static class StartupService
    {
        private const string RunKey =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        private const string RunOnceKey =
            @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

        private const string StartupApprovedBaseKey =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

        private const string StartupApprovedRun =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        private const string StartupApprovedStartupFolder =
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        /*
         * Windows хранит состояние Startup примерно так:
         *
         * 02 ... = разрешено
         * 03 ... = отключено
         *
         * Остальные байты стараемся сохранять.
         */

        private const byte EnabledState = 0x02;
        private const byte DisabledState = 0x03;

        // ============================================================
        // ПУБЛИЧНЫЙ СКАНЕР
        // ============================================================

        public static List<StartupItem> GetStartupItems()
        {
            List<StartupItem> items = new();

            /*
             * На случай, если старая версия WinTweaker
             * успела перенести что-то в свои временные ключи,
             * пытаемся аккуратно вернуть это обратно.
             */
            MigrateLegacyDisabledRegistry(
                Registry.CurrentUser,
                RegistryHive.CurrentUser,
                RunKey,
                "Registry (текущий пользователь)",
                items);

            MigrateLegacyDisabledRegistry(
                Registry.LocalMachine,
                RegistryHive.LocalMachine,
                RunKey,
                "Registry (все пользователи)",
                items);

            // Обычный Run
            ReadRegistryRun(
                Registry.CurrentUser,
                RegistryHive.CurrentUser,
                RunKey,
                StartupApprovedRun,
                "Registry (текущий пользователь)",
                items);

            ReadRegistryRun(
                Registry.LocalMachine,
                RegistryHive.LocalMachine,
                RunKey,
                StartupApprovedRun,
                "Registry (все пользователи)",
                items);

            // RunOnce показываем, но пока не даём переключать.
            ReadRegistryRunOnce(
                Registry.CurrentUser,
                RegistryHive.CurrentUser,
                RunOnceKey,
                "Registry RunOnce (текущий пользователь)",
                items);

            ReadRegistryRunOnce(
                Registry.LocalMachine,
                RegistryHive.LocalMachine,
                RunOnceKey,
                "Registry RunOnce (все пользователи)",
                items);

            // Startup Folder
            ReadStartupFolder(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Startup),
                "Startup Folder (текущий пользователь)",
                RegistryHive.CurrentUser,
                items);

            ReadStartupFolder(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonStartup),
                "Startup Folder (все пользователи)",
                RegistryHive.LocalMachine,
                items);

            return items;
        }

        // ============================================================
        // REGISTRY RUN
        // ============================================================

        private static void ReadRegistryRun(
            RegistryKey root,
            RegistryHive hive,
            string subKey,
            string approvedKey,
            string source,
            List<StartupItem> items)
        {
            try
            {
                using RegistryKey? key =
                    root.OpenSubKey(subKey);

                if (key == null)
                    return;

                foreach (string valueName in key.GetValueNames())
                {
                    object? value = key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);

                    if (value == null)
                        continue;

                    RegistryValueKind kind;

                    try
                    {
                        kind = key.GetValueKind(valueName);
                    }
                    catch
                    {
                        continue;
                    }

                    bool enabled =
                        GetStartupApprovedState(
                            root,
                            approvedKey,
                            valueName);

                    items.Add(new StartupItem
                    {
                        Name = valueName,
                        Command = value.ToString() ?? "",
                        Source = source,
                        Location =
                            $"{GetHiveName(hive)}\\{subKey}",

                        IsEnabled = enabled,

                        ItemType = "Registry",

                        RegistryRoot =
                            GetHiveName(hive),

                        RegistrySubKey =
                            subKey,

                        RegistryValueName =
                            valueName,

                        RegistryValueKind =
                            (int)kind,

                        StartupApprovedSubKey =
                            approvedKey,

                        CanToggle = true
                    });
                }
            }
            catch
            {
                // Один источник не должен ломать весь сканер.
            }
        }

        // ============================================================
        // REGISTRY RUNONCE
        // ============================================================

        private static void ReadRegistryRunOnce(
            RegistryKey root,
            RegistryHive hive,
            string subKey,
            string source,
            List<StartupItem> items)
        {
            try
            {
                using RegistryKey? key =
                    root.OpenSubKey(subKey);

                if (key == null)
                    return;

                foreach (string valueName in key.GetValueNames())
                {
                    object? value = key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);

                    if (value == null)
                        continue;

                    RegistryValueKind kind;

                    try
                    {
                        kind = key.GetValueKind(valueName);
                    }
                    catch
                    {
                        continue;
                    }

                    items.Add(new StartupItem
                    {
                        Name = valueName,
                        Command = value.ToString() ?? "",
                        Source = source,
                        Location =
                            $"{GetHiveName(hive)}\\{subKey}",

                        /*
                         * RunOnce — одноразовый источник.
                         * Пока просто показываем его.
                         */
                        IsEnabled = true,

                        ItemType = "Registry",

                        RegistryRoot =
                            GetHiveName(hive),

                        RegistrySubKey =
                            subKey,

                        RegistryValueName =
                            valueName,

                        RegistryValueKind =
                            (int)kind,

                        CanToggle = false
                    });
                }
            }
            catch
            {
                // Игнорируем недоступный источник.
            }
        }

        // ============================================================
        // STARTUP FOLDER
        // ============================================================

        private static void ReadStartupFolder(
            string? folderPath,
            string source,
            RegistryHive hive,
            List<StartupItem> items)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) ||
                    !Directory.Exists(folderPath))
                {
                    return;
                }

                // Файлы
                foreach (string file in Directory.GetFiles(folderPath))
                {
                    string fileName =
                        Path.GetFileName(file);

                    bool enabled =
                        GetStartupApprovedState(
                            hive == RegistryHive.CurrentUser
                                ? Registry.CurrentUser
                                : Registry.LocalMachine,
                            StartupApprovedStartupFolder,
                            fileName);

                    items.Add(new StartupItem
                    {
                        Name = fileName,
                        Command = file,
                        Source = source,
                        Location = folderPath,
                        IsEnabled = enabled,
                        ItemType = "StartupFolder",
                        OriginalPath = file,
                        StartupApprovedSubKey =
                            StartupApprovedStartupFolder,
                        CanToggle = true
                    });
                }

                // Папки
                foreach (string directory in Directory.GetDirectories(folderPath))
                {
                    string directoryName =
                        Path.GetFileName(directory);

                    bool enabled =
                        GetStartupApprovedState(
                            hive == RegistryHive.CurrentUser
                                ? Registry.CurrentUser
                                : Registry.LocalMachine,
                            StartupApprovedStartupFolder,
                            directoryName);

                    items.Add(new StartupItem
                    {
                        Name = directoryName,
                        Command = directory,
                        Source = source,
                        Location = folderPath,
                        IsEnabled = enabled,
                        ItemType = "StartupFolder",
                        OriginalPath = directory,
                        StartupApprovedSubKey =
                            StartupApprovedStartupFolder,
                        CanToggle = true
                    });
                }
            }
            catch
            {
                // Игнорируем недоступную папку.
            }
        }

        // ============================================================
        // ПОЛУЧЕНИЕ СОСТОЯНИЯ STARTUPAPPROVED
        // ============================================================

        private static bool GetStartupApprovedState(
            RegistryKey root,
            string approvedSubKey,
            string valueName)
        {
            try
            {
                using RegistryKey? key =
                    root.OpenSubKey(approvedSubKey);

                if (key == null)
                {
                    /*
                     * Если StartupApproved ещё не содержит
                     * значение — считаем запись включённой.
                     */
                    return true;
                }

                object? value =
                    key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);

                if (value is not byte[] data ||
                    data.Length == 0)
                {
                    return true;
                }

                return data[0] != DisabledState;
            }
            catch
            {
                /*
                 * Если не удалось прочитать состояние,
                 * не будем ошибочно считать элемент отключённым.
                 */
                return true;
            }
        }

        // ============================================================
        // DISABLE
        // ============================================================

        public static bool Disable(StartupItem item)
        {
            if (!item.CanToggle)
                return false;

            try
            {
                if (item.ItemType == "Registry")
                {
                    return SetRegistryStartupApproved(
                        item,
                        enabled: false);
                }

                if (item.ItemType == "StartupFolder")
                {
                    return SetFolderStartupApproved(
                        item,
                        enabled: false);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // ENABLE
        // ============================================================

        public static bool Enable(StartupItem item)
        {
            if (!item.CanToggle)
                return false;

            try
            {
                if (item.ItemType == "Registry")
                {
                    return SetRegistryStartupApproved(
                        item,
                        enabled: true);
                }

                if (item.ItemType == "StartupFolder")
                {
                    return SetFolderStartupApproved(
                        item,
                        enabled: true);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // REGISTRY STARTUPAPPROVED
        // ============================================================

        private static bool SetRegistryStartupApproved(
            StartupItem item,
            bool enabled)
        {
            RegistryKey root =
                GetRegistryRoot(
                    item.RegistryRoot);

            using RegistryKey key =
                root.CreateSubKey(
                    item.StartupApprovedSubKey,
                    writable: true);

            byte[] data =
                GetExistingApprovalData(
                    key,
                    item.RegistryValueName);

            data[0] =
                enabled
                    ? EnabledState
                    : DisabledState;

            key.SetValue(
                item.RegistryValueName,
                data,
                RegistryValueKind.Binary);

            return VerifyStartupApproved(
                root,
                item.StartupApprovedSubKey,
                item.RegistryValueName,
                enabled);
        }

        // ============================================================
        // STARTUP FOLDER STARTUPAPPROVED
        // ============================================================

        private static bool SetFolderStartupApproved(
            StartupItem item,
            bool enabled)
        {
            RegistryKey root =
                item.Source.Contains(
                        "текущий пользователь",
                        StringComparison.OrdinalIgnoreCase)
                    ? Registry.CurrentUser
                    : Registry.LocalMachine;

            using RegistryKey key =
                root.CreateSubKey(
                    StartupApprovedStartupFolder,
                    writable: true);

            byte[] data =
                GetExistingApprovalData(
                    key,
                    item.Name);

            data[0] =
                enabled
                    ? EnabledState
                    : DisabledState;

            key.SetValue(
                item.Name,
                data,
                RegistryValueKind.Binary);

            return VerifyStartupApproved(
                root,
                StartupApprovedStartupFolder,
                item.Name,
                enabled);
        }

        // ============================================================
        // ПОЛУЧЕНИЕ СУЩЕСТВУЮЩЕГО BINARY
        // ============================================================

        private static byte[] GetExistingApprovalData(
            RegistryKey key,
            string valueName)
        {
            try
            {
                object? value =
                    key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);

                if (value is byte[] existing &&
                    existing.Length >= 2)
                {
                    return existing;
                }
            }
            catch
            {
                // Создадим стандартный буфер ниже.
            }

            /*
             * Обычно StartupApproved использует
             * небольшой бинарный массив.
             * 12 байт достаточно для создания записи.
             */
            return new byte[12];
        }

        // ============================================================
        // ПРОВЕРКА РЕЗУЛЬТАТА
        // ============================================================

        private static bool VerifyStartupApproved(
            RegistryKey root,
            string approvedSubKey,
            string valueName,
            bool expectedEnabled)
        {
            try
            {
                using RegistryKey? key =
                    root.OpenSubKey(approvedSubKey);

                if (key == null)
                    return false;

                object? value =
                    key.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames);

                if (value is not byte[] data ||
                    data.Length == 0)
                {
                    return false;
                }

                bool actualEnabled =
                    data[0] != DisabledState;

                return actualEnabled ==
                       expectedEnabled;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // МИГРАЦИЯ СТАРОЙ ВЕРСИИ WIN TWEAKER
        // ============================================================

        private static void MigrateLegacyDisabledRegistry(
            RegistryKey root,
            RegistryHive hive,
            string originalSubKey,
            string source,
            List<StartupItem> items)
        {
            const string legacySuffix =
                "_WinTweakerDisabled";

            try
            {
                string oldSubKey =
                    originalSubKey + legacySuffix;

                using RegistryKey? oldKey =
                    root.OpenSubKey(
                        oldSubKey,
                        writable: true);

                if (oldKey == null)
                    return;

                string approvedKey =
                    StartupApprovedRun;

                using RegistryKey target =
                    root.CreateSubKey(
                        originalSubKey,
                        writable: true);

                foreach (string valueName in oldKey.GetValueNames())
                {
                    object? value =
                        oldKey.GetValue(
                            valueName,
                            null,
                            RegistryValueOptions.DoNotExpandEnvironmentNames);

                    if (value == null)
                        continue;

                    RegistryValueKind kind;

                    try
                    {
                        kind =
                            oldKey.GetValueKind(valueName);
                    }
                    catch
                    {
                        continue;
                    }

                    target.SetValue(
                        valueName,
                        value,
                        kind);

                    oldKey.DeleteValue(
                        valueName,
                        throwOnMissingValue: false);

                    using RegistryKey approved =
                        root.CreateSubKey(
                            approvedKey,
                            writable: true);

                    byte[] data = new byte[12];

                    data[0] = DisabledState;

                    approved.SetValue(
                        valueName,
                        data,
                        RegistryValueKind.Binary);
                }
            }
            catch
            {
                // Миграция не должна ломать запуск приложения.
            }
        }

        // ============================================================
        // ВСПОМОГАТЕЛЬНЫЕ
        // ============================================================

        private static string GetHiveName(
            RegistryHive hive)
        {
            return hive switch
            {
                RegistryHive.CurrentUser =>
                    "HKEY_CURRENT_USER",

                RegistryHive.LocalMachine =>
                    "HKEY_LOCAL_MACHINE",

                _ =>
                    hive.ToString()
            };
        }

        private static RegistryKey GetRegistryRoot(
            string registryRoot)
        {
            return registryRoot switch
            {
                "HKEY_CURRENT_USER" =>
                    Registry.CurrentUser,

                "HKEY_LOCAL_MACHINE" =>
                    Registry.LocalMachine,

                _ =>
                    throw new InvalidOperationException(
                        "Неизвестный корень реестра.")
            };
        }
    }
}