using System;
using System.IO;
using System.Text.Json;

namespace WinTweaker.Services
{
    public class AppSettings
    {
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

                JsonSerializerOptions options =
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                string json =
                    JsonSerializer.Serialize(
                        Current,
                        options);

                File.WriteAllText(
                    SettingsFile,
                    json);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // =========================================================
        // RESET
        // =========================================================

        public static void ResetToDefaults()
        {
            Current =
                new AppSettings();

            Save();
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