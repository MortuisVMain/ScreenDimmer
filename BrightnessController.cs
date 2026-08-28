using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Windows.Forms;

namespace ScreenDimmer;

public class DisplayMonitorInfo
{
    public string Id { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public bool IsLaptopInternal { get; set; }
    public bool IsPrimary { get; set; }
    public int CurrentBrightness { get; set; } = 80;
    public int NormalBrightness { get; set; } = 80;
    public int DimBrightness { get; set; } = 0;
    public uint MinBrightness { get; set; } = 0;
    public uint MaxBrightness { get; set; } = 100;
    public bool SupportsBrightnessControl { get; set; } = true;
}

public class MonitorBrightnessState
{
    public string Id { get; set; } = string.Empty;
    public int OriginalBrightness { get; set; } = 80;
}

public class AppStateData
{
    public bool IsDimmed { get; set; }
    public List<MonitorBrightnessState> Monitors { get; set; } = new();
}

public class BrightnessController
{
    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenDimmer",
        "state.json"
    );

    private readonly object _lock = new();
    private readonly Dictionary<string, int> _originalBrightnessMap = new();

    public bool IsDimmed { get; private set; }

    public event Action<bool>? StateChanged;
    public event Action? MonitorsUpdated;

    public BrightnessController()
    {
        EnsureStateDir();
        CheckAndRecoverFromPreviousCrash();
        InitMonitorsAndSettings();
    }

