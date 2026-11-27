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

            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            if (SettingsService.Current.RunAsAdministrator &&
                !new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            {
                try
                {
                    var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
                        { UseShellExecute = true, Verb = "runas" };
                    if (System.IO.Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                        start.ArgumentList.Add(typeof(App).Assembly.Location);
                    foreach (var arg in e.Args) start.ArgumentList.Add(arg);
                    System.Diagnostics.Process.Start(start);
                    Shutdown();
                    return;
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Запуск с правами администратора"); }
            }
            base.OnStartup(e);
        }
    }
}