using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinTweaker.Services;

namespace WinTweaker.Pages;

public partial class RestoreCenterPage : Page
{
    public RestoreCenterPage()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateUndoButtons();
    }

    private void UpdateUndoButtons()
    {
        UndoGameModeButton.IsEnabled = GameModeService.HasUndo();
        UndoDisplayButton.IsEnabled = DisplayService.HasSavedOriginalMode();
    }

    private async void CreatePoint_Click(object sender, RoutedEventArgs e)
    {
        CreatePointButton.IsEnabled = false;
        PointStatus.Text = "Windows создаёт и проверяет точку восстановления…";
        try { PointStatus.Text = await RestorePointService.CreateAsync(); }
        catch (Exception ex) { PointStatus.Text = $"Не удалось запросить создание точки: {ex.Message}"; }
        finally { CreatePointButton.IsEnabled = true; }
    }

    private void OpenWizard_Click(object sender, RoutedEventArgs e) =>
        Run("открыть мастер Windows", RestorePointService.OpenWizard);

    private void ExportSettings_Click(object sender, RoutedEventArgs e) => Run("экспортировать настройки", () =>
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Копия настроек (*.json)|*.json", DefaultExt = ".json",
            FileName = "WinTweaker-settings.json", OverwritePrompt = true
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        SettingsService.ExportBackup(dialog.FileName);
        Show("Копия настроек сохранена.");
    });

    private void ImportSettings_Click(object sender, RoutedEventArgs e) => Run("импортировать настройки", () =>
    {
        var dialog = new OpenFileDialog { Filter = "Копия настроек (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var settings = SettingsService.ReadBackup(dialog.FileName);
        if (!Confirm("Заменить текущие настройки WinTweaker настройками из выбранной копии?")) return;
        SettingsService.ApplyBackup(settings);
        (Window.GetWindow(this) as MainWindow)?.ApplyAppSettings();
        Show("Настройки WinTweaker восстановлены.");
    });

    private void UndoGameMode_Click(object sender, RoutedEventArgs e) => Run("откатить игровой режим", () =>
    {
        if (!Confirm("Отменить последнее изменение игрового режима в WinTweaker?")) return;
        Show(GameModeService.UndoLastChange()
            ? "Предыдущее значение игрового режима восстановлено."
            : "Откат недоступен: копия отсутствует или игровой режим уже изменился вне WinTweaker.");
        UpdateUndoButtons();
    });

    private void UndoDisplay_Click(object sender, RoutedEventArgs e) => Run("вернуть разрешение", () =>
    {
        if (!Confirm("Вернуть исходное разрешение основного экрана?")) return;
        Show(DisplayService.RestoreOriginalMode()
            ? "Windows приняла запрос на возврат исходного режима экрана."
            : "Не удалось вернуть исходное разрешение.");
        UpdateUndoButtons();
    });

    private void Run(string action, Action operation)
    {
        try { operation(); }
        catch (Exception ex) { Show($"Не удалось {action}:\n{ex.Message}", MessageBoxImage.Error); }
    }

    private void Show(string message, MessageBoxImage icon = MessageBoxImage.Information) =>
        AppDialogs.Show(Window.GetWindow(this), message, "WinTweaker", MessageBoxButton.OK, icon);

    private bool Confirm(string message) =>
        AppDialogs.Show(Window.GetWindow(this), message, "WinTweaker", MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
}
