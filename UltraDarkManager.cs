using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenDimmer;

internal class UltraDarkOverlayForm : Form
{
    public UltraDarkOverlayForm(Rectangle bounds)
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Opacity = SettingsManager.Current.FadeAnimation ? 0.0 : (SettingsManager.Current.UltraDarkTintPercentage / 100.0);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // 0x20 = WS_EX_TRANSPARENT (все клики мыши и жесты проходят сквозь форму)
            // 0x80 = WS_EX_TOOLWINDOW (не отображается в Alt+Tab)
            // 0x08 = WS_EX_TOPMOST (поверх всех окон)
            // 0x08000000 = WS_EX_NOACTIVATE (не перехватывает фокус ввода)
            cp.ExStyle |= 0x20 | 0x80 | 0x08 | 0x08000000;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void ApplyExactBounds(Rectangle r)
    {
        NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HWND_TOPMOST,
            r.Left,
            r.Top,
            r.Width,
            r.Height,
            NativeMethods.SWP_SHOWWINDOW | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED
        );
    }

    public async Task FadeInAsync(double targetOpacity)
    {
        if (!SettingsManager.Current.FadeAnimation)
        {
            Opacity = targetOpacity;
            return;
        }

        for (double o = 0.05; o <= targetOpacity; o += 0.08)
        {
            Opacity = Math.Min(targetOpacity, o);
            await Task.Delay(15);
        }
        Opacity = targetOpacity;
    }

    public async Task FadeOutAsync()
    {
        if (!SettingsManager.Current.FadeAnimation)
        {
            Opacity = 0.0;
            return;
        }

        for (double o = Opacity; o >= 0.0; o -= 0.1)
        {
            Opacity = Math.Max(0.0, o);
            await Task.Delay(15);
        }
        Opacity = 0.0;
    }
}

public static class UltraDarkManager
{
    private static readonly List<UltraDarkOverlayForm> _activeForms = new();
    private static readonly Dictionary<string, uint> _originalContrastMap = new();
    private static readonly object _lock = new();

    public static bool IsActive { get; private set; }
    public static event Action<bool>? StateChanged;

    public static async void Activate(BrightnessController brightnessController)
    {
        List<UltraDarkOverlayForm> formsToFade;
        double targetOpacity = Math.Clamp(SettingsManager.Current.UltraDarkTintPercentage / 100.0, 0.10, 0.95);

        lock (_lock)
        {
            if (IsActive) return;
            IsActive = true;

            // 1. Аппаратная яркость всех мониторов (DDC/CI + WMI) в 0%
            brightnessController.Dim(0);

            // 2. Аппаратная контрастность DDC/CI на внешних мониторах -> 15% (подавление белого)
            LowerHardwareContrast();

            // 3. Создание сквозных кликабельных светофильтров поверх всех экранов
            _activeForms.Clear();
            foreach (var screen in Screen.AllScreens)
            {
                var form = new UltraDarkOverlayForm(screen.Bounds);
                form.Show();
                form.ApplyExactBounds(screen.Bounds);
                _activeForms.Add(form);
            }

            formsToFade = new List<UltraDarkOverlayForm>(_activeForms);
            StateChanged?.Invoke(true);
        }

        // Плавный фейд-ин
        foreach (var form in formsToFade)
        {
            try
            {
                await form.FadeInAsync(targetOpacity);
            }
            catch { }
        }
    }

    public static async void Deactivate(BrightnessController brightnessController, bool restoreBrightness = true)
    {
        List<UltraDarkOverlayForm> formsToClose;

        lock (_lock)
        {
            if (!IsActive) return;
            IsActive = false;

            // 1. Восстановление аппаратной контрастности DDC/CI
            RestoreHardwareContrast();

            formsToClose = new List<UltraDarkOverlayForm>(_activeForms);
            _activeForms.Clear();
            StateChanged?.Invoke(false);
        }

        // 2. Восстановление сохраненной нормальной яркости дисплеев при необходимости
        if (restoreBrightness)
        {
            brightnessController.Restore();
        }

        // Плавный фейд-аут и очистка окон
        foreach (var form in formsToClose)
        {
            try
            {
                await form.FadeOutAsync();
                form.Close();
                form.Dispose();
            }
            catch { }
        }
    }

    public static void Toggle(BrightnessController brightnessController)
    {
        if (IsActive)
        {
            Deactivate(brightnessController);
        }
        else
        {
            Activate(brightnessController);
        }
    }

    public static void UpdateCurrentOpacity(int percentage)
    {
        if (!IsActive) return;
        double target = Math.Clamp(percentage / 100.0, 0.10, 0.95);
        lock (_lock)
        {
            foreach (var form in _activeForms)
            {
                try
                {
                    form.Opacity = target;
                }
                catch { }
            }
        }
    }

    private static void LowerHardwareContrast()
    {
        var ddcMonitors = GetDdcPhysicalMonitors();
        try
        {
            for (int i = 0; i < ddcMonitors.Count; i++)
            {
                var mon = ddcMonitors[i];
                string key = $"DDC_{i}_{mon.szPhysicalMonitorDescription.Trim()}";
                if (NativeMethods.GetMonitorContrast(mon.hPhysicalMonitor, out uint min, out uint cur, out uint max))
                {
                    if (!_originalContrastMap.ContainsKey(key))
                    {
                        _originalContrastMap[key] = cur;
                    }
                    uint target = Math.Max(min, 15);
                    NativeMethods.SetMonitorContrast(mon.hPhysicalMonitor, target);
                }
            }
        }
        catch { }
        finally
        {
            DestroyDdcMonitors(ddcMonitors);
        }
    }

    private static void RestoreHardwareContrast()
    {
        var ddcMonitors = GetDdcPhysicalMonitors();
        try
        {
            for (int i = 0; i < ddcMonitors.Count; i++)
            {
                var mon = ddcMonitors[i];
                string key = $"DDC_{i}_{mon.szPhysicalMonitorDescription.Trim()}";
                if (_originalContrastMap.TryGetValue(key, out uint orig))
                {
                    NativeMethods.SetMonitorContrast(mon.hPhysicalMonitor, orig);
                }
            }
        }
        catch { }
        finally
        {
            DestroyDdcMonitors(ddcMonitors);
        }
    }

    private static List<NativeMethods.PHYSICAL_MONITOR> GetDdcPhysicalMonitors()
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

    private static void DestroyDdcMonitors(List<NativeMethods.PHYSICAL_MONITOR> monitors)
    {
        if (monitors.Count > 0)
        {
            NativeMethods.DestroyPhysicalMonitors((uint)monitors.Count, monitors.ToArray());
        }
    }
}
