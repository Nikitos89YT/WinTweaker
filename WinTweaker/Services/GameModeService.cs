using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace WinTweaker.Services
{
    public static class GameModeService
    {
        private const string RegistryPath = @"Software\Microsoft\GameBar";
        private const string GameModeValue = "AutoGameModeEnabled";
        private static readonly string UndoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinTweaker", "game-mode-undo.json");
        private static string PendingPath => UndoPath + ".pending";
        private sealed record UndoState(
            [property: System.Text.Json.Serialization.JsonRequired] int? Previous,
            [property: System.Text.Json.Serialization.JsonRequired] int Applied);

        private static UndoState ReadUndo(string path)
        {
            var state = JsonSerializer.Deserialize<UndoState>(File.ReadAllText(path));
            if (state is null || state.Applied is not (0 or 1) || state.Previous == state.Applied)
                throw new InvalidDataException("Копия игрового режима повреждена.");
            return state;
        }

        // Recover a toggle interrupted between writing the registry and publishing the undo.
        private static void FinishPending(int? current)
        {
            if (!File.Exists(PendingPath)) return;
            if (ReadUndo(PendingPath).Applied == current)
                File.Move(PendingPath, UndoPath, overwrite: true);
            else
                File.Delete(PendingPath);
        }

        private static int? ReadValue(RegistryKey? key) => key?.GetValue(GameModeValue) switch
        {
            null => null,
            int value => value,
            _ => throw new InvalidDataException("Не удалось безопасно сохранить исходное значение игрового режима.")
        };

        public static bool HasUndo() => File.Exists(UndoPath) || File.Exists(PendingPath);

        public static bool UndoLastChange()
        {
            if (!HasUndo())
                return false;

            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true);
            int? current = ReadValue(key);
            FinishPending(current);
            if (key is null || !File.Exists(UndoPath))
                return false;

            var state = ReadUndo(UndoPath);
            if (current != state.Applied)
                return false;

            if (state.Previous is int previous)
                key.SetValue(GameModeValue, previous, RegistryValueKind.DWord);
            else
                key.DeleteValue(GameModeValue);
            File.Delete(UndoPath);
            return true;
        }

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
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            int? previous = ReadValue(key);
            FinishPending(previous);
            int applied = enabled ? 1 : 0;
            if (previous == applied)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(UndoPath)!);
            string temporary = UndoPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew))
                    JsonSerializer.Serialize(stream, new UndoState(previous, applied));
                File.Move(temporary, PendingPath);
                key.SetValue(GameModeValue, applied, RegistryValueKind.DWord);
                FinishPending(applied);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}