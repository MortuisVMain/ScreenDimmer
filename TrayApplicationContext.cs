using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenDimmer;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly BrightnessController _brightnessController;
    private readonly KeyboardHookManager _keyboardHook;
    private readonly TrayMouseWheelHook _mouseWheelHook;
    private readonly SessionRecoveryManager _sessionRecovery;

    private readonly ToolStripMenuItem _blackoutMenuItem;
    private readonly ToolStripMenuItem _dimMenuItem;
    private readonly ToolStripMenuItem _restoreMenuItem;
    private readonly ToolStripMenuItem _soloScreenMenu;
    private readonly ToolStripMenuItem _sleepTimerMenu;
    private readonly ToolStripMenuItem _dimLevelMenu;
    private readonly ToolStripMenuItem _optionsMenu;
    private readonly ToolStripMenuItem _brightnessInfoItem;
    private readonly ToolStripMenuItem _autoRunMenuItem;

    private IntPtr _currentIconHandle = IntPtr.Zero;
    private readonly int[] _presetLevels = { 0, 1, 2, 3, 5, 10, 15, 20 };

    public TrayApplicationContext()
    {
        _brightnessController = new BrightnessController();
        _sessionRecovery = new SessionRecoveryManager(_brightnessController);
        _keyboardHook = new KeyboardHookManager();
        _mouseWheelHook = new TrayMouseWheelHook();

        // Подписка на горячие клавиши и события
        _keyboardHook.DimRequested += OnDimRequested;
        _keyboardHook.RestoreRequested += OnRestoreRequested;
        _keyboardHook.BlackoutToggleRequested += OnBlackoutToggleRequested;

        // Регулировка яркости колесиком мыши по трею
        _mouseWheelHook.WheelScrolled += OnTrayWheelScrolled;

        _brightnessController.StateChanged += OnBrightnessStateChanged;
        BlackoutManager.StateChanged += OnBlackoutStateChanged;
        SleepTimerManager.TimerTick += OnSleepTimerTick;
        SleepTimerManager.StateChanged += isRunning => UpdateMenuState();

        // Создание контекстного меню трея
        var contextMenu = new ContextMenuStrip();

        _blackoutMenuItem = new ToolStripMenuItem("🌑 Ночной Blackout (Alt + Backspace)", null, (s, e) => BlackoutManager.Toggle(_brightnessController));
        _dimMenuItem = new ToolStripMenuItem($"🌙 Затемнить до {SettingsManager.Current.DimPercentage}% (Alt + .)", null, (s, e) => _brightnessController.Dim());
        _restoreMenuItem = new ToolStripMenuItem("☀️ Восстановить яркость (Alt + /)", null, (s, e) => OnRestoreRequested());

        // Подменю раздельного управления мониторами
        _soloScreenMenu = new ToolStripMenuItem("🖥️ Режим экранов");
        BuildSoloScreenMenu();

        // Подменю таймера сна
        _sleepTimerMenu = new ToolStripMenuItem("⏱️ Таймер сна");
        BuildSleepTimerMenu();

        // Подменю выбора процента затемнения
        _dimLevelMenu = new ToolStripMenuItem("🎯 Процент затемнения");
        BuildDimLevelSubMenu();

        // Меню дополнительных настроек (Плавность, HUD, Ночной Mute)
        _optionsMenu = new ToolStripMenuItem("⚙️ Опции и анимации");
        BuildOptionsMenu();

        _autoRunMenuItem = new ToolStripMenuItem("🚀 Запуск вместе с Windows", null, OnAutoRunToggled)
        {
            CheckOnClick = true,
            Checked = AutoRunManager.IsAutoRunEnabled()
        };

        _brightnessInfoItem = new ToolStripMenuItem($"📊 Текущая яркость: {_brightnessController.GetAverageBrightness()}% (Скролл трея)")
        {
            Enabled = false
        };

        var exitMenuItem = new ToolStripMenuItem("❌ Выход", null, (s, e) => ExitThread());

        contextMenu.Items.Add(_blackoutMenuItem);
        contextMenu.Items.Add(_dimMenuItem);
        contextMenu.Items.Add(_restoreMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(_soloScreenMenu);
        contextMenu.Items.Add(_sleepTimerMenu);
        contextMenu.Items.Add(_dimLevelMenu);
        contextMenu.Items.Add(_optionsMenu);
        contextMenu.Items.Add(_autoRunMenuItem);
        contextMenu.Items.Add(_brightnessInfoItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitMenuItem);

        // Создание NotifyIcon
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = contextMenu,
            Text = "Screen Dimmer Pro (Alt+Backspace / Alt+. / Alt+/)",
            Visible = true
        };

        SetTrayIcon(false);

        // Двойной клик по иконке переключает режим затемнения
        _notifyIcon.DoubleClick += (s, e) =>
        {
            if (BlackoutManager.IsActive)
            {
                BlackoutManager.Deactivate(_brightnessController);
            }
            else if (_brightnessController.IsDimmed)
            {
                _brightnessController.Restore();
            }
            else
            {
                _brightnessController.Dim();
            }
        };

        // Открытие меню обновляет статус
        contextMenu.Opening += (s, e) =>
        {
            int bri = _brightnessController.GetAverageBrightness();
            _brightnessInfoItem.Text = $"📊 Текущая яркость: {bri}% (Скролл трея)";
            _dimMenuItem.Text = $"🌙 Затемнить до {SettingsManager.Current.DimPercentage}% (Alt + .)";
            _dimMenuItem.Enabled = !_brightnessController.IsDimmed && !BlackoutManager.IsActive;
            _restoreMenuItem.Enabled = _brightnessController.IsDimmed || BlackoutManager.IsActive;
            _blackoutMenuItem.Text = BlackoutManager.IsActive 
                ? "☀️ Выключить Blackout (Alt + Backspace)" 
                : "🌑 Ночной Blackout (Alt + Backspace)";

            BuildSoloScreenMenu();
            BuildSleepTimerMenu();
            BuildDimLevelSubMenu();
            BuildOptionsMenu();
        };

        UpdateMenuState();

        _notifyIcon.ShowBalloonTip(
            3000,
            "Screen Dimmer Pro активен",
            "Alt + Backspace — Ночной Blackout (экраны 100% темные, ПК не спит)\nКолесико по трею — быстрая яркость\nAlt + . / Alt + / — затемнить / восстановить",
            ToolTipIcon.Info
        );
    }

    private void OnTrayWheelScrolled(int delta)
    {
        int newBri = _brightnessController.AdjustBrightness(delta);
        _notifyIcon.Text = $"Screen Dimmer (Яркость: {newBri}%)";
        _brightnessInfoItem.Text = $"📊 Текущая яркость: {newBri}% (Скролл трея)";

        BrightnessHud.ShowHud(newBri);
    }

    private void BuildSoloScreenMenu()
    {
        _soloScreenMenu.DropDownItems.Clear();

        var allItem = new ToolStripMenuItem("🌑 Все экраны", null, (s, e) => BlackoutManager.Activate(_brightnessController, null))
        {
            Checked = BlackoutManager.IsActive && BlackoutManager.ActiveSoloScreenIndex == null
        };
        _soloScreenMenu.DropDownItems.Add(allItem);

        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            int idx = i;
            string name = screens[i].Primary ? $"💻 Экран ноутбука / Главный ({screens[i].Bounds.Width}x{screens[i].Bounds.Height})" : $"🖥️ Монитор {i + 1} ({screens[i].Bounds.Width}x{screens[i].Bounds.Height})";
            var item = new ToolStripMenuItem(name, null, (s, e) => BlackoutManager.Activate(_brightnessController, idx))
            {
                Checked = BlackoutManager.IsActive && BlackoutManager.ActiveSoloScreenIndex == idx
            };
            _soloScreenMenu.DropDownItems.Add(item);
        }
    }

    private void BuildSleepTimerMenu()
    {
        _sleepTimerMenu.DropDownItems.Clear();

        if (SleepTimerManager.IsRunning)
        {
            _sleepTimerMenu.Text = $"⏱️ Таймер сна (осталось {SleepTimerManager.RemainingMinutes} мин)";
            var cancelItem = new ToolStripMenuItem("❌ Отменить таймер", null, (s, e) => SleepTimerManager.Stop());
            _sleepTimerMenu.DropDownItems.Add(cancelItem);
            _sleepTimerMenu.DropDownItems.Add(new ToolStripSeparator());
        }
        else
        {
            _sleepTimerMenu.Text = "⏱️ Таймер сна";
        }

        int[] durations = { 15, 30, 45, 60, 120 };
        foreach (int min in durations)
        {
            string label = min >= 60 ? $"Через {min / 60} ч." : $"Через {min} мин.";
            var item = new ToolStripMenuItem(label, null, (s, e) =>
            {
                SleepTimerManager.Start(min, _brightnessController);
                _notifyIcon.ShowBalloonTip(2000, "Таймер сна запущен", $"Blackout включится через {min} мин.", ToolTipIcon.Info);
            });
            _sleepTimerMenu.DropDownItems.Add(item);
        }
    }

    private void BuildOptionsMenu()
    {
        _optionsMenu.DropDownItems.Clear();

        var fadeItem = new ToolStripMenuItem("🌊 Плавная кино-анимация (Fade)", null, (s, e) =>
        {
            SettingsManager.SetFadeAnimation(!SettingsManager.Current.FadeAnimation);
            BuildOptionsMenu();
        })
        {
            Checked = SettingsManager.Current.FadeAnimation
        };

        var hudItem = new ToolStripMenuItem("📊 Наэкранный индикатор яркости (HUD)", null, (s, e) =>
        {
            SettingsManager.SetShowBrightnessHud(!SettingsManager.Current.ShowBrightnessHud);
            BuildOptionsMenu();
        })
        {
            Checked = SettingsManager.Current.ShowBrightnessHud
        };

        var muteItem = new ToolStripMenuItem("🔇 Отключать звук в режиме Blackout (Night Mute)", null, (s, e) =>
        {
            SettingsManager.SetMuteAudioInBlackout(!SettingsManager.Current.MuteAudioInBlackout);
            BuildOptionsMenu();
        })
        {
            Checked = SettingsManager.Current.MuteAudioInBlackout
        };

        _optionsMenu.DropDownItems.Add(fadeItem);
        _optionsMenu.DropDownItems.Add(hudItem);
        _optionsMenu.DropDownItems.Add(muteItem);
    }

    private void BuildDimLevelSubMenu()
    {
        _dimLevelMenu.DropDownItems.Clear();
        int currentPercent = SettingsManager.Current.DimPercentage;

        foreach (int lvl in _presetLevels)
        {
            string label = (lvl == 0) ? "⚫ 0% (Максимально темно)" : $"{lvl}%";
            var item = new ToolStripMenuItem(label, null, (s, e) => SetDimLevel(lvl))
            {
                Checked = (currentPercent == lvl)
            };
            _dimLevelMenu.DropDownItems.Add(item);
        }

        _dimLevelMenu.DropDownItems.Add(new ToolStripSeparator());

        var customItem = new ToolStripMenuItem("✏️ Другой процент...", null, (s, e) =>
        {
            using var dlg = new CustomPercentDialog(SettingsManager.Current.DimPercentage);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                SetDimLevel(dlg.SelectedPercentage);
            }
        });
        _dimLevelMenu.DropDownItems.Add(customItem);
    }

    private void SetDimLevel(int percent)
    {
        SettingsManager.SetDimPercentage(percent);
        BuildDimLevelSubMenu();
        UpdateMenuState();

        if (_brightnessController.IsDimmed && !BlackoutManager.IsActive)
        {
            _brightnessController.Dim(percent);
            BrightnessHud.ShowHud(percent);
        }
    }

    private void OnDimRequested()
    {
        if (BlackoutManager.IsActive)
        {
            BlackoutManager.Deactivate(_brightnessController);
        }
        _brightnessController.Dim();
        BrightnessHud.ShowHud(SettingsManager.Current.DimPercentage);
    }

    private void OnRestoreRequested()
    {
        if (BlackoutManager.IsActive)
        {
            BlackoutManager.Deactivate(_brightnessController);
        }
        else
        {
            _brightnessController.Restore();
        }
        BrightnessHud.ShowHud(_brightnessController.GetAverageBrightness());
    }

    private void OnBlackoutToggleRequested()
    {
        BlackoutManager.Toggle(_brightnessController);
    }

    private void OnSleepTimerTick()
    {
        if (SleepTimerManager.IsRunning)
        {
            _sleepTimerMenu.Text = $"⏱️ Таймер сна (осталось {SleepTimerManager.RemainingMinutes} мин)";
        }
    }

    private void OnBlackoutStateChanged(bool isActive)
    {
        UpdateMenuState();
        SetTrayIcon(isActive || _brightnessController.IsDimmed);
        _notifyIcon.Text = isActive ? "Screen Dimmer (Blackout Активен)" : "Screen Dimmer (Стандарт)";

        if (isActive)
        {
            _notifyIcon.ShowBalloonTip(1500, "Ночной Blackout", "Экраны погашены. ПК работает на 100% мощности.\nВыход только по комбинации Alt + Backspace.", ToolTipIcon.None);
        }
        else
        {
            _notifyIcon.ShowBalloonTip(1500, "Blackout отключен", "Экраны и яркость восстановлены.", ToolTipIcon.None);
        }
    }

    private void OnBrightnessStateChanged(bool isDimmed)
    {
        UpdateMenuState();
        SetTrayIcon(isDimmed || BlackoutManager.IsActive);
        _notifyIcon.Text = isDimmed ? $"Screen Dimmer (Затемнен: {SettingsManager.Current.DimPercentage}%)" : "Screen Dimmer (Стандарт)";
    }

    private void UpdateMenuState()
    {
        _dimMenuItem.Text = $"🌙 Затемнить до {SettingsManager.Current.DimPercentage}% (Alt + .)";
        _dimMenuItem.Enabled = !_brightnessController.IsDimmed && !BlackoutManager.IsActive;
        _restoreMenuItem.Enabled = _brightnessController.IsDimmed || BlackoutManager.IsActive;
        _blackoutMenuItem.Text = BlackoutManager.IsActive 
            ? "☀️ Выключить Blackout (Alt + Backspace)" 
            : "🌑 Ночной Blackout (Alt + Backspace)";
        int bri = _brightnessController.GetAverageBrightness();
        _brightnessInfoItem.Text = $"📊 Текущая яркость: {bri}% (Скролл трея)";

        if (SleepTimerManager.IsRunning)
        {
            _sleepTimerMenu.Text = $"⏱️ Таймер сна (осталось {SleepTimerManager.RemainingMinutes} мин)";
        }
        else
        {
            _sleepTimerMenu.Text = "⏱️ Таймер сна";
        }
    }

    private void OnAutoRunToggled(object? sender, EventArgs e)
    {
        AutoRunManager.SetAutoRun(_autoRunMenuItem.Checked);
    }

    private void SetTrayIcon(bool isDark)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            if (isDark)
            {
                using var brush = new SolidBrush(Color.FromArgb(90, 160, 255));
                g.FillEllipse(brush, 4, 4, 24, 24);
                using var cutBrush = new SolidBrush(Color.FromArgb(30, 30, 40));
                g.FillEllipse(cutBrush, 10, 2, 20, 20);
            }
            else
            {
                using var brush = new SolidBrush(Color.FromArgb(255, 210, 50));
                g.FillEllipse(brush, 8, 8, 16, 16);

                using var pen = new Pen(Color.FromArgb(255, 210, 50), 2);
                g.DrawLine(pen, 16, 2, 16, 6);
                g.DrawLine(pen, 16, 26, 16, 30);
                g.DrawLine(pen, 2, 16, 6, 16);
                g.DrawLine(pen, 26, 16, 30, 16);
                g.DrawLine(pen, 6, 6, 9, 9);
                g.DrawLine(pen, 23, 23, 26, 26);
                g.DrawLine(pen, 23, 9, 26, 6);
                g.DrawLine(pen, 6, 23, 9, 26);
            }
        }

        IntPtr newIconHandle = bitmap.GetHicon();
        var newIcon = Icon.FromHandle(newIconHandle);

        var oldIcon = _notifyIcon.Icon;
        IntPtr oldHandle = _currentIconHandle;

        _notifyIcon.Icon = newIcon;
        _currentIconHandle = newIconHandle;

        if (oldIcon != null)
        {
            oldIcon.Dispose();
        }
        if (oldHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(oldHandle);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyboardHook.Dispose();
            _mouseWheelHook.Dispose();
            _sessionRecovery.Dispose();
            SleepTimerManager.Stop();
            
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();

            if (_currentIconHandle != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(_currentIconHandle);
                _currentIconHandle = IntPtr.Zero;
            }
        }
        base.Dispose(disposing);
    }
}
