using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinTweaker.Pages;
using WinTweaker.Services;

namespace WinTweaker
{
    public partial class MainWindow : Window
    {
        private Button? _activeButton;

        private Brush ActiveBrush =
            new SolidColorBrush(Color.FromRgb(38, 48, 75));

        public MainWindow()
        {
            InitializeComponent();

            ShowHome();
            ApplyAppSettings();
        }

        // =========================================================
        // APPLY SETTINGS
        // =========================================================

        public void ApplyAppSettings()
        {
            // Масштаб полностью отключён.
            // Интерфейс всегда работает в обычном размере окна.

            Width = 1100;
            Height = 700;

            MinWidth = 950;
            MinHeight = 600;

            ActiveBrush =
                CreateAccentBrush(
                    SettingsService.Current.AccentColor);

            if (_activeButton != null)
                _activeButton.Background = ActiveBrush;
        }

        // =========================================================
        // ACCENT COLOR
        // =========================================================

        private static Brush CreateAccentBrush(
            string accentColor)
        {
            Color color;

            switch (accentColor)
            {
                case "Purple":
                    color = Color.FromRgb(68, 48, 95);
                    break;

                case "Green":
                    color = Color.FromRgb(39, 76, 55);
                    break;

                case "Orange":
                    color = Color.FromRgb(91, 58, 30);
                    break;

                case "Red":
                    color = Color.FromRgb(91, 42, 48);
                    break;

                case "Blue":
                default:
                    color = Color.FromRgb(38, 48, 75);
                    break;
            }

            return new SolidColorBrush(color);
        }

        // =========================================================
        // NAVIGATION
        // =========================================================

        private void ResetButtons()
        {
            HomeButton.Background = Brushes.Transparent;
            TweaksButton.Background = Brushes.Transparent;
            UnlockerButton.Background = Brushes.Transparent;
            StartupButton.Background = Brushes.Transparent;
            ToolsButton.Background = Brushes.Transparent;
            SettingsButton.Background = Brushes.Transparent;
        }

        private void SetActiveButton(Button button)
        {
            ResetButtons();

            _activeButton = button;
            button.Background = ActiveBrush;
        }

        private void ShowHome()
        {
            SetActiveButton(HomeButton);
            MainFrame.Navigate(new HomePage());
        }

        private void HomeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowHome();
        }

        private void ShowTweaks()
        {
            SetActiveButton(TweaksButton);
            MainFrame.Navigate(new TweaksPage());
        }

        private void TweaksButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowTweaks();
        }

        private void ShowUnlocker()
        {
            SetActiveButton(UnlockerButton);
            MainFrame.Navigate(new UnlockerPage());
        }

        private void UnlockerButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowUnlocker();
        }

        private void ShowStartup()
        {
            SetActiveButton(StartupButton);
            MainFrame.Navigate(new StartupPage());
        }

        private void StartupButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowStartup();
        }

        private void ShowTools()
        {
            SetActiveButton(ToolsButton);
            MainFrame.Navigate(new ToolsPage());
        }

        private void ToolsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowTools();
        }

        private void ShowSettings()
        {
            SetActiveButton(SettingsButton);
            MainFrame.Navigate(new SettingsPage());
        }

        private void SettingsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ShowSettings();
        }
    }
}