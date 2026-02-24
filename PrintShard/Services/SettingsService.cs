using System.IO;
using System.Text.Json;
using PrintShard.Models;

namespace PrintShard.Services;

public static class SettingsService
{
    private static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrintShard");

    private static string SettingsPath => Path.Combine(DataDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
        }
        catch { /* fall through to defaults */ }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* best-effort */ }
    }

    public static void AddRecentFile(AppSettings settings, string path)
    {
        settings.RecentFiles.Remove(path);
        settings.RecentFiles.Insert(0, path);
        while (settings.RecentFiles.Count > settings.MaxRecentFiles)
            settings.RecentFiles.RemoveAt(settings.RecentFiles.Count - 1);
        Save(settings);
    }
}
