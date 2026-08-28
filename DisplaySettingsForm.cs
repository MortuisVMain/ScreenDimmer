using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenDimmer;

public class DisplaySettingsForm : Form
{
    private readonly BrightnessController _brightnessController;
    private readonly FlowLayoutPanel _monitorsPanel;
    private readonly CheckBox _fadeCheck;
    private readonly CheckBox _hudCheck;
    private readonly CheckBox _muteCheck;
    private readonly CheckBox _autoRunCheck;

    private readonly List<MonitorCard> _cards = new();

    private class MonitorCard
    {
        public DisplayMonitorInfo Monitor { get; set; } = null!;
        public TrackBar LiveSlider { get; set; } = null!;
        public Label LiveValueLabel { get; set; } = null!;
        public TrackBar NormalSlider { get; set; } = null!;
        public Label NormalValueLabel { get; set; } = null!;
        public TrackBar DimSlider { get; set; } = null!;
        public Label DimValueLabel { get; set; } = null!;
    }

    public DisplaySettingsForm(BrightnessController brightnessController)
    {
        _brightnessController = brightnessController;

        Text = "Screen Dimmer Pro — Настройка экранов и яркости";
        Size = new Size(580, 680);
        MinimumSize = new Size(520, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(20, 24, 34);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = Color.Transparent
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));

        // 1. Header
        var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var titleLabel = new Label
        {
            Text = "⚙️ Настройка яркости по мониторам",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(240, 245, 255),
            AutoSize = true,
            Location = new Point(0, 4)
        };
        var subTitleLabel = new Label
        {
            Text = "Индивидуальная калибровка и раздельное управление каждым дисплеем",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(140, 160, 190),
            AutoSize = true,
            Location = new Point(0, 28)
        };
        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(subTitleLabel);
        mainLayout.Controls.Add(headerPanel, 0, 0);

