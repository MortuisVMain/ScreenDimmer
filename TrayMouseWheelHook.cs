using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ScreenDimmer;

public class TrayMouseWheelHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private bool _isDisposed;

    public event Action<int>? WheelScrolled;

    public TrayMouseWheelHook()
    {
        _proc = HookCallback;
        _hookId = SetHook(_proc);
    }

    private IntPtr SetHook(NativeMethods.LowLevelMouseProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        string? moduleName = curModule?.ModuleName;
        IntPtr moduleHandle = NativeMethods.GetModuleHandle(moduleName);
        return NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, proc, moduleHandle, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == NativeMethods.WM_MOUSEWHEEL)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            short delta = (short)((hookStruct.mouseData >> 16) & 0xffff);

            if (IsCursorOverTaskbarOrTray(hookStruct.pt))
            {
                int step = delta > 0 ? 5 : -5;
                WheelScrolled?.Invoke(step);
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsCursorOverTaskbarOrTray(NativeMethods.POINT pt)
    {
        try
        {
            IntPtr hWnd = NativeMethods.WindowFromPoint(pt);
            if (hWnd != IntPtr.Zero)
            {
                var sb = new StringBuilder(256);
                NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
                string className = sb.ToString();

                if (className.Contains("Tray") ||
                    className.Contains("Shell_TrayWnd") ||
                    className.Contains("NotifyIconOverflowWindow") ||
                    className.Contains("ToolbarWindow32"))
                {
                    return true;
                }
            }

            foreach (var screen in Screen.AllScreens)
            {
                var b = screen.Bounds;
                var trayZone = new Rectangle(b.Right - 350, b.Bottom - 60, 350, 60);
                if (trayZone.Contains(pt.X, pt.Y))
                {
                    return true;
                }
            }
        }
        catch { }

        return false;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _isDisposed = true;
        }
    }

    ~TrayMouseWheelHook()
    {
        Dispose(false);
    }
}
