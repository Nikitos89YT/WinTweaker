using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WinTweaker.Services
{
    public static class WindowsToolsService
    {
        // =========================================================
        // RUN ELEVATED COMMAND
        // =========================================================

        public static async Task<string> RunElevatedCommandAsync(
            string command)
        {
            string tempDirectory =
                Path.Combine(
                    Path.GetTempPath(),
                    "WinTweaker");

            Directory.CreateDirectory(tempDirectory);

            string outputFile =
                Path.Combine(
                    tempDirectory,
                    $"tool_{Guid.NewGuid():N}.log");

            try
            {
                string escapedOutputPath =
                    outputFile.Replace("'", "''");

                string escapedCommand =
                    command.Replace("'", "''");

                string script = $"""
                    $OutputEncoding = [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

                    & cmd.exe /c '{escapedCommand}' 2>&1 |
                        Out-File -FilePath '{escapedOutputPath}' -Encoding utf8

                    exit $LASTEXITCODE
                    """;

                string encodedCommand =
                    Convert.ToBase64String(
                        Encoding.Unicode.GetBytes(script));

                ProcessStartInfo startInfo =
                    new ProcessStartInfo
                    {
                        FileName =
                            "powershell.exe",

                        Arguments =
                            $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",

                        UseShellExecute =
                            true,

                        Verb =
                            "runas",

                        CreateNoWindow =
                            true,

                        WindowStyle =
                            ProcessWindowStyle.Hidden
                    };

                using Process? process =
                    Process.Start(startInfo);

                if (process == null)
                {
                    return "Не удалось запустить административную операцию.";
                }

                await process.WaitForExitAsync();

                if (File.Exists(outputFile))
                {
                    string output =
                        await File.ReadAllTextAsync(
                            outputFile,
                            Encoding.UTF8);

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return
                            $"Команда завершена с кодом {process.ExitCode}.";
                    }

                    return
                        $"Код завершения: {process.ExitCode}\n\n{output}";
                }

                return
                    $"Команда завершена с кодом {process.ExitCode}, но вывод не найден.";
            }
            catch (System.ComponentModel.Win32Exception ex)
                when (ex.NativeErrorCode == 1223)
            {
                return "Операция отменена пользователем.";
            }
            catch (Exception ex)
            {
                return
                    $"Ошибка выполнения:\n{ex.Message}";
            }
            finally
            {
                try
                {
                    if (File.Exists(outputFile))
                        File.Delete(outputFile);
                }
                catch
                {
                    // Игнорируем ошибку удаления временного файла.
                }
            }
        }


        // =========================================================
        // DISK CHECK
        // =========================================================

        public static Task<string> CheckDiskAsync()
        {
            string systemDrive =
                Environment.GetEnvironmentVariable("SystemDrive")
                ?? "C:";

            return RunElevatedCommandAsync(
                $"chkdsk {systemDrive} /scan");
        }


        public static Task<string> RepairDiskAsync()
        {
            string systemDrive =
                Environment.GetEnvironmentVariable("SystemDrive")
                ?? "C:";

            return RunElevatedCommandAsync(
                $"chkdsk {systemDrive} /f");
        }


        // =========================================================
        // SFC
        // =========================================================

        public static Task<string> RunSfcAsync()
        {
            return RunElevatedCommandAsync(
                "sfc /scannow");
        }


        // =========================================================
        // DISM
        // =========================================================

        public static Task<string> RunDismCheckHealthAsync()
        {
            return RunElevatedCommandAsync(
                "DISM /Online /Cleanup-Image /CheckHealth");
        }


        public static Task<string> RunDismScanHealthAsync()
        {
            return RunElevatedCommandAsync(
                "DISM /Online /Cleanup-Image /ScanHealth");
        }


        public static Task<string> RunDismRestoreHealthAsync()
        {
            return RunElevatedCommandAsync(
                "DISM /Online /Cleanup-Image /RestoreHealth");
        }


        // =========================================================
        // FULL WINDOWS REPAIR
        // =========================================================

        public static async Task<string> RunFullRepairAsync()
        {
            StringBuilder result =
                new StringBuilder();

            result.AppendLine(
                "========================================");

            result.AppendLine(
                "КОМПЛЕКСНАЯ ПРОВЕРКА WINDOWS");

            result.AppendLine(
                "========================================");

            result.AppendLine();


            // -----------------------------------------------------
            // DISM
            // -----------------------------------------------------

            result.AppendLine(
                "[1/2] DISM /RestoreHealth");

            result.AppendLine(
                "Запуск...");

            result.AppendLine();


            string dism =
                await RunDismRestoreHealthAsync();

            result.AppendLine(dism);

            result.AppendLine();


            result.AppendLine(
                "========================================");

            result.AppendLine();


            // -----------------------------------------------------
            // SFC
            // -----------------------------------------------------

            result.AppendLine(
                "[2/2] SFC /scannow");

            result.AppendLine(
                "Запуск...");

            result.AppendLine();


            string sfc =
                await RunSfcAsync();

            result.AppendLine(sfc);

            result.AppendLine();


            result.AppendLine(
                "========================================");

            result.AppendLine(
                "КОМПЛЕКСНАЯ ПРОВЕРКА ЗАВЕРШЕНА");

            result.AppendLine(
                "========================================");


            return result.ToString();
        }


        // =========================================================
        // WINDOWS SERVICES
        // =========================================================

        public static void OpenWindowsServices()
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        "services.msc",

                    UseShellExecute =
                        true
                });
        }
    }
}