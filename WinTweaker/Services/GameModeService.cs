using Microsoft.Win32;

namespace WinTweaker.Services
{
    public static class GameModeService
    {
        private const string RegistryPath = @"Software\Microsoft\GameBar";
        private const string GameModeValue = "AutoGameModeEnabled";

        /// <summary>
        /// Возвращает состояние Game Mode:
        /// true  = включён
        /// false = выключен
        /// null  = значение не задано или не удалось прочитать
        /// </summary>
        public static bool? GetStatus()
        {
            try
            {
                using RegistryKey? key =
                    Registry.CurrentUser.OpenSubKey(RegistryPath);

                if (key == null)
                    return null;

                object? value = key.GetValue(GameModeValue);

                if (value == null)
                    return null;

                return value switch
                {
                    int intValue => intValue == 1,
                    long longValue => longValue == 1,
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Включает или выключает Game Mode.
        /// true  = включить
        /// false = выключить
        /// </summary>
        public static void SetStatus(bool enabled)
        {
            using RegistryKey key =
                Registry.CurrentUser.CreateSubKey(RegistryPath);

            key.SetValue(
                GameModeValue,
                enabled ? 1 : 0,
                RegistryValueKind.DWord
            );
        }
    }
}