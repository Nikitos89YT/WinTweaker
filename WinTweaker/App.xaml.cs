using System.Windows;
using WinTweaker.Services;

namespace WinTweaker
{
    public partial class App : Application
    {
        protected override void OnStartup(
            StartupEventArgs e)
        {
            // Загружаем сохранённые настройки
            // до открытия главного окна.
            SettingsService.Load();

            base.OnStartup(e);
        }
    }
}