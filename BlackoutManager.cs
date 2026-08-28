using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenDimmer;

internal class BlackoutForm : Form
{
    public BlackoutForm(Rectangle bounds)
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Opacity = SettingsManager.Current.FadeAnimation ? 0.0 : 1.0;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x08 | 0x08000000;
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

    public async Task FadeInAsync()
    {
        if (!SettingsManager.Current.FadeAnimation)
        {
            Opacity = 1.0;
            return;
        }

        for (double o = 0.1; o <= 1.0; o += 0.15)
        {
            Opacity = o;
            await Task.Delay(15);
        }
        Opacity = 1.0;
    }

    public async Task FadeOutAsync()
    {
        if (!SettingsManager.Current.FadeAnimation)
        {
            Opacity = 0.0;
            return;
        }

        for (double o = 0.85; o >= 0.0; o -= 0.2)
        {
            Opacity = o;
            await Task.Delay(15);
        }
        Opacity = 0.0;
    }
}

public static class BlackoutManager
{
    private static readonly List<BlackoutForm> _activeForms = new();
    private static readonly object _lock = new();
    private static bool _audioWasMutedByUs = false;

    public static bool IsActive { get; private set; }
    public static int? ActiveSoloScreenIndex { get; private set; }

    public static event Action<bool>? StateChanged;

    public static async void Activate(BrightnessController brightnessController, int? soloScreenIndex = null)
    {
        List<BlackoutForm> formsToFade;

        lock (_lock)
        {
            if (IsActive) return;

            IsActive = true;
            ActiveSoloScreenIndex = soloScreenIndex;

            // 1. Устанавливаем аппаратную яркость в 0%
            brightnessController.Dim(0);

            // 2. Блокируем спящий режим Windows (ПК продолжает работу на полной мощности)
            NativeMethods.SetThreadExecutionState(
                NativeMethods.ES_CONTINUOUS |
                NativeMethods.ES_SYSTEM_REQUIRED |
                NativeMethods.ES_AWAYMODE_REQUIRED
            );

            // 3. Умное отключение звука через CoreAudio API (только если звук был включен)
            if (SettingsManager.Current.MuteAudioInBlackout)
            {
                bool wasAlreadyMuted = AudioEndpointController.GetIsMuted();
                if (!wasAlreadyMuted)
                {
                    AudioEndpointController.SetMute(true);
                    _audioWasMutedByUs = true;
                }
            }

            // 4. Получаем список физических мониторов
            var monitorRects = GetPhysicalMonitorRectangles();
            if (monitorRects.Count == 0)
            {
                foreach (var screen in Screen.AllScreens)
                {
                    monitorRects.Add(screen.Bounds);
                }
            }

            // 5. Создаем индивидуальные черные полотна на каждый физический монитор
            if (soloScreenIndex == null)
            {
                if (monitorRects.Count > 0)
                {
                    foreach (var rect in monitorRects)
                    {
                        var form = new BlackoutForm(rect);
                        form.Show();
                        form.ApplyExactBounds(rect);
                        _activeForms.Add(form);
                    }
                }
                else
                {
                    int vLeft = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
                    int vTop = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
                    int vWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
                    int vHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

                    if (vWidth > 0 && vHeight > 0)
                    {
                        var masterForm = new BlackoutForm(new Rectangle(vLeft, vTop, vWidth, vHeight));
                        masterForm.Show();
                        masterForm.ApplyExactBounds(new Rectangle(vLeft, vTop, vWidth, vHeight));
                        _activeForms.Add(masterForm);
                    }
                }
            }
            else if (soloScreenIndex.Value >= 0 && soloScreenIndex.Value < monitorRects.Count)
            {
                var rect = monitorRects[soloScreenIndex.Value];
                var form = new BlackoutForm(rect);
                form.Show();
                form.ApplyExactBounds(rect);
                _activeForms.Add(form);
            }

            // 6. Скрываем курсор мыши
            try
            {
                Cursor.Hide();
            }
            catch { }

            formsToFade = new List<BlackoutForm>(_activeForms);
            StateChanged?.Invoke(true);
        }

        // Плавная кино-анимация появления
        foreach (var form in formsToFade)
        {
            try
            {
                await form.FadeInAsync();
            }
            catch { }
        }
    }

    public static async void Deactivate(BrightnessController brightnessController)
    {
        List<BlackoutForm> formsToClose;

        lock (_lock)
        {
            if (!IsActive) return;

            IsActive = false;
            ActiveSoloScreenIndex = null;

            // 1. Показываем курсор мыши
            try
            {
                Cursor.Show();
            }
            catch { }

            // 2. Возвращаем звук обратно (если мы его приглушали)
            if (_audioWasMutedByUs)
            {
                AudioEndpointController.SetMute(false);
                _audioWasMutedByUs = false;
            }

            formsToClose = new List<BlackoutForm>(_activeForms);
            _activeForms.Clear();

            // 3. Восстанавливаем исходную яркость
            brightnessController.Restore();

            // 4. Возвращаем стандартный режим управления питанием Windows
            NativeMethods.SetThreadExecutionState(NativeMethods.ES_CONTINUOUS);

            StateChanged?.Invoke(false);
        }

        // Плавное затухание перед закрытием окон
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

    public static void Toggle(BrightnessController brightnessController, int? soloScreenIndex = null)
    {
        if (IsActive)
        {
            Deactivate(brightnessController);
        }
        else
        {
            Activate(brightnessController, soloScreenIndex);
        }
    }

    private static List<Rectangle> GetPhysicalMonitorRectangles()
    {
        var list = new List<Rectangle>();

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref NativeMethods.RECT rc, IntPtr data) =>
        {
            var mi = new NativeMethods.MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));

            if (NativeMethods.GetMonitorInfo(hMon, ref mi))
            {
                list.Add(new Rectangle(
                    mi.rcMonitor.Left,
                    mi.rcMonitor.Top,
                    mi.rcMonitor.Width,
                    mi.rcMonitor.Height
                ));
            }
            return true;
        }, IntPtr.Zero);

        return list;
    }
}
