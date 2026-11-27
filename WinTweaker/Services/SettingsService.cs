using System;
using System.IO;
using System.Text.Json;

namespace WinTweaker.Services
{
    public class OverlayOptions
    {
        public bool Cpu { get; set; } = true;
        public bool Gpu { get; set; } = true;
        public bool Ram { get; set; } = true;
        public bool Download { get; set; } = true;
        public bool Upload { get; set; } = true;
        public bool RamInGb { get; set; }
    }

    public class AppSettings
    {
        public OverlayOptions Overlay { get; set; } = new();
        // =========================================================
        // INTERFACE
        // =========================================================

        public bool DarkTheme { get; set; } = true;

        public string AccentColor { get; set; } = "Blue";

        public double UiScale { get; set; } = 1.0;

        // =========================================================
        // BEHAVIOR
        // =========================================================

        public bool ConfirmDangerousActions { get; set; } = true;

        public bool RunAsAdministrator { get; set; } = false;

        public bool ShowNotifications { get; set; } = true;

        // =========================================================
        // STARTUP PAGE
        // =========================================================

        public bool SaveStartupColumns { get; set; } = true;

        public bool SaveStartupColumnOrder { get; set; } = true;

        public bool ShowSystemStartupItems { get; set; } = true;

        // =========================================================
        // SECURITY
        // =========================================================

        public bool CreateRestorePointBeforeChanges { get; set; } = false;

        public bool BackupBeforeChanges { get; set; } = false;
    }

    public static class SettingsService
    {
        // =========================================================
        // PATH
        // =========================================================

        private static readonly string SettingsDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "WinTweaker");

        private static readonly string SettingsFile =
            Path.Combine(
                SettingsDirectory,
                "settings.json");

        // =========================================================
        // CURRENT SETTINGS
        // =========================================================

        public static AppSettings Current { get; private set; }
            = new AppSettings();

        // =========================================================
        // LOAD
        // =========================================================

        public static void Load()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                {
                    Current =
                        new AppSettings();

                    return;
                }

                string json =
                    File.ReadAllText(
                        SettingsFile);

                AppSettings? loaded =
                    JsonSerializer.Deserialize<AppSettings>(
                        json);

                Current =
                    loaded
                    ?? new AppSettings();
            }
            catch
            {
                Current =
                    new AppSettings();
            }
        }

        // =========================================================
        // SAVE
        // =========================================================

        public static bool Save()
        {
            try
            {
                Directory.CreateDirectory(
                    SettingsDirectory);

                WriteBackupFile(SettingsFile, Current);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ExportBackup(string path) =>
            WriteBackupFile(path, Current);

        public static AppSettings ReadBackup(string path)
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > 65536)
                throw new InvalidDataException("Файл настроек слишком большой.");

            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Ожидалась копия настроек WinTweaker.");

            var expected = JsonSerializer.SerializeToElement(new AppSettings())
                .EnumerateObject().Select(property => property.Name).ToHashSet();
            foreach (var property in root.EnumerateObject())
                if (!expected.Remove(property.Name))
                    throw new InvalidDataException($"Неизвестный или повторяющийся параметр: {property.Name}.");
            expected.Remove(nameof(AppSettings.Overlay)); // Older backups predate overlay preferences.
            if (expected.Count != 0)
                throw new InvalidDataException("В копии отсутствуют обязательные параметры.");

            var settings = root.Deserialize<AppSettings>()!;
            if (settings.AccentColor is not ("Blue" or "Purple" or "Green" or "Orange" or "Red") ||
                !double.IsFinite(settings.UiScale) || settings.UiScale <= 0)
                throw new InvalidDataException("Недопустимый цвет акцента или масштаб.");
            return settings;
        }

        public static void ApplyBackup(AppSettings settings)
        {
            Directory.CreateDirectory(SettingsDirectory);
            if (Current.BackupBeforeChanges && File.Exists(SettingsFile) &&
                JsonSerializer.Serialize(Current) != JsonSerializer.Serialize(settings))
                WriteBackupFile(Path.Combine(SettingsDirectory, "settings.previous.json"), Current);
            WriteBackupFile(SettingsFile, settings);
            Current = settings;
        }

        private static void WriteBackupFile(string path, AppSettings settings)
        {
            string destination = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".wintweaker-{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew))
                    JsonSerializer.Serialize(stream, settings, new JsonSerializerOptions { WriteIndented = true });
                File.Move(temporary, destination, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        // =========================================================
        // RESET
        // =========================================================

        public static void ResetToDefaults()
        {
            ApplyBackup(new AppSettings());
        }

        // =========================================================
        // SETTINGS PATH
        // =========================================================

        public static string GetSettingsFilePath()
        {
            return SettingsFile;
        }
    }
}