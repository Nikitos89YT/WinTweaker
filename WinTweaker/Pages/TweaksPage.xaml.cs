using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinTweaker.Services;

namespace WinTweaker.Pages
{
    public partial class TweaksPage : Page
    {
        private readonly Brush GreenBrush =
            new SolidColorBrush(Color.FromRgb(80, 200, 120));

        private readonly Brush RedBrush =
            new SolidColorBrush(Color.FromRgb(255, 90, 90));

        private readonly Brush GrayBrush =
            new SolidColorBrush(Color.FromRgb(150, 150, 150));

        public TweaksPage()
        {
            InitializeComponent();

            Loaded += TweaksPage_Loaded;
        }

        private void TweaksPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            UpdateGameMode();
            UpdateDisplayInfo();
            LoadDisplayModes();

            LoadNetworkAdapters();
            LoadDnsPresets();
            UpdateDnsInfo();
        }

        // =========================================================
        // GAME MODE
        // =========================================================

        private void UpdateGameMode()
        {
            bool? status = GameModeService.GetStatus();

            if (status == true)
            {
                GameModeStatusText.Text = "🟢 Включено";
                GameModeStatusText.Foreground = GreenBrush;
                GameModeButton.Content = "Выключить";
            }
            else if (status == false)
            {
                GameModeStatusText.Text = "🔴 Выключено";
                GameModeStatusText.Foreground = RedBrush;
                GameModeButton.Content = "Включить";
            }
            else
            {
                GameModeStatusText.Text = "⚪ Неизвестно";
                GameModeStatusText.Foreground = GrayBrush;
                GameModeButton.Content = "Переключить";
            }
        }

        private void GameModeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            bool? status = GameModeService.GetStatus();

            if (status == true)
            {
                GameModeService.SetStatus(false);
            }
            else
            {
                GameModeService.SetStatus(true);
            }

