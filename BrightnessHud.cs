using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenDimmer;

public class BrightnessHud : Form
{
    private static BrightnessHud? _instance;
    private static readonly object _lock = new();
    private static int _displayCounter;

    private int _percentage = 50;
    private bool _isUltraDark;
    private string? _customTitle;

    public BrightnessHud()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Size = new Size(240, 64);
        BackColor = Color.FromArgb(18, 18, 22);

        var primary = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(primary.Left + (primary.Width - Width) / 2, primary.Top + 56);

        Opacity = 0.0;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x20 | 0x80 | 0x08 | 0x08000000;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public static void ShowHud(int percentage)
    {
        if (!SettingsManager.Current.ShowBrightnessHud || BlackoutManager.IsActive) return;

        lock (_lock)
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new BrightnessHud();
                _instance.Show();
            }

            _instance._isUltraDark = false;
            _instance._customTitle = null;
            _instance._percentage = Math.Clamp(percentage, 0, 100);
            _instance.Invalidate();

            int currentId = ++_displayCounter;
            _ = _instance.AnimateHudAsync(currentId);
        }
    }

    public static void ShowUltraDarkHud()
    {
        if (!SettingsManager.Current.ShowBrightnessHud || BlackoutManager.IsActive) return;

        lock (_lock)
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new BrightnessHud();
                _instance.Show();
            }

            _instance._isUltraDark = true;
            _instance._customTitle = "🌌  Сверхтемнота (Ultra-Dark)";
            _instance._percentage = 0;
            _instance.Invalidate();

            int currentId = ++_displayCounter;
            _ = _instance.AnimateHudAsync(currentId);
        }
    }

    private async Task AnimateHudAsync(int displayId)
    {
        while (Opacity < 0.96 && displayId == _displayCounter)
        {
            Opacity = Math.Min(0.96, Opacity + 0.18);
            await Task.Delay(14);
        }

        await Task.Delay(1100);

        while (Opacity > 0.0 && displayId == _displayCounter)
        {
            Opacity = Math.Max(0.0, Opacity - 0.12);
            await Task.Delay(18);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 1. Apple Cupertino Frosted Glass Capsule
        using var bgPath = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 20);
        using (var bgBrush = new SolidBrush(Color.FromArgb(240, 22, 22, 26)))
        {
            g.FillPath(bgBrush, bgPath);
        }
        
        // 2. Subpixel Hairline Border (1px Apple translucent border)
        using (var borderPen = new Pen(Color.FromArgb(42, 255, 255, 255), 1.0f))
        {
            g.DrawPath(borderPen, bgPath);
        }

        // 3. Apple Icon & Typography
        using var fontTitle = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        using var fontValue = new Font("Segoe UI", 10.0f, FontStyle.Bold);
        using var brushTitle = new SolidBrush(Color.FromArgb(180, 245, 245, 247));
        using var brushValue = new SolidBrush(Color.FromArgb(245, 245, 247));

        if (_isUltraDark && !string.IsNullOrEmpty(_customTitle))
        {
            g.DrawString(_customTitle, fontTitle, brushTitle, new PointF(18, 12));
        }
        else
        {
            string icon = _percentage switch
            {
                0 => "🌑",
                < 30 => "🌘",
                < 70 => "🌤️",
                _ => "☀️"
            };

            g.DrawString($"{icon}  Яркость", fontTitle, brushTitle, new PointF(18, 12));
            
            string valText = $"{_percentage}%";
            var valSize = g.MeasureString(valText, fontValue);
            g.DrawString(valText, fontValue, brushValue, new PointF(Width - 18 - valSize.Width, 11));
        }

        // 4. Apple Capsule Slider Track
        int barX = 18;
        int barY = 38;
        int barWidth = Width - 36;
        int barHeight = 10;

        using var trackPath = GetRoundedRect(new Rectangle(barX, barY, barWidth, barHeight), 5);
        using (var trackBrush = new SolidBrush(Color.FromArgb(48, 48, 54)))
        {
            g.FillPath(trackBrush, trackPath);
        }

        // 5. Active Fill
        int fillWidth = _isUltraDark ? (int)(barWidth * 0.15) : (int)(barWidth * (_percentage / 100.0));
        if (fillWidth > 6)
        {
            using var fillPath = GetRoundedRect(new Rectangle(barX, barY, fillWidth, barHeight), 5);
            if (_isUltraDark)
            {
                using var fillBrush = new SolidBrush(Color.FromArgb(168, 85, 247));
                g.FillPath(fillBrush, fillPath);
            }
            else
            {
                using var fillBrush = new LinearGradientBrush(
                    new Point(barX, barY),
                    new Point(barX + barWidth, barY),
                    Color.FromArgb(245, 245, 247),
                    Color.FromArgb(0, 113, 227) // Apple Accent Blue
                );
                g.FillPath(fillBrush, fillPath);
            }
        }
    }

    private static GraphicsPath GetRoundedRect(Rectangle rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
