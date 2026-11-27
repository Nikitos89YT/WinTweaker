using Microsoft.Win32;
using WinTweaker.Components;
using System.Windows;
using System.Windows.Media;
using WinTweaker.Services;

namespace WinTweaker.Pages
{
    public partial class UnlockerPage
    {
        private readonly SolidColorBrush _green =
            new SolidColorBrush(Color.FromRgb(100, 210, 130));

        private readonly SolidColorBrush _red =
            new SolidColorBrush(Color.FromRgb(240, 96, 96));

        private readonly SolidColorBrush _gray =
            new SolidColorBrush(Color.FromRgb(180, 185, 195));

        private readonly List<Action> _refreshPolicies = new();

        public UnlockerPage()
        {
            InitializeComponent();

            foreach (var restriction in UserRestriction.All)
            {
                var card = new UnlockerCard
                {
                    Title = restriction.Title,
                    Description = restriction.Description
                };
                PolicyCards.Children.Add(card);
                bool offerRestore = false;

                void Refresh()
                {
                    try
                    {
                        offerRestore = restriction.IsRestricted(Registry.CurrentUser);
                        card.Status = offerRestore ? "● Обнаружено ограничение" : "● Запрет не обнаружен";
                        card.StatusColor = offerRestore ? _red : _green;
                        card.ButtonText = offerRestore ? "Восстановить" : "Проверить";
                        card.ToolTip = null;
                    }
                    catch (Exception ex)
                    {
                        offerRestore = false;
                        card.Status = "● Не удалось прочитать политику";
                        card.StatusColor = _gray;
                        card.ButtonText = "Проверить";
                        card.ToolTip = ex.Message;
                    }
                }

                _refreshPolicies.Add(Refresh);
                card.Loaded += (_, _) => Refresh();
                card.ActionClicked += (_, _) =>
                {
                    try
                    {
                        if (offerRestore)
                            restriction.Restore(Registry.CurrentUser);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        ShowError("Windows запретила изменение политики. Запустите WinTweaker от имени администратора под той же учётной записью.");
                    }
                    catch (Exception ex)
                    {
                        ShowError(ex.Message);
                    }
                    Refresh();
                };
            }

            Loaded += (_, _) => UpdateTaskManagerStatus();
        }
        // ========================================
        // ДИСПЕТЧЕР ЗАДАЧ
        // ========================================

        private async void CheckAll_Click(object sender, RoutedEventArgs e)
        {
            IsEnabled = false;
            BatchResult.Text = "Проверяем ограничения…";
            try
            {
                BatchResult.Text = await Task.Run(CheckAndRestoreAll);
            }
            finally
            {
                IsEnabled = true;
                UpdateTaskManagerStatus();
                foreach (var refresh in _refreshPolicies)
                    refresh();
            }
        }

        private static string CheckAndRestoreAll()
        {
            int available = 0, restored = 0;
            var failures = new List<string>();

            void Check(string title, Func<bool?> isLocked, Action restore)
            {
                try
                {
                    switch (isLocked())
                    {
                        case false:
                            available++;
                            return;
                        case null:
                            failures.Add($"{title} — не удалось прочитать политику.");
                            return;
                    }
                    restore();
                    if (isLocked() == false)
                        restored++;
                    else
                        failures.Add($"{title} — снятие ограничения не подтверждено.");
                }
                catch (Exception ex)
                {
                    failures.Add($"{title} — {ex.Message}");
                }
            }

            Check("Диспетчер задач", TaskManagerUnlocker.IsLocked, () =>
            {
                if (!TaskManagerUnlocker.Unlock())
                    throw new InvalidOperationException("Windows не разрешила изменение или запрос UAC отменён.");
            });
            foreach (var policy in UserRestriction.All)
                Check(policy.Title, () => policy.IsRestricted(Registry.CurrentUser),
                    () => policy.Restore(Registry.CurrentUser));

            string summary = $"Без запрета: {available}. Разблокировано: {restored}. Ошибок: {failures.Count}.";
            return string.Join(Environment.NewLine, failures.Prepend(summary));
        }

        private void UpdateTaskManagerStatus()
        {
            bool? locked = TaskManagerUnlocker.IsLocked();

            if (locked == true)
            {
                TaskManagerCard.Status = "🔴 Заблокирован";
                TaskManagerCard.StatusColor = _red;
                TaskManagerCard.ButtonText = "Разблокировать";
            }
            else if (locked == false)
            {
                TaskManagerCard.Status = "🟢 Доступен";
                TaskManagerCard.StatusColor = _green;
                TaskManagerCard.ButtonText = "Тест блокировки";
            }
            else
            {
                TaskManagerCard.Status = "⚪ Не удалось определить";
                TaskManagerCard.StatusColor = _gray;
                TaskManagerCard.ButtonText = "Проверить";
            }
        }

        private void TaskManagerCard_ActionClicked(
            object sender,
            RoutedEventArgs e)
        {
            bool? locked = TaskManagerUnlocker.IsLocked();

            if (locked == null)
            {
                ShowError(
                    "Не удалось определить состояние Диспетчера задач.");

                return;
            }

            bool success;

            if (locked == true)
            {
                success = TaskManagerUnlocker.Unlock();
            }
            else
            {
                success = TaskManagerUnlocker.Lock();
            }

            if (!success)
            {
                ShowError(
                    "Операция не выполнена.\n\n" +
                    "Возможно, запрос UAC был отменён " +
                    "или Windows не разрешила изменение политики.");

                return;
            }

            UpdateTaskManagerStatus();
        }

        // ========================================
        // ОШИБКА
        // ========================================

        private static void ShowError(string message)
        {
            AppDialogs.Show(
                message,
                "WinTweaker",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