            UpdateGameMode();
        }

        // =========================================================
        // DISPLAY
        // =========================================================

        private void UpdateDisplayInfo()
        {
            var currentMode = DisplayService.GetCurrentMode();

            if (currentMode == null)
            {
                CurrentResolutionText.Text =
                    "Не удалось определить монитор";

                return;
            }

            CurrentResolutionText.Text =
                $"Текущий: {currentMode.Width} × {currentMode.Height} @ {currentMode.RefreshRate} Гц";
        }

        private void LoadDisplayModes()
        {
            StretchResolutionComboBox.Items.Clear();

            var modes =
                DisplayService.GetAvailableModes();

            var usefulModes =
                modes
                    .Where(x =>
                        x.Width >= 800 &&
                        x.Height >= 600)
                    .OrderByDescending(x =>
                        x.Width == 1440 &&
                        x.Height == 1080)
                    .ThenByDescending(x => x.Width)
                    .ThenByDescending(x => x.Height)
                    .ThenByDescending(x => x.RefreshRate)
                    .ToList();

            foreach (var mode in usefulModes)
            {
                StretchResolutionComboBox.Items.Add(mode);
            }

            var preferredMode =
                usefulModes
                    .Where(x =>
                        x.Width == 1440 &&
                        x.Height == 1080)
                    .OrderByDescending(x => x.RefreshRate)
                    .FirstOrDefault();

            if (preferredMode != null)
            {
                StretchResolutionComboBox.SelectedItem =
                    preferredMode;
            }
            else if (StretchResolutionComboBox.Items.Count > 0)
            {
                StretchResolutionComboBox.SelectedIndex = 0;
            }
        }

        private void StretchButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (StretchResolutionComboBox.SelectedItem
                is not DisplayModeInfo selectedMode)
            {
                MessageBox.Show(
                    "Сначала выбери разрешение.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            bool success =
                DisplayService.SetResolution(
                    selectedMode.Width,
                    selectedMode.Height,
                    selectedMode.RefreshRate);

            if (!success)
            {
                MessageBox.Show(
                    "Не удалось применить выбранное разрешение.\n\n" +
                    "Возможно, монитор не поддерживает этот режим.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            UpdateDisplayInfo();

            MessageBox.Show(
                $"Разрешение изменено на " +
                $"{selectedMode.Width} × {selectedMode.Height} @ {selectedMode.RefreshRate} Гц.",
                "WinTweaker",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RestoreResolutionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            bool success =
                DisplayService.RestoreOriginalMode();

            if (!success)
            {
                MessageBox.Show(
                    "Исходный режим ещё не был сохранён.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            UpdateDisplayInfo();

            MessageBox.Show(
                "Исходное разрешение восстановлено.",
                "WinTweaker",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // =========================================================
        // NETWORK ADAPTER
        // =========================================================

        private void LoadNetworkAdapters(
            string? selectedAdapterName = null)
        {
            NetworkAdapterComboBox.Items.Clear();

            var adapters =
                NetworkService.GetAdapters();

            foreach (var adapter in adapters)
            {
                NetworkAdapterComboBox.Items.Add(adapter);
            }

            if (!string.IsNullOrWhiteSpace(
                    selectedAdapterName))
            {
                var adapterToSelect =
                    adapters.FirstOrDefault(x =>
                        x.Name.Equals(
                            selectedAdapterName,
                            StringComparison.OrdinalIgnoreCase));

                if (adapterToSelect != null)
                {
                    NetworkAdapterComboBox.SelectedItem =
                        adapterToSelect;
                }
            }

            if (NetworkAdapterComboBox.SelectedItem == null &&
                NetworkAdapterComboBox.Items.Count > 0)
            {
                NetworkAdapterComboBox.SelectedIndex = 0;
            }

            UpdateNetworkAdapterInfo();
        }

        private void UpdateNetworkAdapterInfo()
        {
            if (NetworkAdapterComboBox.SelectedItem
                is not NetworkAdapterInfo adapter)
            {
                NetworkAdapterStatusText.Text =
                    "Адаптер не выбран";

                NetworkAdapterStatusText.Foreground =
                    GrayBrush;

                NetworkAdapterToggleButton.Content =
                    "Включить / выключить";

                return;
            }

            if (adapter.IsEnabled)
            {
                NetworkAdapterStatusText.Text =
                    $"🟢 {adapter.Name} — включён";

                NetworkAdapterStatusText.Foreground =
                    GreenBrush;

                NetworkAdapterToggleButton.Content =
                    "Выключить адаптер";
            }
            else
            {
                NetworkAdapterStatusText.Text =
                    $"🔴 {adapter.Name} — отключён";

                NetworkAdapterStatusText.Foreground =
                    RedBrush;

                NetworkAdapterToggleButton.Content =
                    "Включить адаптер";
            }
        }

        private void NetworkAdapterComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            UpdateNetworkAdapterInfo();
        }

        private void NetworkAdapterToggleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (NetworkAdapterComboBox.SelectedItem
                is not NetworkAdapterInfo adapter)
            {
                MessageBox.Show(
                    "Сначала выбери сетевой адаптер.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string selectedAdapterName =
                adapter.Name;

            bool success;

            if (adapter.IsEnabled)
            {
                success =
                    NetworkService.DisableAdapter(
                        selectedAdapterName);
            }
            else
            {
                success =
                    NetworkService.EnableAdapter(
                        selectedAdapterName);
            }

            if (!success)
            {
                MessageBox.Show(
                    adapter.IsEnabled
                        ? $"Не удалось отключить адаптер «{selectedAdapterName}»."
                        : $"Не удалось включить адаптер «{selectedAdapterName}».",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            LoadNetworkAdapters(
                selectedAdapterName);

            var updatedAdapter =
                NetworkAdapterComboBox.SelectedItem
                    as NetworkAdapterInfo;

            if (updatedAdapter != null)
            {
                MessageBox.Show(
                    updatedAdapter.IsEnabled
                        ? $"Адаптер «{selectedAdapterName}» включён."
                        : $"Адаптер «{selectedAdapterName}» отключён.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            UpdateDnsInfo();
        }

        // =========================================================
        // DNS
        // =========================================================

        private void LoadDnsPresets()
        {
            DnsPresetComboBox.Items.Clear();

            var presets =
                NetworkService.GetDnsPresets();

            foreach (var preset in presets)
            {
                DnsPresetComboBox.Items.Add(preset);
            }

            if (DnsPresetComboBox.Items.Count > 0)
            {
                DnsPresetComboBox.SelectedIndex = 0;
            }
        }

        private void ApplyDnsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (NetworkAdapterComboBox.SelectedItem
                is not NetworkAdapterInfo adapter)
            {
                MessageBox.Show(
                    "Сначала выбери сетевой адаптер.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (DnsPresetComboBox.SelectedItem
                is not DnsPreset preset)
            {
                MessageBox.Show(
                    "Сначала выбери DNS-пресет.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            bool success =
                NetworkService.SetDnsPreset(
                    adapter.Name,
                    preset);

            if (!success)
            {
                MessageBox.Show(
                    $"Не удалось применить DNS-пресет «{preset.Name}».\n\n" +
                    "Проверь, что сетевой адаптер включён.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            UpdateDnsInfo();

            MessageBox.Show(
                $"DNS-пресет «{preset.Name}» применён к адаптеру «{adapter.Name}».",
                "WinTweaker",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void FlushDnsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            bool result =
                NetworkService.FlushDns();

            MessageBox.Show(
                result
                    ? "DNS-кэш успешно очищен."
                    : "Не удалось очистить DNS-кэш.",
                "WinTweaker",
                MessageBoxButton.OK,
                result
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Error);

            UpdateDnsInfo();
        }

        private void ResetWinsockButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            bool result =
                NetworkService.ResetWinsock();

            MessageBox.Show(
                result
                    ? "Winsock успешно сброшен.\n\nДля применения изменений может потребоваться перезагрузка Windows."
                    : "Не удалось сбросить Winsock.",
                "WinTweaker",
                MessageBoxButton.OK,
                result
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Error);
        }

        private void ResetTcpIpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            bool result =
                NetworkService.ResetTcpIp();

            MessageBox.Show(
                result
                    ? "TCP/IP успешно сброшен.\n\nДля применения изменений может потребоваться перезагрузка Windows."
                    : "Не удалось сбросить TCP/IP.",
                "WinTweaker",
                MessageBoxButton.OK,
                result
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Error);
        }

        private void UpdateDnsInfo()
        {
            CurrentDnsText.Text =
                NetworkService.GetCurrentDns();
        }

        private void RefreshNetworkButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string? selectedAdapterName =
                (NetworkAdapterComboBox.SelectedItem
                    as NetworkAdapterInfo)?.Name;

            LoadNetworkAdapters(
                selectedAdapterName);

            UpdateDnsInfo();
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

            MessageBox.Show(
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
                MessageBox.Show(
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

        private void SafeCleanupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var confirmation =
                MessageBox.Show(
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

            MessageBox.Show(
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
                MessageBox.Show(
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

            MessageBox.Show(
                success
                    ? "Windows Explorer успешно перезапущен."
                    : "Не удалось перезапустить Windows Explorer.",
                "WinTweaker",
                MessageBoxButton.OK,
                success
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Error);
        }

        private void CleanSystemIconCacheButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var result =
                SystemTweaksService.CleanIconCache();

            ShowCleanupResult(result);
        }

        private void CleanSystemThumbnailCacheButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var result =
                SystemTweaksService.CleanThumbnailCache();

            ShowCleanupResult(result);
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
                MessageBox.Show(
                    "Не удалось запустить системный компонент.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}