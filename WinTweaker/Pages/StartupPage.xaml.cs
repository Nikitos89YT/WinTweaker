using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinTweaker.Models;
using WinTweaker.Services;

namespace WinTweaker.Pages
{
    public partial class StartupPage : Page
    {
        private StartupItem? _selectedItem;

        public StartupPage()
        {
            InitializeComponent();

            Loaded += StartupPage_Loaded;
        }

        // =========================================================
        // PAGE LOAD
        // =========================================================

        private void StartupPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            LoadStartupItems();
        }

        // =========================================================
        // LOAD STARTUP ITEMS
        // =========================================================

        private void LoadStartupItems(
            string? selectedName = null)
        {
            // Обычный автозапуск:
            // HKCU/HKLM Run, RunOnce, Startup Folder,
            // StartupApproved и т.д.
            var items =
                StartupService.GetStartupItems();

            // Системный автозапуск:
            // Winlogon + задачи Планировщика.
            var systemItems =
                StartupSystemService.GetAllSystemStartup();

            foreach (StartupSystemItem systemItem
                     in systemItems)
            {
                items.Add(
                    ConvertSystemItem(
                        systemItem));
            }

            // Обновляем таблицу.
            StartupDataGrid.ItemsSource = null;
            StartupDataGrid.ItemsSource = items;

            // =====================================================
            // RESTORE SELECTION
            // =====================================================

            if (!string.IsNullOrWhiteSpace(selectedName))
            {
                var itemToSelect =
                    items.FirstOrDefault(
                        x => x.Name.Equals(
                            selectedName,
                            StringComparison.OrdinalIgnoreCase));

                if (itemToSelect != null)
                {
                    StartupDataGrid.SelectedItem =
                        itemToSelect;

                    StartupDataGrid.ScrollIntoView(
                        itemToSelect);
                }
            }

            // Если ничего не выбрано —
            // автоматически выбираем первую запись.
            if (StartupDataGrid.SelectedItem == null &&
                StartupDataGrid.Items.Count > 0)
            {
                StartupDataGrid.SelectedIndex = 0;
            }

            UpdateDetails();
        }

        // =========================================================
        // SYSTEM ITEM → STARTUP ITEM
        // =========================================================

        private StartupItem ConvertSystemItem(
            StartupSystemItem systemItem)
        {
            return new StartupItem
            {
                Name =
                    systemItem.Name,

                Command =
                    systemItem.Value,

                Source =
                    systemItem.Source,

                Location =
                    systemItem.Location,

                // Системные записи нельзя включать/выключать
                // через обычный механизм StartupApproved.
                IsEnabled =
                    true,

                ItemType =
                    systemItem.IsCritical
                        ? "Системный"
                        : "Событие входа",

                // Передаём настоящий статус
                // системной записи.
                CustomStatusText =
                    systemItem.StatusText,

                // Эти поля для системных записей
                // не используются, но оставляем
                // пустыми строками, чтобы не ломать
                // существующую модель StartupItem.
                RegistryRoot =
                    "",

                RegistrySubKey =
                    "",

                RegistryValueName =
                    "",

                RegistryValueKind =
                    0,

                StartupApprovedSubKey =
                    "",

                OriginalPath =
                    systemItem.Value,

                CanToggle =
                    false
            };
        }

        // =========================================================
        // SELECTION
        // =========================================================

