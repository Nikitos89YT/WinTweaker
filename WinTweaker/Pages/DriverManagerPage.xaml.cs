using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using WinTweaker.Services;

namespace WinTweaker.Pages;

public partial class DriverManagerPage : Page
{
    private ICollectionView? _view;
    private bool _loaded;
    private bool _loading;
    private bool _hasWarnings;
    private Task<DriverExportResult>? _observedExport;

    public DriverManagerPage() => InitializeComponent();

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (DriverExportCoordinator.CurrentTask is { } task && !ReferenceEquals(task, _observedExport))
            _ = ShowExportAsync(task);
        if (_loaded) return;
        _loaded = true;
        await LoadDriversAsync();
    }

    private async void RefreshDriversButton_Click(object sender, RoutedEventArgs e) => await LoadDriversAsync();

    private async Task LoadDriversAsync()
    {
        if (_loading) return;
        _loading = true;
        RefreshButton.IsEnabled = false;
        InventoryStatus.Text = "Загрузка устройств и драйверов…";
        WarningsText.Text = "";
        try
        {
            var snapshot = await DriverInventoryService.LoadAsync();
            var rows = snapshot.Rows.Select(row => row with
            {
                Device = Display(row.Device), Provider = Display(row.Provider),
                Version = Display(row.Version), InfName = Display(row.InfName), Status = Display(row.Status)
            }).ToList();
            _view = CollectionViewSource.GetDefaultView(rows);
            _view.Filter = item => ProblemFilter.IsChecked != true || ((DriverRow)item).HasProblem;
            DriversGrid.ItemsSource = _view;
            _hasWarnings = snapshot.Warnings.Count > 0;
            WarningsText.Text = string.Join(Environment.NewLine, snapshot.Warnings);
            UpdateInventoryStatus();
        }
        catch (Exception ex)
        {
            DriversGrid.ItemsSource = null;
            _view = null;
            InventoryStatus.Text = "Не удалось загрузить список устройств.";
            WarningsText.Text = ex.Message;
        }
        finally
        {
            _loading = false;
            RefreshButton.IsEnabled = true;
        }
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private void ProblemFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (_view is null) return;
        _view.Refresh();
        UpdateInventoryStatus();
    }

    private void UpdateInventoryStatus()
    {
        if (_view is null) return;
        InventoryStatus.Text = _view.IsEmpty
            ? (_hasWarnings ? "Нет доступных результатов; см. предупреждения." : "Устройства не найдены для выбранного фильтра.")
            : "Список устройств и драйверов Windows";
    }

    private void BackToToolsButton_Click(object sender, RoutedEventArgs e) => NavigationService?.Navigate(new ToolsPage());

    private void OpenDeviceManagerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SystemTweaksService.LaunchSystemComponent(SystemComponent.DeviceManager))
            AppDialogs.Show("Не удалось открыть диспетчер устройств.", "WinTweaker", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private async void ExportOemDriversButton_Click(object sender, RoutedEventArgs e)
    {
        if (DriverExportCoordinator.CurrentTask is { IsCompleted: false }) return;
        var dialog = new OpenFolderDialog { Title = "Выберите родительскую папку для копии драйверов" };
        if (dialog.ShowDialog() != true)
        {
            ExportNotice.Text = "Выбор папки отменён. Экспорт не запускался.";
            return;
        }
        ExportNotice.Text = "";
        var task = DriverExportCoordinator.TryStart(() => DriverExportService.ExportAsync(dialog.FolderName));
        if (task is not null) await ShowExportAsync(task);
    }

    private async Task ShowExportAsync(Task<DriverExportResult> task)
    {
        _observedExport = task;
        ExportButton.IsEnabled = false;
        ExportStatus.Text = "Экспорт OEM-пакетов выполняется…";
        try
        {
            var result = await task;
            if (!ReferenceEquals(task, DriverExportCoordinator.CurrentTask) || !ReferenceEquals(task, _observedExport)) return;
            string message = result.Status switch
            {
                DriverExportStatus.Completed => "Экспорт завершён. Полнота копии независимо не проверена.",
                DriverExportStatus.NeedsAdministrator => "Для экспорта требуются права администратора.",
                DriverExportStatus.Partial => "Ошибка экспорта: копия неполная; созданные файлы сохранены.",
                DriverExportStatus.NoFiles => "Экспорт завершился без файлов INF; копия не подтверждена.",
                _ => "Не удалось выполнить экспорт."
            };
            ExportStatus.Text = message
                + (result.Destination is null ? "" : $"\nПапка: {result.Destination}")
                + (result.ExitCode is null ? "" : $"\nКод процесса: {result.ExitCode}")
                + (string.IsNullOrWhiteSpace(result.Detail) ? "" : $"\n{result.Detail}");
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(task, DriverExportCoordinator.CurrentTask) && ReferenceEquals(task, _observedExport))
                ExportStatus.Text = $"Не удалось выполнить экспорт: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(task, DriverExportCoordinator.CurrentTask) && ReferenceEquals(task, _observedExport))
                ExportButton.IsEnabled = true;
        }
    }
}

internal static class DriverExportCoordinator
{
    private static readonly object Gate = new();
    internal static Task<DriverExportResult>? CurrentTask { get; private set; }

    internal static Task<DriverExportResult>? TryStart(Func<Task<DriverExportResult>> runner)
    {
        lock (Gate)
        {
            if (CurrentTask is { IsCompleted: false }) return null;
            CurrentTask = RunAsync(runner);
            return CurrentTask;
        }
    }

    private static async Task<DriverExportResult> RunAsync(Func<Task<DriverExportResult>> runner)
    {
        try { return await runner(); }
        catch (Exception ex) { return new(DriverExportStatus.Failed, null, null, ex.Message); }
    }
}