        // 2. Monitors Scroll Panel
        _monitorsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent
        };
        mainLayout.Controls.Add(_monitorsPanel, 0, 1);

        // 3. Footer Options and Buttons
        var footerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var optGroup = new GroupBox
        {
            Text = "Общие параметры",
            ForeColor = Color.FromArgb(180, 200, 230),
            Dock = DockStyle.Top,
            Height = 75,
            BackColor = Color.Transparent
        };

        _fadeCheck = new CheckBox
        {
            Text = "Плавная анимация",
            Checked = SettingsManager.Current.FadeAnimation,
            AutoSize = true,
            Location = new Point(12, 22),
            ForeColor = Color.White
        };
        _hudCheck = new CheckBox
        {
            Text = "Наэкранный HUD",
            Checked = SettingsManager.Current.ShowBrightnessHud,
            AutoSize = true,
            Location = new Point(170, 22),
            ForeColor = Color.White
        };
        _muteCheck = new CheckBox
        {
            Text = "Mute в Blackout",
            Checked = SettingsManager.Current.MuteAudioInBlackout,
            AutoSize = true,
            Location = new Point(310, 22),
            ForeColor = Color.White
        };
        _autoRunCheck = new CheckBox
        {
            Text = "Автозапуск с Windows",
            Checked = AutoRunManager.IsAutoRunEnabled(),
            AutoSize = true,
            Location = new Point(12, 46),
            ForeColor = Color.White
        };

        optGroup.Controls.Add(_fadeCheck);
        optGroup.Controls.Add(_hudCheck);
        optGroup.Controls.Add(_muteCheck);
        optGroup.Controls.Add(_autoRunCheck);
        footerPanel.Controls.Add(optGroup);

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent
        };

        var saveBtn = new Button
        {
            Text = "💾 Сохранить",
            Size = new Size(120, 34),
            BackColor = Color.FromArgb(40, 120, 220),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.Click += (s, e) => SaveAndApply();

        var cancelBtn = new Button
        {
            Text = "Закрыть",
            Size = new Size(95, 34),
            BackColor = Color.FromArgb(40, 48, 64),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        cancelBtn.FlatAppearance.BorderSize = 0;
        cancelBtn.Click += (s, e) => Close();

        btnPanel.Controls.Add(saveBtn);
        btnPanel.Controls.Add(cancelBtn);
        footerPanel.Controls.Add(btnPanel);

        mainLayout.Controls.Add(footerPanel, 0, 2);
        Controls.Add(mainLayout);

        PopulateMonitorCards();
    }

    private void PopulateMonitorCards()
    {
        _monitorsPanel.Controls.Clear();
        _cards.Clear();

        var monitors = _brightnessController.GetConnectedMonitors();

        foreach (var mon in monitors)
        {
            var cardPanel = new Panel
            {
                Width = 520,
                Height = 160,
                Margin = new Padding(0, 0, 0, 14),
                BackColor = Color.FromArgb(28, 34, 48),
                Padding = new Padding(12)
            };

            // Custom border drawing
            cardPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(50, 65, 90), 1);
                e.Graphics.DrawRectangle(pen, 0, 0, cardPanel.Width - 1, cardPanel.Height - 1);
            };

            var nameLabel = new Label
            {
                Text = $"{mon.FriendlyName} {(mon.IsPrimary ? "⭐ (Основной)" : "")}",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 215, 60),
                AutoSize = true,
                Location = new Point(12, 10)
            };
            cardPanel.Controls.Add(nameLabel);

            var typeBadge = new Label
            {
                Text = mon.IsLaptopInternal ? "eDP / WMI" : "DDC/CI DXVA2",
                Font = new Font("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 160, 220),
                AutoSize = true,
                Location = new Point(410, 12)
            };
            cardPanel.Controls.Add(typeBadge);

            // Row 1: Live Hardware Brightness
            var liveTitle = new Label
            {
                Text = "⚡ Текущая яркость:",
                ForeColor = Color.FromArgb(210, 225, 245),
                AutoSize = true,
                Location = new Point(12, 40)
            };
            var liveVal = new Label
            {
                Text = $"{mon.CurrentBrightness}%",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                AutoSize = true,
                Location = new Point(155, 40)
            };
            var liveTrack = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = Math.Clamp(mon.CurrentBrightness, 0, 100),
                TickFrequency = 10,
                SmallChange = 5,
                LargeChange = 10,
                Width = 290,
                Height = 30,
                Location = new Point(205, 36)
            };
            liveTrack.Scroll += (s, e) =>
            {
                liveVal.Text = $"{liveTrack.Value}%";
                _brightnessController.SetMonitorBrightnessById(mon.Id, liveTrack.Value);
            };
            cardPanel.Controls.Add(liveTitle);
            cardPanel.Controls.Add(liveVal);
            cardPanel.Controls.Add(liveTrack);

            // Row 2: Default Normal Brightness
            var normTitle = new Label
            {
                Text = "☀️ Обычная яркость:",
                ForeColor = Color.FromArgb(180, 200, 220),
                AutoSize = true,
                Location = new Point(12, 78)
            };
            var normVal = new Label
            {
                Text = $"{mon.NormalBrightness}%",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 210, 50),
                AutoSize = true,
                Location = new Point(155, 78)
            };
            var normTrack = new TrackBar
            {
                Minimum = 10,
                Maximum = 100,
                Value = Math.Clamp(mon.NormalBrightness, 10, 100),
                TickFrequency = 10,
                SmallChange = 5,
                LargeChange = 10,
                Width = 290,
                Height = 30,
                Location = new Point(205, 74)
            };
            normTrack.Scroll += (s, e) =>
            {
                normVal.Text = $"{normTrack.Value}%";
            };
            cardPanel.Controls.Add(normTitle);
            cardPanel.Controls.Add(normVal);
            cardPanel.Controls.Add(normTrack);

            // Row 3: Dim Brightness
            var dimTitle = new Label
            {
                Text = "🌙 Уровень затемнения:",
                ForeColor = Color.FromArgb(180, 200, 220),
                AutoSize = true,
                Location = new Point(12, 116)
            };
            var dimVal = new Label
            {
                Text = $"{mon.DimBrightness}%",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(140, 160, 255),
                AutoSize = true,
                Location = new Point(155, 116)
            };
            var dimTrack = new TrackBar
            {
                Minimum = 0,
                Maximum = 50,
                Value = Math.Clamp(mon.DimBrightness, 0, 50),
                TickFrequency = 5,
                SmallChange = 1,
                LargeChange = 5,
                Width = 290,
                Height = 30,
                Location = new Point(205, 112)
            };
            dimTrack.Scroll += (s, e) =>
            {
                dimVal.Text = $"{dimTrack.Value}%";
            };
            cardPanel.Controls.Add(dimTitle);
            cardPanel.Controls.Add(dimVal);
            cardPanel.Controls.Add(dimTrack);

            _monitorsPanel.Controls.Add(cardPanel);

            _cards.Add(new MonitorCard
            {
                Monitor = mon,
                LiveSlider = liveTrack,
                LiveValueLabel = liveVal,
                NormalSlider = normTrack,
                NormalValueLabel = normVal,
                DimSlider = dimTrack,
                DimValueLabel = dimVal
            });
        }
    }

    private void SaveAndApply()
    {
        SettingsManager.SetFadeAnimation(_fadeCheck.Checked);
        SettingsManager.SetShowBrightnessHud(_hudCheck.Checked);
        SettingsManager.SetMuteAudioInBlackout(_muteCheck.Checked);
        AutoRunManager.SetAutoRun(_autoRunCheck.Checked);

        foreach (var card in _cards)
        {
            _brightnessController.SetMonitorNormalBrightness(card.Monitor.Id, card.NormalSlider.Value);
            _brightnessController.SetMonitorDimBrightness(card.Monitor.Id, card.DimSlider.Value);
            _brightnessController.SetMonitorBrightnessById(card.Monitor.Id, card.LiveSlider.Value);
        }

        Close();
    }
}