        private void StartupDataGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            UpdateDetails();
        }

        private void UpdateDetails()
        {
            _selectedItem =
                StartupDataGrid.SelectedItem
                as StartupItem;

            if (_selectedItem == null)
            {
                SelectedNameText.Text =
                    "Ничего не выбрано";

                SelectedSourceText.Text =
                    "";

                SelectedLocationText.Text =
                    "";

                SelectedCommandText.Text =
                    "";

                ToggleStartupButton.IsEnabled =
                    false;

                ToggleStartupButton.Content =
                    "Отключить";

                return;
            }

            // =====================================================
            // DETAILS
            // =====================================================

            SelectedNameText.Text =
                _selectedItem.Name;

            SelectedSourceText.Text =
                _selectedItem.Source;

            SelectedLocationText.Text =
                _selectedItem.Location;

            SelectedCommandText.Text =
                _selectedItem.Command;

            // =====================================================
            // TOGGLE BUTTON
            // =====================================================

            ToggleStartupButton.IsEnabled =
                _selectedItem.CanToggle;

            if (_selectedItem.CanToggle)
            {
                ToggleStartupButton.Content =
                    _selectedItem.IsEnabled
                        ? "Отключить"
                        : "Включить";
            }
            else
            {
                ToggleStartupButton.Content =
                    "Системная запись";
            }
        }

        // =========================================================
        // ENABLE / DISABLE
        // =========================================================

        private void ToggleStartupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedItem == null)
                return;

            // Системные записи здесь специально защищены.
            if (!_selectedItem.CanToggle)
            {
                MessageBox.Show(
                    "Эта запись является системной " +
                    "и не может быть отключена из WinTweaker.",
                    "Автозагрузка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            string itemName =
                _selectedItem.Name;

            bool currentlyEnabled =
                _selectedItem.IsEnabled;

            string action =
                currentlyEnabled
                    ? "отключить"
                    : "включить";

            MessageBoxResult confirmation =
                MessageBox.Show(
                    $"Ты действительно хочешь {action} запись:\n\n" +
                    itemName,
                    "Автозагрузка",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (confirmation !=
                MessageBoxResult.Yes)
            {
                return;
            }

            bool success;

            if (currentlyEnabled)
            {
                success =
                    StartupService.Disable(
                        _selectedItem);
            }
            else
            {
                success =
                    StartupService.Enable(
                        _selectedItem);
            }

            if (!success)
            {
                MessageBox.Show(
                    $"Не удалось {action} запись:\n\n" +
                    itemName,
                    "Автозагрузка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            // После изменения перезагружаем таблицу
            // и возвращаем выбор.
            LoadStartupItems(
                itemName);
        }

        // =========================================================
        // REFRESH
        // =========================================================

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string? selectedName =
                _selectedItem?.Name;

            LoadStartupItems(
                selectedName);
        }

        // =========================================================
        // OPEN STARTUP FOLDER
        // =========================================================

        private void OpenStartupFolderButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string startupFolder =
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Startup);

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName =
                            startupFolder,

                        UseShellExecute =
                            true
                    });
            }
            catch
            {
                MessageBox.Show(
                    "Не удалось открыть папку автозагрузки.",
                    "Автозагрузка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // COPY COMMAND
        // =========================================================

        private void CopyCommandButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedItem == null)
                return;

            if (string.IsNullOrWhiteSpace(
                    _selectedItem.Command))
            {
                return;
            }

            try
            {
                Clipboard.SetText(
                    _selectedItem.Command);
            }
            catch
            {
                MessageBox.Show(
                    "Не удалось скопировать команду.",
                    "Автозагрузка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // =========================================================
        // OPEN LOCATION
        // =========================================================

        private void OpenLocationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_selectedItem == null)
                return;

            try
            {
                // =================================================
                // WINLOGON
                // =================================================

                if (_selectedItem.Source.Equals(
                        "Winlogon",
                        StringComparison.OrdinalIgnoreCase))
                {
                    bool opened =
                        StartupSystemService
                            .OpenWinlogonLocation();

                    if (!opened)
                    {
                        MessageBox.Show(
                            "Не удалось открыть редактор реестра.",
                            "Автозагрузка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }

                    return;
                }

                // =================================================
                // Обычный элемент
                // =================================================

                string location =
                    _selectedItem.Location;

                if (string.IsNullOrWhiteSpace(
                        location))
                {
                    MessageBox.Show(
                        "Расположение неизвестно.",
                        "Автозагрузка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    return;
                }

                // Папка.
                if (Directory.Exists(location))
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName =
                                location,

                            UseShellExecute =
                                true
                        });

                    return;
                }

                // Файл.
                if (File.Exists(location))
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo
                        {
                            FileName =
                                "explorer.exe",

                            Arguments =
                                $"/select,\"{location}\"",

                            UseShellExecute =
                                true
                        });

                    return;
                }

                // Для некоторых типов записей Location —
                // это не физический путь, а, например,
                // раздел реестра или путь задачи.
                MessageBox.Show(
                    "Не удалось открыть расположение:\n\n" +
                    location,
                    "Автозагрузка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch
            {
                MessageBox.Show(
                    "Не удалось открыть расположение.",
                    "Автозагрузка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}