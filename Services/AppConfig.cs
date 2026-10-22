using System.IO;
using System.Text.Json;

namespace FuturesTicker.Services;

/// <summary>Persisted user configuration.</summary>
public class AppConfig
{
    /// <summary>进程内共享的配置单例（悬浮窗与主界面共用同一实例）。</summary>
    public static AppConfig Current { get; } = ConfigStore.Load();

    public int RefreshIntervalSeconds { get; set; } = 3;
    public bool AutoStart { get; set; } = false;
    public double DimOpacity { get; set; } = 0.45;
    public double FontSize { get; set; } = 11;
    public bool Topmost { get; set; } = true;
    public bool MouseThrough { get; set; } = true;
    public bool ShowColor { get; set; } = true;
    public bool PriceOnly { get; set; } = false;
    public bool ShowWidget { get; set; } = true;
    public List<string> Contracts { get; set; } = new() { "RB0", "AU0", "SC0", "M0" };
}

public static class ConfigStore
{
    public static string ConfigPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FuturesTicker", "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null && cfg.Contracts != null) return cfg;
            }
        }
        catch
        {
            // Fall through to defaults on any parse/IO error.
        }
        return new AppConfig();
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Best effort; do not crash the widget on a save failure.
        }
    }
}