    private void EnsureStateDir()
    {
        try
        {
            var dir = Path.GetDirectoryName(StateFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        catch { }
    }

    public void InitMonitorsAndSettings()
    {
        lock (_lock)
        {
            var list = GetConnectedMonitors();
            foreach (var mon in list)
            {
                if (!_originalBrightnessMap.ContainsKey(mon.Id))
                {
                    _originalBrightnessMap[mon.Id] = mon.NormalBrightness;
                }
            }
        }
    }

    private void CheckAndRecoverFromPreviousCrash()
    {
        try
        {
            if (File.Exists(StateFilePath))
            {
                string json = File.ReadAllText(StateFilePath);
                var state = JsonSerializer.Deserialize<AppStateData>(json);
                if (state != null && state.IsDimmed && state.Monitors.Count > 0)
                {
                    foreach (var m in state.Monitors)
                    {
                        _originalBrightnessMap[m.Id] = m.OriginalBrightness;
                    }
                    IsDimmed = true;
                    Restore();
                }
            }
        }
        catch { }
    }

    private void SaveStateToDisk()
    {
        try
        {
            var data = new AppStateData
            {
                IsDimmed = IsDimmed,
                Monitors = new List<MonitorBrightnessState>()
            };

            foreach (var kvp in _originalBrightnessMap)
            {
                data.Monitors.Add(new MonitorBrightnessState
                {
                    Id = kvp.Key,
                    OriginalBrightness = kvp.Value
                });
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StateFilePath, json);
        }
        catch { }
    }

    public List<DisplayMonitorInfo> GetConnectedMonitors()
    {
        var result = new List<DisplayMonitorInfo>();
        var screens = Screen.AllScreens;

        // 1. DDC/CI Monitors (Внешние мониторы)
        var ddcMonitors = GetDdcPhysicalMonitors();
        try
        {
            for (int i = 0; i < ddcMonitors.Count; i++)
            {
                var mon = ddcMonitors[i];
                if (NativeMethods.GetMonitorBrightness(mon.hPhysicalMonitor, out uint min, out uint cur, out uint max))
                {
                    string id = $"DDC_{i}_{mon.szPhysicalMonitorDescription.Trim()}";
                    string friendlyName = (i == 0 && ddcMonitors.Count == 1) 
                        ? $"🖥️ Внешний монитор ({mon.szPhysicalMonitorDescription.Trim()})" 
                        : $"🖥️ Внешний монитор {i + 1} ({mon.szPhysicalMonitorDescription.Trim()})";

                    var setting = SettingsManager.GetOrCreateMonitorSetting(id, friendlyName, (int)cur);
                    result.Add(new DisplayMonitorInfo
                    {
                        Id = id,
                        FriendlyName = friendlyName,
                        DeviceName = (i < screens.Length) ? screens[i].DeviceName : $"Display_{i + 1}",
                        IsLaptopInternal = false,
                        IsPrimary = (i < screens.Length) && screens[i].Primary,
                        CurrentBrightness = (int)cur,
                        NormalBrightness = setting.NormalBrightness,
                        DimBrightness = setting.DimBrightness,
                        MinBrightness = min,
                        MaxBrightness = max,
                        SupportsBrightnessControl = true
                    });
                }
            }
        }
        finally
        {
            DestroyDdcMonitors(ddcMonitors);
        }

        // 2. WMI Monitors (Встроенный экран ноутбука)
        var wmiMonitors = GetWmiBrightnessInstances();
        for (int i = 0; i < wmiMonitors.Count; i++)
        {
            var mon = wmiMonitors[i];
            string id = $"WMI_{mon.InstanceName}";
            string friendlyName = wmiMonitors.Count == 1 
                ? "💻 Встроенный дисплей ноутбука" 
                : $"💻 Экран ноутбука {i + 1}";

            var setting = SettingsManager.GetOrCreateMonitorSetting(id, friendlyName, mon.Brightness);
            result.Add(new DisplayMonitorInfo
            {
                Id = id,
                FriendlyName = friendlyName,
                DeviceName = "Laptop_Internal",
                IsLaptopInternal = true,
                IsPrimary = false,
                CurrentBrightness = mon.Brightness,
                NormalBrightness = setting.NormalBrightness,
                DimBrightness = setting.DimBrightness,
                MinBrightness = 0,
                MaxBrightness = 100,
                SupportsBrightnessControl = true
            });
        }

        // Fallback если ни DDC, ни WMI не дали результатов
        if (result.Count == 0)
        {
            for (int i = 0; i < screens.Length; i++)
            {
                var s = screens[i];
                string id = $"GENERIC_{s.DeviceName}";
                string friendlyName = s.Primary ? $"💻 Основной дисплей ({s.Bounds.Width}x{s.Bounds.Height})" : $"🖥️ Дисплей {i + 1} ({s.Bounds.Width}x{s.Bounds.Height})";
                var setting = SettingsManager.GetOrCreateMonitorSetting(id, friendlyName, SettingsManager.Current.DefaultNormalBrightness);
                result.Add(new DisplayMonitorInfo
                {
                    Id = id,
                    FriendlyName = friendlyName,
                    DeviceName = s.DeviceName,
                    IsLaptopInternal = !s.Primary,
                    IsPrimary = s.Primary,
                    CurrentBrightness = setting.NormalBrightness,
                    NormalBrightness = setting.NormalBrightness,
                    DimBrightness = setting.DimBrightness,
                    MinBrightness = 0,
                    MaxBrightness = 100,
                    SupportsBrightnessControl = true
                });
            }
        }

        return result;
    }

    public void SetMonitorBrightnessById(string id, int targetBrightness)
    {
        lock (_lock)
        {
            int level = Math.Clamp(targetBrightness, 0, 100);

            if (id.StartsWith("DDC_"))
            {
                var ddcMonitors = GetDdcPhysicalMonitors();
                try
                {
                    for (int i = 0; i < ddcMonitors.Count; i++)
                    {
                        var mon = ddcMonitors[i];
                        string monId = $"DDC_{i}_{mon.szPhysicalMonitorDescription.Trim()}";
                        if (monId == id)
                        {
                            if (NativeMethods.GetMonitorBrightness(mon.hPhysicalMonitor, out uint min, out _, out uint max))
                            {
                                uint target = (uint)Math.Clamp(level, (int)min, (int)max);
                                NativeMethods.SetMonitorBrightness(mon.hPhysicalMonitor, target);
                            }
                        }
                    }
                }
                finally
                {
                    DestroyDdcMonitors(ddcMonitors);
                }
            }
            else if (id.StartsWith("WMI_"))
            {
                string instanceName = id.Substring(4);
                SetWmiBrightness(level, instanceName);
            }

            if (!IsDimmed)
            {
                _originalBrightnessMap[id] = level;
                SettingsManager.UpdateMonitorSetting(id, normalBrightness: level, lastBrightness: level);
            }

            SaveStateToDisk();
        }
        MonitorsUpdated?.Invoke();
    }

    public void SetMonitorNormalBrightness(string id, int normalBrightness)
    {
        int level = Math.Clamp(normalBrightness, 0, 100);
        lock (_lock)
        {
            _originalBrightnessMap[id] = level;
            SettingsManager.UpdateMonitorSetting(id, normalBrightness: level);
        }
        if (!IsDimmed)
        {
            SetMonitorBrightnessById(id, level);
        }
    }

    public void SetMonitorDimBrightness(string id, int dimBrightness)
    {
        int level = Math.Clamp(dimBrightness, 0, 100);
        lock (_lock)
        {
            SettingsManager.UpdateMonitorSetting(id, dimBrightness: level);
        }
        if (IsDimmed)
        {
            SetMonitorBrightnessById(id, level);
        }
    }

    public void Dim(int? targetLevel = null)
    {
        lock (_lock)
        {
            var monitors = GetConnectedMonitors();

            // 1. DDC/CI (Внешние мониторы)
            var ddcMonitors = GetDdcPhysicalMonitors();
            try
            {
                for (int i = 0; i < ddcMonitors.Count; i++)
                {
                    var mon = ddcMonitors[i];
                    string key = $"DDC_{i}_{mon.szPhysicalMonitorDescription.Trim()}";
                    var monInfo = monitors.Find(m => m.Id == key);

                    int level = targetLevel ?? monInfo?.DimBrightness ?? SettingsManager.Current.DimPercentage;
                    level = Math.Clamp(level, 0, 100);

                    if (NativeMethods.GetMonitorBrightness(mon.hPhysicalMonitor, out uint min, out uint cur, out uint max))
                    {
                        if (!_originalBrightnessMap.ContainsKey(key) || !IsDimmed)
                        {
                            int recordedBri = (int)cur;
                            _originalBrightnessMap[key] = recordedBri;
                            SettingsManager.UpdateMonitorSetting(key, normalBrightness: recordedBri);
                        }

                        uint newBri = (uint)Math.Clamp(level, (int)min, (int)max);
                        NativeMethods.SetMonitorBrightness(mon.hPhysicalMonitor, newBri);
                    }
                }
            }
            finally
            {
                DestroyDdcMonitors(ddcMonitors);
            }

            // 2. WMI (Встроенный экран ноутбука)
            var wmiMonitors = GetWmiBrightnessInstances();
            foreach (var mon in wmiMonitors)
            {
                string key = $"WMI_{mon.InstanceName}";
                var monInfo = monitors.Find(m => m.Id == key);
                int level = targetLevel ?? monInfo?.DimBrightness ?? SettingsManager.Current.DimPercentage;
                level = Math.Clamp(level, 0, 100);

                if (!_originalBrightnessMap.ContainsKey(key) || !IsDimmed)
                {
                    int recordedBri = mon.Brightness;
                    _originalBrightnessMap[key] = recordedBri;
                    SettingsManager.UpdateMonitorSetting(key, normalBrightness: recordedBri);
                }

                SetWmiBrightness(level, mon.InstanceName);
            }

            if (wmiMonitors.Count == 0 && ddcMonitors.Count == 0)
            {
                int level = targetLevel ?? SettingsManager.Current.DimPercentage;
                SetWmiBrightness(level);
            }

            IsDimmed = true;
            SaveStateToDisk();
        }

        StateChanged?.Invoke(true);
        MonitorsUpdated?.Invoke();
    }

    public void Restore()
    {
        lock (_lock)
        {
            var monitors = GetConnectedMonitors();

            // 1. DDC/CI (Внешние мониторы)
            var ddcMonitors = GetDdcPhysicalMonitors();
            try
            {
                for (int i = 0; i < ddcMonitors.Count; i++)
                {
                    var mon = ddcMonitors[i];
                    string key = $"DDC_{i}_{mon.szPhysicalMonitorDescription.Trim()}";
                    var monInfo = monitors.Find(m => m.Id == key);

                    int target = _originalBrightnessMap.TryGetValue(key, out int val) 
                        ? val 
                        : (monInfo?.NormalBrightness ?? SettingsManager.Current.DefaultNormalBrightness);
                    
                    NativeMethods.SetMonitorBrightness(mon.hPhysicalMonitor, (uint)Math.Clamp(target, 0, 100));
                }
            }
            finally
            {
                DestroyDdcMonitors(ddcMonitors);
            }

            // 2. WMI (Экран ноутбука)
            var wmiMonitors = GetWmiBrightnessInstances();
            foreach (var mon in wmiMonitors)
            {
                string key = $"WMI_{mon.InstanceName}";
                var monInfo = monitors.Find(m => m.Id == key);

                int target = _originalBrightnessMap.TryGetValue(key, out int val)
                    ? val
                    : (monInfo?.NormalBrightness ?? SettingsManager.Current.DefaultNormalBrightness);

                SetWmiBrightness(target, mon.InstanceName);
            }

            if (wmiMonitors.Count == 0)
            {
                int defaultTarget = SettingsManager.Current.DefaultNormalBrightness;
                SetWmiBrightness(defaultTarget);
            }

            IsDimmed = false;
            SaveStateToDisk();
        }

        StateChanged?.Invoke(false);
        MonitorsUpdated?.Invoke();
    }

    public int AdjustBrightness(int delta)
    {
        int newBri;
        lock (_lock)
        {
            int current = GetAverageBrightness();
            newBri = Math.Clamp(current + delta, 0, 100);

            IsDimmed = false;

            var ddcMonitors = GetDdcPhysicalMonitors();
            try
            {
                for (int i = 0; i < ddcMonitors.Count; i++)
                {
                    var mon = ddcMonitors[i];
                    string key = $"DDC_{i}_{mon.szPhysicalMonitorDescription.Trim()}";
                    _originalBrightnessMap[key] = newBri;
                    SettingsManager.UpdateMonitorSetting(key, normalBrightness: newBri, lastBrightness: newBri);

                    if (NativeMethods.GetMonitorBrightness(mon.hPhysicalMonitor, out uint min, out _, out uint max))
                    {
                        uint target = (uint)Math.Clamp(newBri, (int)min, (int)max);
                        NativeMethods.SetMonitorBrightness(mon.hPhysicalMonitor, target);
                    }
                }
            }
            finally
            {
                DestroyDdcMonitors(ddcMonitors);
            }

            var wmiMonitors = GetWmiBrightnessInstances();
            foreach (var mon in wmiMonitors)
            {
                string key = $"WMI_{mon.InstanceName}";
                _originalBrightnessMap[key] = newBri;
                SettingsManager.UpdateMonitorSetting(key, normalBrightness: newBri, lastBrightness: newBri);
                SetWmiBrightness(newBri, mon.InstanceName);
            }

            if (wmiMonitors.Count == 0)
            {
                SetWmiBrightness(newBri);
            }

            SaveStateToDisk();
        }

        StateChanged?.Invoke(IsDimmed);
        MonitorsUpdated?.Invoke();
        return newBri;
    }

    public int GetAverageBrightness()
    {
        try
        {
            var monitors = GetConnectedMonitors();
            if (monitors.Count > 0)
            {
                int sum = 0;
                int count = 0;
                foreach (var m in monitors)
                {
                    sum += m.CurrentBrightness;
                    count++;
                }
                if (count > 0) return sum / count;
            }
        }
        catch { }

        return 80;
    }

    private List<NativeMethods.PHYSICAL_MONITOR> GetDdcPhysicalMonitors()
    {
        var list = new List<NativeMethods.PHYSICAL_MONITOR>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
        {
            if (NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) && count > 0)
            {
                var physMons = new NativeMethods.PHYSICAL_MONITOR[count];
                if (NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMon, count, physMons))
                {
                    list.AddRange(physMons);
                }
            }
            return true;
        }, IntPtr.Zero);

