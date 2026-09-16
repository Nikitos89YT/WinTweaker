using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WinTweaker.Services
{
    public class CleanupResult
    {
        public string Name { get; set; } = "";

        public int DeletedFiles { get; set; }

        public int DeletedDirectories { get; set; }

        public long FreedBytes { get; set; }

        public int SkippedItems { get; set; }

        public bool Success { get; set; }

        public string Message
        {
            get
            {
                if (!Success)
                    return "Не удалось выполнить очистку.";

                return $"Удалено файлов: {DeletedFiles}, " +
                       $"освобождено: {FormatBytes(FreedBytes)}";
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} Б";

            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} КБ";

            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F1} МБ";

            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} ГБ";
        }
    }

    public static class CleanupService
    {
        // =========================================================
        // WINDOWS API — RECYCLE BIN
        // =========================================================

        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        [DllImport(
            "Shell32.dll",
            CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(
            IntPtr hwnd,
            string? pszRootPath,
            uint dwFlags);

        [DllImport(
            "Shell32.dll",
            CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(
            string? pszRootPath,
            ref SHQUERYRBINFO pSHQueryRBInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public uint cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        // =========================================================
        // USER TEMP
        // =========================================================

        public static CleanupResult CleanUserTemp()
        {
            string path = Path.GetTempPath();

            return CleanDirectory(
                "Временные файлы пользователя",
                path);
        }

        // =========================================================
        // WINDOWS TEMP
        // =========================================================

        public static CleanupResult CleanWindowsTemp()
        {
            string windowsDirectory =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows);

            string path =
                Path.Combine(
                    windowsDirectory,
                    "Temp");

            return CleanDirectory(
                "Windows Temp",
                path);
        }

        // =========================================================
        // DIRECTX SHADER CACHE
        // =========================================================

        public static CleanupResult CleanDirectXShaderCache()
        {
            string localAppData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            string path =
                Path.Combine(
                    localAppData,
                    "D3DSCache");

            return CleanDirectory(
                "DirectX Shader Cache",
                path);
        }

        // =========================================================
        // THUMBNAIL CACHE
        // =========================================================

        public static CleanupResult CleanThumbnailCache()
        {
            string localAppData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            string explorerPath =
                Path.Combine(
                    localAppData,
                    "Microsoft",
                    "Windows",
                    "Explorer");

            if (!Directory.Exists(explorerPath))
            {
                return new CleanupResult
                {
                    Name = "Кэш миниатюр",
                    Success = true
                };
            }

            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(
                    explorerPath,
                    "thumbcache_*.db",
                    System.IO.SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return new CleanupResult
                {
                    Name = "Кэш миниатюр",
                    Success = false
                };
            }

            return DeleteFiles(
                "Кэш миниатюр",
                files);
        }

        // =========================================================
        // ICON CACHE
        // =========================================================

        public static CleanupResult CleanIconCache()
        {
            string localAppData =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            string explorerPath =
                Path.Combine(
                    localAppData,
                    "Microsoft",
                    "Windows",
                    "Explorer");

            if (!Directory.Exists(explorerPath))
            {
                return new CleanupResult
                {
                    Name = "Кэш значков",
                    Success = true
                };
            }

            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(
                    explorerPath,
                    "iconcache*.db",
                    System.IO.SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return new CleanupResult
                {
                    Name = "Кэш значков",
                    Success = false
                };
            }

            return DeleteFiles(
                "Кэш значков",
                files);
        }

        // =========================================================
        // DELIVERY OPTIMIZATION
        // =========================================================

        public static CleanupResult CleanDeliveryOptimization()
        {
            try
            {
                int exitCode =
                    RunPowerShellCommand(
                        "Delete-DeliveryOptimizationCache -Force");

                return new CleanupResult
                {
                    Name = "Delivery Optimization",
                    Success = exitCode == 0
                };
            }
            catch
            {
                return new CleanupResult
                {
                    Name = "Delivery Optimization",
                    Success = false
                };
            }
        }

        // =========================================================
        // RECYCLE BIN
        // =========================================================

        public static CleanupResult EmptyRecycleBin()
        {
            try
            {
                long itemCountBefore = 0;
                long totalSizeBefore = 0;

                // Смотрим содержимое корзины ДО очистки.
                SHQUERYRBINFO before =
                    new SHQUERYRBINFO
                    {
                        cbSize =
                            (uint)Marshal.SizeOf<SHQUERYRBINFO>()
                    };

                int queryBeforeResult =
                    SHQueryRecycleBin(
                        null,
                        ref before);

                if (queryBeforeResult == 0)
                {
                    itemCountBefore =
                        before.i64NumItems;

                    totalSizeBefore =
                        before.i64Size;
                }

                // Очищаем корзину.
                int clearResult =
                    SHEmptyRecycleBin(
                        IntPtr.Zero,
                        null,
                        SHERB_NOCONFIRMATION |
                        SHERB_NOPROGRESSUI |
                        SHERB_NOSOUND);

                // Проверяем корзину ПОСЛЕ очистки.
                SHQUERYRBINFO after =
                    new SHQUERYRBINFO
                    {
                        cbSize =
                            (uint)Marshal.SizeOf<SHQUERYRBINFO>()
                    };

                int queryAfterResult =
                    SHQueryRecycleBin(
                        null,
                        ref after);

                bool isEmptyAfter =
                    queryAfterResult == 0 &&
                    after.i64NumItems == 0;

                // Если после операции корзина пуста —
                // считаем очистку успешной независимо
                // от вспомогательного кода Shell.
                bool success =
                    clearResult == 0 ||
                    isEmptyAfter;

                if (!success)
                {
                    return new CleanupResult
                    {
                        Name = "Корзина",
                        Success = false
                    };
                }

                return new CleanupResult
                {
                    Name = "Корзина",
                    Success = true,
                    DeletedFiles =
                        itemCountBefore > int.MaxValue
                            ? int.MaxValue
                            : (int)itemCountBefore,
                    FreedBytes =
                        totalSizeBefore
                };
            }
            catch
            {
                return new CleanupResult
                {
                    Name = "Корзина",
                    Success = false
                };
            }
        }

        // =========================================================
        // SAFE CLEANUP
        // =========================================================

        public static List<CleanupResult> SafeCleanup()
        {
            var results =
                new List<CleanupResult>();

            results.Add(
                CleanUserTemp());

            results.Add(
                CleanWindowsTemp());

            results.Add(
                CleanDirectXShaderCache());

            results.Add(
                CleanThumbnailCache());

            results.Add(
                CleanIconCache());

            results.Add(
                CleanDeliveryOptimization());

            results.Add(
                EmptyRecycleBin());

            return results;
        }

        // =========================================================
        // DIRECTORY CLEANUP
        // =========================================================

        private static CleanupResult CleanDirectory(
            string name,
            string path)
        {
            var result =
                new CleanupResult
                {
                    Name = name,
                    Success = true
                };

            if (!Directory.Exists(path))
                return result;

            try
            {
                foreach (string file in Directory.EnumerateFiles(
                             path,
                             "*",
                             System.IO.SearchOption.AllDirectories))
                {
                    TryDeleteFile(
                        file,
                        result);
                }

                var directories =
                    Directory.EnumerateDirectories(
                        path,
                        "*",
                        System.IO.SearchOption.AllDirectories)
                    .OrderByDescending(
                        x => x.Length)
                    .ToList();

                foreach (string directory in directories)
                {
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(
                                directory).Any())
                        {
                            Directory.Delete(
                                directory,
                                false);

                            result.DeletedDirectories++;
                        }
                    }
                    catch
                    {
                        result.SkippedItems++;
                    }
                }
            }
            catch
            {
                result.SkippedItems++;
            }

            return result;
        }

        // =========================================================
        // DELETE FILES
        // =========================================================

        private static CleanupResult DeleteFiles(
            string name,
            IEnumerable<string> files)
        {
            var result =
                new CleanupResult
                {
                    Name = name,
                    Success = true
                };

            foreach (string file in files)
            {
                TryDeleteFile(
                    file,
                    result);
            }

            return result;
        }

        // =========================================================
        // SAFE FILE DELETE
        // =========================================================

        private static void TryDeleteFile(
            string file,
            CleanupResult result)
        {
            try
            {
                if (!File.Exists(file))
                    return;

                long size = 0;

                try
                {
                    size =
                        new FileInfo(file).Length;
                }
                catch
                {
                }

                File.Delete(file);

                result.DeletedFiles++;
                result.FreedBytes += size;
            }
            catch
            {
                // Занятый/защищённый файл пропускаем.
                result.SkippedItems++;
            }
        }

        // =========================================================
        // POWERSHELL
        // =========================================================

        private static int RunPowerShellCommand(
            string command)
        {
            try
            {
                using var process =
                    new Process();

                process.StartInfo =
                    new ProcessStartInfo
                    {
                        FileName =
                            "powershell.exe",

                        Arguments =
                            $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",

                        UseShellExecute =
                            true,

                        CreateNoWindow =
                            true,

                        WindowStyle =
                            ProcessWindowStyle.Hidden,

                        Verb =
                            "runas"
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
    }
}