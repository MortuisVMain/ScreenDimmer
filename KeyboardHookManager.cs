using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScreenDimmer;

public class KeyboardHookManager : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private DateTime _lastToggleTime = DateTime.MinValue;
    private bool _isDisposed;

    public event Action? DimRequested;
    public event Action? RestoreRequested;
    public event Action? BlackoutToggleRequested;

    public KeyboardHookManager()
    {
        _proc = HookCallback;
        _hookId = SetHook(_proc);
    }

    private IntPtr SetHook(NativeMethods.LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        string? moduleName = curModule?.ModuleName;
        IntPtr moduleHandle = NativeMethods.GetModuleHandle(moduleName);
        return NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, proc, moduleHandle, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                bool isAltDown = (hookStruct.flags & 0x20) != 0 ||
                                 (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) != 0 ||
                                 (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LMENU) & 0x8000) != 0 ||
                                 (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RMENU) & 0x8000) != 0;

                uint scanCode = hookStruct.scanCode;
                uint vkCode = hookStruct.vkCode;

                // 1. Комбинация Alt + Backspace -> Вход / Выход из режима Blackout
                if (isAltDown && (scanCode == NativeMethods.SCAN_CODE_BACKSPACE || vkCode == NativeMethods.VK_BACK))
                {
                    if ((DateTime.UtcNow - _lastToggleTime).TotalMilliseconds > 400)
                    {
                        _lastToggleTime = DateTime.UtcNow;
                        BlackoutToggleRequested?.Invoke();
                        return (IntPtr)1; // Подавляем Backspace
                    }
                }

                // 2. Комбинация Alt + . -> Затемнение до выбранного процента
                if (isAltDown && (scanCode == NativeMethods.SCAN_CODE_PERIOD || vkCode == NativeMethods.VK_OEM_PERIOD))
                {
                    DimRequested?.Invoke();
                    return (IntPtr)1;
                }

                // 3. Комбинация Alt + / -> Восстановление нормальной яркости и выход из Blackout
                if (isAltDown && (scanCode == NativeMethods.SCAN_CODE_SLASH || vkCode == NativeMethods.VK_OEM_2))
                {
                    RestoreRequested?.Invoke();
                    return (IntPtr)1;
                }

                // 4. Защитный экран в режиме Blackout:
                if (BlackoutManager.IsActive)
                {
                    bool isAltKey = vkCode == NativeMethods.VK_MENU || vkCode == NativeMethods.VK_LMENU || vkCode == NativeMethods.VK_RMENU;

                    if (!isAltKey)
                    {
                        return (IntPtr)1; // Поглощаем все случайные клавиши (включая Win, пробел и буквы)
                    }
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
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

    ~KeyboardHookManager()
    {
        Dispose(false);
    }
}
