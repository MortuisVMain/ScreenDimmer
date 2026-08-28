using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ScreenDimmer;

public class MonitorSetting
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int NormalBrightness { get; set; } = 80;
    public int DimBrightness { get; set; } = 0;
    public int LastBrightness { get; set; } = 80;
}

public class AppSettings
{
    public int DimPercentage { get; set; } = 0;
    public int DefaultNormalBrightness { get; set; } = 80;
    public bool FadeAnimation { get; set; } = true;
    public bool MuteAudioInBlackout { get; set; } = false;
    public bool ShowBrightnessHud { get; set; } = true;
    public Dictionary<string, MonitorSetting> MonitorSettings { get; set; } = new();
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

    public static MonitorSetting GetOrCreateMonitorSetting(string id, string name, int detectedBrightness)
    {
        if (!_settings.MonitorSettings.TryGetValue(id, out var setting))
        {
            int initBri = (detectedBrightness >= 0 && detectedBrightness <= 100) 
                ? detectedBrightness 
                : _settings.DefaultNormalBrightness;

            setting = new MonitorSetting
            {
                Id = id,
                Name = name,
                NormalBrightness = Math.Clamp(initBri, 0, 100),
                DimBrightness = _settings.DimPercentage,
                LastBrightness = Math.Clamp(initBri, 0, 100)
            };
            _settings.MonitorSettings[id] = setting;
            Save();
        }
        return setting;
    }

    public static void UpdateMonitorSetting(string id, int? normalBrightness = null, int? dimBrightness = null, int? lastBrightness = null)
    {
        if (_settings.MonitorSettings.TryGetValue(id, out var setting))
        {
            if (normalBrightness.HasValue) setting.NormalBrightness = Math.Clamp(normalBrightness.Value, 0, 100);
            if (dimBrightness.HasValue) setting.DimBrightness = Math.Clamp(dimBrightness.Value, 0, 100);
            if (lastBrightness.HasValue) setting.LastBrightness = Math.Clamp(lastBrightness.Value, 0, 100);
            Save();
        }
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
