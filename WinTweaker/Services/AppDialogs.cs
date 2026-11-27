using System.Windows;

namespace WinTweaker.Services;

internal static class AppDialogs
{
    public static MessageBoxResult Show(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.None)
        => Show(null, text, title, buttons, icon, defaultResult);

    public static MessageBoxResult Show(Window? owner, string text, string title, MessageBoxButton buttons,
        MessageBoxImage icon, MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        if (buttons == MessageBoxButton.YesNo && !SettingsService.Current.ConfirmDangerousActions)
            return MessageBoxResult.Yes;
        if (buttons == MessageBoxButton.OK && icon == MessageBoxImage.Information && !SettingsService.Current.ShowNotifications)
            return MessageBoxResult.OK;
        return owner is null ? MessageBox.Show(text, title, buttons, icon, defaultResult)
            : MessageBox.Show(owner, text, title, buttons, icon, defaultResult);
    }
}