        return list;
    }

    private void DestroyDdcMonitors(List<NativeMethods.PHYSICAL_MONITOR> monitors)
    {
        if (monitors.Count > 0)
        {
            NativeMethods.DestroyPhysicalMonitors((uint)monitors.Count, monitors.ToArray());
        }
    }

    private class WmiInstanceInfo
    {
        public string InstanceName { get; set; } = string.Empty;
        public int Brightness { get; set; } = 80;
    }

    private List<WmiInstanceInfo> GetWmiBrightnessInstances()
    {
        var result = new List<WmiInstanceInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness WHERE Active = True");
            using var coll = searcher.Get();
            foreach (ManagementObject obj in coll)
            {
                try
                {
                    string name = obj["InstanceName"]?.ToString() ?? "Default";
                    int bri = 80;
                    var rawVal = obj["CurrentBrightness"];
                    if (rawVal != null)
                    {
                        try { bri = Convert.ToInt32(rawVal); } catch { }
                    }

                    result.Add(new WmiInstanceInfo { InstanceName = name, Brightness = bri });
                }
                finally
                {
                    obj.Dispose();
                }
            }
        }
        catch { }

        // Если активных WMI-инстансов не нашлось (например запрос без фильтрации Active)
        if (result.Count == 0)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness");
                using var coll = searcher.Get();
                foreach (ManagementObject obj in coll)
                {
                    try
                    {
                        string name = obj["InstanceName"]?.ToString() ?? "Default";
                        int bri = 80;
                        var rawVal = obj["CurrentBrightness"];
                        if (rawVal != null)
                        {
                            try { bri = Convert.ToInt32(rawVal); } catch { }
                        }

                        result.Add(new WmiInstanceInfo { InstanceName = name, Brightness = bri });
                    }
                    finally
                    {
                        obj.Dispose();
                    }
                }
            }
            catch { }
        }

        return result;
    }

    private void SetWmiBrightness(int targetBrightness, string? targetInstanceName = null)
    {
        int clamped = Math.Clamp(targetBrightness, 0, 100);
        bool success = false;

        // 1. Основной путь: Прямой вызов WMI с контролем COM-дескрипторов
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = True");
            using var coll = searcher.Get();
            foreach (ManagementObject obj in coll)
            {
                try
                {
                    string instName = obj["InstanceName"]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(targetInstanceName) || instName == targetInstanceName)
                    {
                        using var inParams = obj.GetMethodParameters("WmiSetBrightness");
                        inParams["Timeout"] = (uint)1;
                        inParams["Brightness"] = (byte)clamped;
                        using var outParams = obj.InvokeMethod("WmiSetBrightness", inParams, null);
                        success = true;
                    }
                }
                catch
                {
                    // Изолированная ошибка отдельного монитора не прерывает остальные
                }
                finally
                {
                    obj.Dispose();
                }
            }
        }
        catch { }

        // Если не сработало с Active=True, пробуем общий запрос
        if (!success)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
                using var coll = searcher.Get();
                foreach (ManagementObject obj in coll)
                {
                    try
                    {
                        string instName = obj["InstanceName"]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(targetInstanceName) || instName == targetInstanceName)
                        {
                            using var inParams = obj.GetMethodParameters("WmiSetBrightness");
                            inParams["Timeout"] = (uint)1;
                            inParams["Brightness"] = (byte)clamped;
                            using var outParams = obj.InvokeMethod("WmiSetBrightness", inParams, null);
                            success = true;
                        }
                    }
                    catch { }
                    finally
                    {
                        obj.Dispose();
                    }
                }
            }
            catch { }
        }

        // 2. Резервный путь: Быстрый CIM PowerShell вызов
        if (!success)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"Get-CimInstance -Namespace root/wmi -ClassName WmiMonitorBrightnessMethods | ForEach-Object {{ Invoke-CimMethod -InputObject $_ -MethodName WmiSetBrightness -Arguments @{{Timeout=[uint32]1; Brightness=[byte]{clamped}}} }}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(800);
            }
            catch { }
        }
    }
}
