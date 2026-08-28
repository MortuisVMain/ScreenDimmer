using System;
using System.IO;
using System.Text.Json;

namespace ScreenDimmer;

public class AppSettings
{
    public int DimPercentage { get; set; } = 0;
    public int DefaultNormalBrightness { get; set; } = 80;
    public bool FadeAnimation { get; set; } = true;
    public bool MuteAudioInBlackout { get; set; } = false;
    public bool ShowBrightnessHud { get; set; } = true;
}

public static class SettingsManager
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenDimmer",
        "settings.json"
    );

    private static AppSettings _settings = new();

    public static AppSettings Current => _settings;

    static SettingsManager()
    {
        Load();
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                string json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;
                }
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsFilePath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }

    public static void SetDimPercentage(int percent)
    {
        _settings.DimPercentage = Math.Clamp(percent, 0, 100);
        Save();
    }

    public static void SetFadeAnimation(bool enable)
    {
        _settings.FadeAnimation = enable;
        Save();
    }

    public static void SetMuteAudioInBlackout(bool enable)
    {
        _settings.MuteAudioInBlackout = enable;
        Save();
    }

    public static void SetShowBrightnessHud(bool enable)
    {
        _settings.ShowBrightnessHud = enable;
        Save();
    }
}
