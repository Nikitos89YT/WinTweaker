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

        public UnlockerPage()
        {
            InitializeComponent();

            UpdateTaskManagerStatus();
        }

        // ========================================
        // ДИСПЕТЧЕР ЗАДАЧ
        // ========================================

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
            MessageBox.Show(
                message,
                "WinTweaker",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}