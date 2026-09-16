using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Principal;

namespace WinTweaker.Services
{
    public static class TaskManagerUnlocker
    {
        private const string RegistryPath =
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\System";

        private const string DisableTaskManagerValue =
            "DisableTaskMgr";

        public static bool? IsLocked()
        {
            try
            {
                using RegistryKey? key =
                    Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Policies\System");

                if (key == null)
                    return false;

                object? value = key.GetValue(DisableTaskManagerValue);

                if (value == null)
                    return false;

                return value switch
                {
                    int intValue => intValue == 1,
                    long longValue => longValue == 1,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        public static bool Unlock()
        {
            return RunElevatedCommand(
                "reg.exe",
                $"delete \"{RegistryPath}\" /v {DisableTaskManagerValue} /f");
        }

        public static bool Lock()
        {
            return RunElevatedCommand(
                "reg.exe",
                $"add \"{RegistryPath}\" /v {DisableTaskManagerValue} /t REG_DWORD /d 1 /f");
        }

        private static bool RunElevatedCommand(
            string fileName,
            string arguments)
        {
            try
            {
                using Process process = new Process();

                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                if (!process.Start())
                    return false;

                process.WaitForExit();

                return process.ExitCode == 0;
            }
            catch
            {
                // Пользователь мог нажать "Нет" в окне UAC.
                return false;
            }
        }
    }
}