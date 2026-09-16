using System;
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
                MessageBox.Show(
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
                MessageBox.Show(
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
                MessageBox.Show(
                    "Не удалось открыть службы Windows.\n\n" +
                    ex.Message,
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}