using System;
using System.Windows.Forms;

namespace ScreenDimmer;

public static class SleepTimerManager
{
    private static System.Windows.Forms.Timer? _timer;
    private static DateTime _targetTime = DateTime.MinValue;
    private static BrightnessController? _brightnessController;

    public static bool IsRunning => _timer != null && _timer.Enabled;
    public static int RemainingMinutes => IsRunning ? (int)Math.Ceiling((_targetTime - DateTime.UtcNow).TotalMinutes) : 0;

    public static event Action? TimerTick;
    public static event Action<bool>? StateChanged;

    public static void Start(int minutes, BrightnessController brightnessController)
    {
        Stop();

        _brightnessController = brightnessController;
        _targetTime = DateTime.UtcNow.AddMinutes(minutes);

        _timer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };

        _timer.Tick += (s, e) =>
        {
            if (DateTime.UtcNow >= _targetTime)
            {
                Stop();
                if (_brightnessController != null)
                {
                    BlackoutManager.Activate(_brightnessController);
                }
            }
            else
            {
                TimerTick?.Invoke();
            }
        };

        _timer.Start();
        StateChanged?.Invoke(true);
    }

    public static void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Dispose();
            _timer = null;
            StateChanged?.Invoke(false);
        }
    }
}
