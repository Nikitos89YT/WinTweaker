using System;
using System.Diagnostics;
using System.IO;

namespace WinTweaker.Services
{
    public static class SystemTweaksService
    {
        // =========================================================
        // WINDOWS EXPLORER
        // =========================================================

        /// <summary>
        /// Перезапускает Windows Explorer.
        /// </summary>
        public static bool RestartExplorer()
        {
            try
            {
                foreach (Process process
                         in Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                    catch
                    {
                        // Уже завершился или недоступен.
                    }
                }

                string explorerPath =
                    Path.Combine(
                        Environment.SystemDirectory,
                        "explorer.exe");

                if (!File.Exists(explorerPath))
                    explorerPath = "explorer.exe";

                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = explorerPath,
                        UseShellExecute = true
                    });

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // SYSTEM COMPONENTS
        // =========================================================

        /// <summary>
        /// Запускает системный компонент Windows.
        /// </summary>
        public static bool LaunchSystemComponent(
            SystemComponent component)
        {
            string systemDirectory =
                Environment.SystemDirectory;

            string mmcPath =
                Path.Combine(
                    systemDirectory,
                    "mmc.exe");

            string taskManagerPath =
                Path.Combine(
                    systemDirectory,
                    "Taskmgr.exe");

            string resourceMonitorPath =
                Path.Combine(
                    systemDirectory,
                    "resmon.exe");

            string controlPath =
                Path.Combine(
                    systemDirectory,
                    "control.exe");

            try
            {
                switch (component)
                {
                    // =================================================
                    // EXE
                    // =================================================

                    case SystemComponent.TaskManager:

                        return StartProcess(
                            taskManagerPath);

                    case SystemComponent.ResourceMonitor:

                        return StartProcess(
                            resourceMonitorPath);


                    // =================================================
                    // MMC
                    // =================================================

                    case SystemComponent.DeviceManager:

                        return StartProcess(
                            mmcPath,
                            "devmgmt.msc");

                    case SystemComponent.DiskManagement:

                        return StartProcess(
                            mmcPath,
                            "diskmgmt.msc");

                    case SystemComponent.Services:

                        return StartProcess(
                            mmcPath,
                            "services.msc");

                    case SystemComponent.EventViewer:

                        return StartProcess(
                            mmcPath,
                            "eventvwr.msc");


                    // =================================================
                    // CONTROL PANEL
                    // =================================================

                    case SystemComponent.SystemProperties:

                        return StartProcess(
                            controlPath,
                            "sysdm.cpl");

                    case SystemComponent.ControlPanel:

                        return StartProcess(
                            controlPath);

                    case SystemComponent.NetworkConnections:

                        return StartProcess(
                            controlPath,
                            "ncpa.cpl");


                    default:

                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // PROCESS START
        // =========================================================

        private static bool StartProcess(
            string fileName,
            string arguments = "")
        {
            try
            {
                if (!File.Exists(fileName))
                    return false;

                var startInfo =
                    new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = true,
                        WorkingDirectory =
                            Path.GetDirectoryName(fileName)
                            ?? Environment.SystemDirectory
                    };

                Process? process =
                    Process.Start(startInfo);

                return process != null;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // CACHE HELPERS
        // =========================================================

        /// <summary>
        /// Очищает кэш значков.
        /// </summary>
        public static CleanupResult CleanIconCache()
        {
            return CleanupService.CleanIconCache();
        }

        /// <summary>
        /// Очищает кэш миниатюр.
        /// </summary>
        public static CleanupResult CleanThumbnailCache()
        {
            return CleanupService.CleanThumbnailCache();
        }
    }

    public enum SystemComponent
    {
        TaskManager,
        DeviceManager,
        DiskManagement,
        Services,
        EventViewer,
        ResourceMonitor,
        SystemProperties,
        ControlPanel,
        NetworkConnections
    }
}