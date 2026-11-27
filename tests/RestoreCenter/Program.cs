using System.Text.Json.Nodes;
using WinTweaker.Services;

var dir = Directory.CreateTempSubdirectory("WinTweaker-restore-test-");
try
{
    var path = Path.Combine(dir.FullName, "backup.json");
    SettingsService.Current.AccentColor = "Purple";
    SettingsService.Current.ShowNotifications = false;
    SettingsService.ExportBackup(path);
    var valid = File.ReadAllText(path);
    var imported = SettingsService.ReadBackup(path);
    if (imported.AccentColor != "Purple" || imported.ShowNotifications || ReferenceEquals(imported, SettingsService.Current))
        throw new Exception("Backup did not round-trip as an independent candidate.");

    var badAccent = JsonNode.Parse(valid)!.AsObject();
    badAccent["AccentColor"] = "Bogus";
    foreach (var invalid in new[] { "{}", "null", "{", badAccent.ToJsonString(), valid.Insert(1, "\"DarkTheme\":false,") })
    {
        File.WriteAllText(path, invalid);
        try { SettingsService.ReadBackup(path); }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException) { continue; }
        throw new Exception("Invalid backup was accepted.");
    }
    if (SettingsService.Current.AccentColor != "Purple" || SettingsService.Current.ShowNotifications)
        throw new Exception("Reading an invalid backup changed current settings.");
    Console.WriteLine("PASS settings backup round-trip and invalid-input rejection");
}
finally { dir.Delete(recursive: true); }
