using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenDimmer;

public class SessionRecoveryManager : IDisposable
{
    private readonly BrightnessController _brightnessController;
    private bool _isDisposed;

    public SessionRecoveryManager(BrightnessController brightnessController)
    {
        _brightnessController = brightnessController;

        // Перехват событий выключения / перезагрузки Windows
        SystemEvents.SessionEnding += OnSessionEnding;
        SystemEvents.SessionEnded += OnSessionEnded;

        // Перехват событий завершения процесса приложения
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Application.ApplicationExit += OnApplicationExit;
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        BlackoutManager.Deactivate(_brightnessController);
        _brightnessController.Restore();
    }

    private void OnSessionEnded(object? sender, SessionEndedEventArgs e)
    {
        BlackoutManager.Deactivate(_brightnessController);
        _brightnessController.Restore();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        BlackoutManager.Deactivate(_brightnessController);
        _brightnessController.Restore();
    }

    private void OnApplicationExit(object? sender, EventArgs e)
    {
        BlackoutManager.Deactivate(_brightnessController);
        _brightnessController.Restore();
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            SystemEvents.SessionEnding -= OnSessionEnding;
            SystemEvents.SessionEnded -= OnSessionEnded;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            Application.ApplicationExit -= OnApplicationExit;

            BlackoutManager.Deactivate(_brightnessController);
            _brightnessController.Restore();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
