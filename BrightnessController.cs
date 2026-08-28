using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.Json;

namespace ScreenDimmer;

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

    public BrightnessController()
    {
        EnsureStateDir();
        CheckAndRecoverFromPreviousCrash();
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
                        if (m.OriginalBrightness > 15)
                        {
                            _originalBrightnessMap[m.Id] = m.OriginalBrightness;
                        }
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

    public void Dim(int? targetLevel = null)
    {
        lock (_lock)
        {
            int level = targetLevel ?? SettingsManager.Current.DimPercentage;
            level = Math.Clamp(level, 0, 100);

            // 1. DDC/CI (Внешние мониторы)
            var ddcMonitors = GetDdcPhysicalMonitors();
            try
            {
                foreach (var mon in ddcMonitors)
                {
                    string key = $"DDC_{mon.hPhysicalMonitor}";
                    if (NativeMethods.GetMonitorBrightness(mon.hPhysicalMonitor, out uint min, out uint cur, out uint max))
                    {
                        if (!_originalBrightnessMap.ContainsKey(key) || !IsDimmed)
                        {
                            _originalBrightnessMap[key] = (cur > 15) ? (int)cur : SettingsManager.Current.DefaultNormalBrightness;
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
                if (!_originalBrightnessMap.ContainsKey(key) || !IsDimmed)
                {
                    _originalBrightnessMap[key] = (mon.Brightness > 15) ? mon.Brightness : SettingsManager.Current.DefaultNormalBrightness;
                }
            }

            if (wmiMonitors.Count > 0)
            {
                SetWmiBrightness(level);
            }

            IsDimmed = true;
            SaveStateToDisk();
            StateChanged?.Invoke(IsDimmed);
        }
    }

    public void Restore()
    {
        lock (_lock)
        {
            // 1. DDC/CI (Внешние мониторы)
            var ddcMonitors = GetDdcPhysicalMonitors();
            try
            {
                foreach (var mon in ddcMonitors)
                {
                    string key = $"DDC_{mon.hPhysicalMonitor}";
                    int target = _originalBrightnessMap.TryGetValue(key, out int val) && val > 15 
                        ? val 
                        : SettingsManager.Current.DefaultNormalBrightness;
                    
                    NativeMethods.SetMonitorBrightness(mon.hPhysicalMonitor, (uint)target);
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
                int target = _originalBrightnessMap.TryGetValue(key, out int val) && val > 15
                    ? val
                    : SettingsManager.Current.DefaultNormalBrightness;

                SetWmiBrightness(target);
            }

            if (wmiMonitors.Count == 0)
            {
                int defaultTarget = SettingsManager.Current.DefaultNormalBrightness;
                SetWmiBrightness(defaultTarget);
            }

            IsDimmed = false;
            SaveStateToDisk();
            StateChanged?.Invoke(IsDimmed);
        }
    }

    public int AdjustBrightness(int delta)
    {
        lock (_lock)
        {
            int current = GetAverageBrightness();
            int newBri = Math.Clamp(current + delta, 0, 100);

            IsDimmed = false;

            var ddcMonitors = GetDdcPhysicalMonitors();
            try
            {
                foreach (var mon in ddcMonitors)
                {
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

            SetWmiBrightness(newBri);

            SaveStateToDisk();
            StateChanged?.Invoke(IsDimmed);
            return newBri;
        }
    }

    public int GetAverageBrightness()
    {
        try
        {
            var wmi = GetWmiBrightnessInstances();
            if (wmi.Count > 0)
            {
                return wmi[0].Brightness;
            }

            var ddcMonitors = GetDdcPhysicalMonitors();
            try
            {
                if (ddcMonitors.Count > 0)
                {
                    if (NativeMethods.GetMonitorBrightness(ddcMonitors[0].hPhysicalMonitor, out _, out uint cur, out _))
                    {
                        return (int)cur;
                    }
                }
            }
            finally
            {
                DestroyDdcMonitors(ddcMonitors);
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
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject obj in searcher.Get())
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
        }
        catch { }
        return result;
    }

    private void SetWmiBrightness(int targetBrightness)
    {
        int clamped = Math.Clamp(targetBrightness, 0, 100);
        bool success = false;

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject obj in searcher.Get())
            {
                var inParams = obj.GetMethodParameters("WmiSetBrightness");
                inParams["Timeout"] = (uint)1;
                inParams["Brightness"] = (byte)clamped;
                obj.InvokeMethod("WmiSetBrightness", inParams, null);
                success = true;
            }
        }
        catch { }

        if (!success)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"(Get-WmiObject -Namespace root/wmi -Class WmiMonitorBrightnessMethods).WmiSetBrightness(1, {clamped})\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(1000);
            }
            catch { }
        }
    }
}
