using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WinTweaker.Services;

namespace WinTweaker.Pages;

public partial class WindowsAppsPage : Page
{
    private bool busy;
    public WindowsAppsPage() => InitializeComponent();

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (NavigationService?.CanGoBack == true) NavigationService.GoBack();
        else NavigationService?.Navigate(new ToolsPage());
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => UpdateControls();

    private void UpdateControls()
    {
        var row = Apps.SelectedItem as WindowsAppRow;
        Apps.IsEnabled = RefreshButton.IsEnabled = !busy;
        ChangeButton.IsEnabled = !busy && row != null;
        ChangeButton.Content = row?.Installed == false ? "Восстановить" : "Удалить";
        StoreButton.Visibility = !busy && row?.Installed == false ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RefreshAsync(WindowsAppRow? change = null)
    {
        if (busy) return;
        busy = true;
        UpdateControls();
        var selection = Apps.SelectedItem as WindowsAppRow;
        string? actionError = null;
        try
        {
            if (change != null)
            {
                Notice.Text = change.Installed ? "Удаление…" : "Восстановление из локального пакета…";
                try { await WindowsAppsService.ChangeAsync(change.App, !change.Installed); }
                catch (Exception ex) { actionError = ex.Message; }
            }
            Notice.Text = "Проверка установленных приложений…";
            var rows = await WindowsAppsService.ListAsync();
            Apps.ItemsSource = rows;
            Apps.SelectedItem = rows.FirstOrDefault(row => row.App == selection?.App);
            Notice.Text = $"Установлено {rows.Count(row => row.Installed)} из {rows.Count} приложений каталога.";
            if (change != null)
            {
                var current = rows.First(row => row.App == change.App);
                Notice.Text = current.Installed != change.Installed
                    ? $"{current.Title} — {current.Status.ToLowerInvariant()}."
                    : (change.Installed ? "Не удалось удалить приложение. " : "Для установки откройте Microsoft Store. ") + actionError;
            }
        }
        catch (Exception ex)
        {
            Apps.ItemsSource = null;
            Notice.Text = $"Не удалось проверить список приложений. {ex.Message} {actionError}";
        }
        finally { busy = false; UpdateControls(); }
    }

    private async void Change_Click(object sender, RoutedEventArgs e)
    {
        if (Apps.SelectedItem is not WindowsAppRow row) return;
        if (row.Installed && AppDialogs.Show(Window.GetWindow(this),
            $"Удалить «{row.Title}» для текущего пользователя? Локальные данные приложения могут быть потеряны.",
            "Приложения Windows", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await RefreshAsync(row);
    }

    private void Store_Click(object sender, RoutedEventArgs e)
    {
        if (Apps.SelectedItem is not WindowsAppRow row || row.Installed) return;
        try
        {
            Process.Start(new ProcessStartInfo($"ms-windows-store://pdp/?ProductId={row.App.StoreId}") { UseShellExecute = true });
            Notice.Text = "Завершите установку в Microsoft Store, затем обновите список.";
        }
        catch (Exception ex) { Notice.Text = $"Не удалось открыть Microsoft Store. {ex.Message}"; }
    }
}
