using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using WinTweaker.Services;

namespace WinTweaker.Pages
{
    public partial class ToolsPage : Page
    {
        private bool _operationRunning;

        public ToolsPage()
        {
            InitializeComponent();
        }


        // =========================================================
        // OUTPUT
        // =========================================================

        private void SetOutput(string text)
        {
            OutputTextBox.Text = text;
            OutputTextBox.ScrollToEnd();
        }


        // =========================================================
        // OPERATION STATE
        // =========================================================

        private void SetOperationState(bool running)
        {
            _operationRunning = running;

            CheckDiskButton.IsEnabled = !running;
            RepairDiskButton.IsEnabled = !running;

            FullRepairButton.IsEnabled = !running;
        }


        // =========================================================
        // DISK CHECK
        // =========================================================

        private async void CheckDiskButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_operationRunning)
                return;


            SetOutput(
                "Запуск проверки диска...\n\n" +
                "Может появиться запрос UAC.");

            SetOperationState(true);

            try
            {
                string result =
                    await WindowsToolsService.CheckDiskAsync();

                SetOutput(
                    "ПРОВЕРКА ДИСКА\n" +
                    "==============================\n\n" +
                    result);
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"Ошибка:\n{ex.Message}");
            }
            finally
            {
                SetOperationState(false);
            }
        }


        // =========================================================
        // DISK REPAIR
        // =========================================================

        private async void RepairDiskButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_operationRunning)
                return;


            MessageBoxResult result =
                AppDialogs.Show(
                    "Запустить проверку и исправление системного диска?\n\n" +
                    "Windows может запросить выполнение операции " +
                    "при следующей перезагрузке.",
                    "Проверка и исправление диска",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;


            SetOutput(
                "Запуск проверки и исправления диска...\n\n" +
                "Может появиться запрос UAC.");

            SetOperationState(true);

            try
            {
                string output =
                    await WindowsToolsService.RepairDiskAsync();

                SetOutput(
                    "ПРОВЕРКА И ИСПРАВЛЕНИЕ ДИСКА\n" +
                    "==============================\n\n" +
                    output);
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"Ошибка:\n{ex.Message}");
            }
            finally
            {
                SetOperationState(false);
            }
        }


        // =========================================================
        // SFC
        // =========================================================

        private async void SfcButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunSingleOperation(
                "SFC /scannow",
                WindowsToolsService.RunSfcAsync);
        }


        // =========================================================
        // DISM CHECK HEALTH
        // =========================================================

        private async void DismCheckHealthButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunSingleOperation(
                "DISM /CheckHealth",
                WindowsToolsService.RunDismCheckHealthAsync);
        }


        // =========================================================
        // DISM SCAN HEALTH
        // =========================================================

        private async void DismScanHealthButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunSingleOperation(
                "DISM /ScanHealth",
                WindowsToolsService.RunDismScanHealthAsync);
        }


        // =========================================================
        // DISM RESTORE HEALTH
        // =========================================================

        private async void DismRestoreHealthButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await RunSingleOperation(
                "DISM /RestoreHealth",
                WindowsToolsService.RunDismRestoreHealthAsync);
        }


        // =========================================================
        // SINGLE OPERATION
        // =========================================================

        private async Task RunSingleOperation(
            string operationName,
            Func<Task<string>> operation)
        {
            if (_operationRunning)
                return;


            SetOutput(
                $"ЗАПУСК: {operationName}\n" +
                "==============================\n\n" +
                "Может появиться запрос UAC.\n\n" +
                "Ожидание результата...");

            SetOperationState(true);

            try
            {
                string result =
                    await operation();

                SetOutput(
                    $"{operationName}\n" +
                    "==============================\n\n" +
                    result);
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"{operationName}\n\n" +
                    $"Ошибка:\n{ex.Message}");
            }
            finally
            {
                SetOperationState(false);
            }
        }


        // =========================================================
        // FULL REPAIR
        // =========================================================

        private async void FullRepairButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_operationRunning)
                return;


            MessageBoxResult confirm =
                AppDialogs.Show(
                    "Будет последовательно выполнено:\n\n" +
                    "1. DISM /RestoreHealth\n" +
                    "2. SFC /scannow\n\n" +
                    "Операция может занять значительное время.\n" +
                    "Продолжить?",
                    "Комплексная проверка Windows",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;


            SetOutput(
                "КОМПЛЕКСНАЯ ПРОВЕРКА WINDOWS\n" +
                "==============================\n\n" +
                "Запускается DISM → SFC.\n" +
                "Не закрывайте WinTweaker до завершения.");

            SetOperationState(true);

            try
            {
                string result =
                    await WindowsToolsService.RunFullRepairAsync();

                SetOutput(result);
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"Ошибка:\n{ex.Message}");
            }
            finally
            {
                SetOperationState(false);
            }
        }


        // =========================================================
        // SERVICES
        // =========================================================

        private void OpenServicesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                WindowsToolsService.OpenWindowsServices();
            }
            catch (Exception ex)
            {
                AppDialogs.Show(
                    "Не удалось открыть службы Windows.\n\n" +
                    ex.Message,
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // CLEANUP
        // =========================================================

        private void ShowCleanupResult(
            CleanupResult result)
        {
            string skippedText =
                result.SkippedItems > 0
                    ? $"\nПропущено: {result.SkippedItems}"
                    : "";

            AppDialogs.Show(
                $"{result.Name}\n\n" +
                $"Удалено файлов: {result.DeletedFiles}\n" +
                $"Удалено папок: {result.DeletedDirectories}\n" +
                $"Освобождено: {FormatBytes(result.FreedBytes)}" +
                skippedText,
                result.Success
                    ? "Очистка завершена"
                    : "Ошибка очистки",
                MessageBoxButton.OK,
                result.Success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }

        private static string FormatBytes(
            long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} Б";

            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} КБ";

            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F1} МБ";

            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} ГБ";
        }

        private void CleanUserTempButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var result =
                CleanupService.CleanUserTemp();

            ShowCleanupResult(result);
        }

        private void CleanWindowsTempButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var result =
                CleanupService.CleanWindowsTemp();

            ShowCleanupResult(result);
        }

        private void CleanDirectXShaderCacheButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var result =
                CleanupService.CleanDirectXShaderCache();

            ShowCleanupResult(result);
        }

        private void CleanThumbnailCacheButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var result =
                CleanupService.CleanThumbnailCache();

            ShowCleanupResult(result);
        }

        private void CleanIconCacheButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var result =
                CleanupService.CleanIconCache();

            ShowCleanupResult(result);
        }

        private void CleanDeliveryOptimizationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var result =
                CleanupService.CleanDeliveryOptimization();

            ShowCleanupResult(result);
        }

        private void EmptyRecycleBinButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var confirmation =
                AppDialogs.Show(
                    "Очистить корзину Windows?\n\n" +
                    "Файлы из корзины будут удалены окончательно.",
                    "WinTweaker",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            var result =
                CleanupService.EmptyRecycleBin();

            ShowCleanupResult(result);
        }

        private void PreviewCleanupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var previews = CleanupService.PreviewSafeCleanup();
            var builder = new StringBuilder();
            long estimatedBytes = 0;
            int estimatedFiles = 0;
            int unestimatedItems = 0;

            builder.AppendLine("ПРЕДПРОСМОТР БЕЗОПАСНОЙ ОЧИСТКИ");
            builder.AppendLine("==============================");
            builder.AppendLine("Файлы не удалялись и не изменялись.");
            builder.AppendLine();

            foreach (CleanupPreviewResult preview in previews)
            {
                builder.AppendLine(preview.Name);

                if (preview.EstimateAvailable)
                {
                    builder.AppendLine($"  Файлов: {preview.EstimatedFiles}");
                    builder.AppendLine($"  Примерный объём: {FormatBytes(preview.EstimatedBytes)}");
                    estimatedFiles += preview.EstimatedFiles;
                    estimatedBytes += preview.EstimatedBytes;
                }
                else
                {
                    builder.AppendLine($"  Оценка недоступна: {preview.Message}");
                }

                if (preview.UnestimatedItems > 0)
                {
                    builder.AppendLine($"  Не удалось оценить: {preview.UnestimatedItems}");
                    unestimatedItems += preview.UnestimatedItems;
                }

                builder.AppendLine();
            }

            builder.AppendLine($"Итого оценено: {estimatedFiles} файлов, {FormatBytes(estimatedBytes)}");
            if (unestimatedItems > 0)
                builder.AppendLine($"Не удалось оценить элементов: {unestimatedItems}");

            CleanupPreviewTextBox.Text = builder.ToString();
            CleanupPreviewTextBox.ScrollToEnd();
        }

        private void SafeCleanupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var confirmation =
                AppDialogs.Show(
                    "Запустить безопасную очистку?\n\n" +
                    "Будут очищены временные файлы и системные кэши. " +
                    "Пользовательские документы, изображения, видео, " +
                    "музыка, рабочий стол и загрузки не затрагиваются.\n\n" +
                    "Корзина также будет очищена.",
                    "Безопасная очистка",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            var results =
                CleanupService.SafeCleanup();

            int totalFiles = 0;
            int totalDirectories = 0;
            int totalSkipped = 0;
            long totalFreedBytes = 0;

            var failed =
                new List<string>();

            foreach (CleanupResult result in results)
            {
                totalFiles +=
                    result.DeletedFiles;

                totalDirectories +=
                    result.DeletedDirectories;

                totalSkipped +=
                    result.SkippedItems;

                totalFreedBytes +=
                    result.FreedBytes;

                if (!result.Success)
                {
                    failed.Add(result.Name);
                }
            }

            var builder =
                new StringBuilder();

            builder.AppendLine(
                "Безопасная очистка завершена.");

            builder.AppendLine();

            builder.AppendLine(
                $"Удалено файлов: {totalFiles}");

            builder.AppendLine(
                $"Удалено папок: {totalDirectories}");

            builder.AppendLine(
                $"Освобождено: {FormatBytes(totalFreedBytes)}");

            if (totalSkipped > 0)
            {
                builder.AppendLine(
                    $"Пропущено: {totalSkipped}");
            }

            if (failed.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(
                    "Не удалось полностью очистить:");

                foreach (string name in failed)
                {
                    builder.AppendLine(
                        $"• {name}");
                }
            }

            AppDialogs.Show(
                builder.ToString(),
                "WinTweaker",
                MessageBoxButton.OK,
                failed.Count == 0
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }

        // =========================================================
        // SYSTEM
        // =========================================================

        private void RestartExplorerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            bool confirmation =
                AppDialogs.Show(
                    "Перезапустить Windows Explorer?\n\n" +
                    "Рабочий стол и окна Проводника на несколько секунд исчезнут, " +
                    "после чего оболочка Windows запустится снова.",
                    "Перезапуск Explorer",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question)
                == MessageBoxResult.Yes;

            if (!confirmation)
                return;

            bool success =
                SystemTweaksService.RestartExplorer();

            AppDialogs.Show(
                success
                    ? "Windows Explorer успешно перезапущен."
                    : "Не удалось перезапустить Windows Explorer.",
                "WinTweaker",
                MessageBoxButton.OK,
                success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Error);
        }

        private void OpenWindowsApps_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new WindowsAppsPage());
        }

        private void OpenDriverManagerButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new DriverManagerPage());
        }

        private void LaunchSystemComponentButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.Tag is not string componentName)
                return;

            if (!Enum.TryParse(
                    componentName,
                    out SystemComponent component))
            {
                return;
            }

            bool success =
                SystemTweaksService.LaunchSystemComponent(
                    component);

            if (!success)
            {
                AppDialogs.Show(
                    "Не удалось запустить системный компонент.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
