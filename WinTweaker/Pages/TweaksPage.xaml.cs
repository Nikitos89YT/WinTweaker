using System;
using System.Collections.Generic;
using System.Linq;
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

        private async Task<bool> PrepareTweakAsync()
        {
            if (!SettingsService.Current.CreateRestorePointBeforeChanges) return true;
            IsEnabled = false;
            try
            {
                string status = await RestorePointService.CreateAsync();
                if (status == RestorePointService.Created) return IsLoaded;
                MessageBox.Show(status, "Твик не применён", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка создания точки"); }
            finally { IsEnabled = true; }
            return false;
        }

        private async void GameModeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!await PrepareTweakAsync()) return;
            bool? status = GameModeService.GetStatus();

            try
            {
                GameModeService.SetStatus(status != true);
            }
            catch (Exception ex)
            {
                AppDialogs.Show("Не удалось изменить игровой режим:\n" + ex.Message,
                    "WinTweaker", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private async void StretchButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (StretchResolutionComboBox.SelectedItem
                is not DisplayModeInfo selectedMode)
            {
                AppDialogs.Show(
                    "Сначала выбери разрешение.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!await PrepareTweakAsync()) return;
            bool success =
                DisplayService.SetResolution(
                    selectedMode.Width,
                    selectedMode.Height,
                    selectedMode.RefreshRate);

            if (!success)
            {
                AppDialogs.Show(
                    "Не удалось применить выбранное разрешение.\n\n" +
                    "Возможно, монитор не поддерживает этот режим.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            UpdateDisplayInfo();

            AppDialogs.Show(
                $"Разрешение изменено на " +
                $"{selectedMode.Width} × {selectedMode.Height} @ {selectedMode.RefreshRate} Гц.",
                "WinTweaker",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void RestoreResolutionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!await PrepareTweakAsync()) return;
            bool success =
                DisplayService.RestoreOriginalMode();

            if (!success)
            {
                AppDialogs.Show(
                    "Исходный режим ещё не был сохранён.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            UpdateDisplayInfo();

            AppDialogs.Show(
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
            UpdateDnsInfo();
        }

        private async void NetworkAdapterToggleButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (NetworkAdapterComboBox.SelectedItem
                is not NetworkAdapterInfo adapter)
            {
                AppDialogs.Show(
                    "Сначала выбери сетевой адаптер.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            string selectedAdapterName =
                adapter.Name;

            if (adapter.IsEnabled && AppDialogs.Show("Отключить выбранный сетевой адаптер?", "Сеть", MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            if (!await PrepareTweakAsync()) return;
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
                AppDialogs.Show(
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
                AppDialogs.Show(
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

        private async void ApplyDnsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (NetworkAdapterComboBox.SelectedItem
                is not NetworkAdapterInfo adapter)
            {
                AppDialogs.Show(
                    "Сначала выбери сетевой адаптер.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (DnsPresetComboBox.SelectedItem
                is not DnsPreset preset)
            {
                AppDialogs.Show(
                    "Сначала выбери DNS-пресет.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (!await PrepareTweakAsync()) return;
            bool success =
                NetworkService.SetDnsPreset(
                    adapter.Name,
                    preset);

            if (!success)
            {
                AppDialogs.Show(
                    $"Не удалось применить DNS-пресет «{preset.Name}».\n\n" +
                    "Проверь, что сетевой адаптер включён.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            UpdateDnsInfo();

            AppDialogs.Show(
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

            AppDialogs.Show(
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

        private async void ResetWinsockButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (AppDialogs.Show("Сбросить Winsock?", "Сеть", MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            if (!await PrepareTweakAsync()) return;
            bool result =
                NetworkService.ResetWinsock();

            AppDialogs.Show(
                result
                    ? "Winsock успешно сброшен.\n\nДля применения изменений может потребоваться перезагрузка Windows."
                    : "Не удалось сбросить Winsock.",
                "WinTweaker",
                MessageBoxButton.OK,
                result
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Error);
        }

        private async void ResetTcpIpButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (AppDialogs.Show("Сбросить настройки TCP/IP?", "Сеть", MessageBoxButton.YesNo,
                MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            if (!await PrepareTweakAsync()) return;
            bool result =
                NetworkService.ResetTcpIp();

            AppDialogs.Show(
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
                NetworkService.GetCurrentDns((NetworkAdapterComboBox.SelectedItem as NetworkAdapterInfo)?.Name);
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
    }
}
