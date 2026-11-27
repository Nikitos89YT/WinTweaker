using System.Windows;
using System.Windows.Controls;
using WinTweaker.Services;

namespace WinTweaker.Pages
{
    public partial class SettingsPage : Page
    {
        private bool _isLoadingSettings;

        public SettingsPage()
        {
            InitializeComponent();

            Loaded += SettingsPage_Loaded;
        }


        // =========================================================
        // LOAD
        // =========================================================

        private void SettingsPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            LoadSettingsToInterface();
        }


        private void LoadSettingsToInterface()
        {
            _isLoadingSettings = true;

            AppSettings settings =
                SettingsService.Current;


            // =====================================================
            // INTERFACE
            // =====================================================

            DarkThemeCheckBox.IsChecked =
                settings.DarkTheme;

            AccentColorComboBox.SelectedValue =
                settings.AccentColor;


            // =====================================================
            // BEHAVIOR
            // =====================================================

            ConfirmDangerousActionsCheckBox.IsChecked =
                settings.ConfirmDangerousActions;

            RunAsAdministratorCheckBox.IsChecked =
                settings.RunAsAdministrator;

            ShowNotificationsCheckBox.IsChecked =
                settings.ShowNotifications;


            // =====================================================
            // STARTUP
            // =====================================================

            SaveStartupColumnsCheckBox.IsChecked =
                settings.SaveStartupColumns;

            SaveStartupColumnOrderCheckBox.IsChecked =
                settings.SaveStartupColumnOrder;

            ShowSystemStartupItemsCheckBox.IsChecked =
                settings.ShowSystemStartupItems;


            // =====================================================
            // SECURITY
            // =====================================================

            CreateRestorePointCheckBox.IsChecked =
                settings.CreateRestorePointBeforeChanges;

            BackupBeforeChangesCheckBox.IsChecked =
                settings.BackupBeforeChanges;


            _isLoadingSettings = false;
        }


        // =========================================================
        // SAVE
        // =========================================================

        private void SaveSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            PersistSettings();
        }

        private void Setting_Click(object sender, RoutedEventArgs e) => PersistSettings();

        private void PersistSettings()
        {
            if (_isLoadingSettings || !IsLoaded) return;
            try
            {
                SettingsService.ApplyBackup(ReadInterface());
                ApplySettingsToWindow();
            }
            catch (Exception ex)
            {
                LoadSettingsToInterface();
                MessageBox.Show($"Не удалось сохранить настройки. {ex.Message}", "WinTweaker");
            }
        }

        // =========================================================
        // SAVE DATA
        // =========================================================

        private AppSettings ReadInterface()
        {
            var settings = new AppSettings { UiScale = SettingsService.Current.UiScale };


            // =====================================================
            // INTERFACE
            // =====================================================

            settings.DarkTheme =
                DarkThemeCheckBox.IsChecked == true;

            settings.AccentColor =
                AccentColorComboBox.SelectedValue
                as string
                ?? "Blue";


            // =====================================================
            // BEHAVIOR
            // =====================================================

            settings.ConfirmDangerousActions =
                ConfirmDangerousActionsCheckBox.IsChecked == true;

            settings.RunAsAdministrator =
                RunAsAdministratorCheckBox.IsChecked == true;

            settings.ShowNotifications =
                ShowNotificationsCheckBox.IsChecked == true;


            // =====================================================
            // STARTUP
            // =====================================================

            settings.SaveStartupColumns =
                SaveStartupColumnsCheckBox.IsChecked == true;

            settings.SaveStartupColumnOrder =
                SaveStartupColumnOrderCheckBox.IsChecked == true;

            settings.ShowSystemStartupItems =
                ShowSystemStartupItemsCheckBox.IsChecked == true;


            // =====================================================
            // SECURITY
            // =====================================================

            settings.CreateRestorePointBeforeChanges =
                CreateRestorePointCheckBox.IsChecked == true;

            settings.BackupBeforeChanges =
                BackupBeforeChangesCheckBox.IsChecked == true;
            return settings;
        }


        // =========================================================
        // APPLY
        // =========================================================

        private void ApplySettingsToWindow()
        {
            Window? window =
                Window.GetWindow(this);

            if (window is MainWindow mainWindow)
            {
                mainWindow.ApplyAppSettings();
            }
        }


        // =========================================================
        // LIVE ACCENT COLOR
        // =========================================================

        private void AccentColorComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (AccentColorComboBox == null)
                return;

            if (_isLoadingSettings)
                return;


            PersistSettings();
        }


        // =========================================================
        // RESET
        // =========================================================

        private void ResetSettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBoxResult result =
                MessageBox.Show(
                    "Сбросить настройки WinTweaker к значениям по умолчанию?",
                    "Сброс настроек",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;


            try { SettingsService.ResetToDefaults(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка сохранения"); return; }

            LoadSettingsToInterface();

            ApplySettingsToWindow();


            if (SettingsService.Current.ShowNotifications) MessageBox.Show(
                "Настройки сброшены.",
                "WinTweaker",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }


        // =========================================================
        // SETTINGS FILE
        // =========================================================

        private void OpenSettingsFileButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string path =
                    SettingsService.GetSettingsFilePath();


                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName =
                            "explorer.exe",

                        Arguments =
                            $"/select,\"{path}\"",

                        UseShellExecute =
                            true
                    });
            }
            catch
            {
                MessageBox.Show(
                    "Не удалось открыть файл настроек.",
                    "WinTweaker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}