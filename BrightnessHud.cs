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

    private int _percentage = 80;

    private BrightnessHud()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        Size = new Size(220, 56);
        BackColor = Color.FromArgb(20, 24, 34);

        var primary = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(primary.Left + (primary.Width - Width) / 2, primary.Top + 60);

        Opacity = 0.0;
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

            _instance._percentage = Math.Clamp(percentage, 0, 100);
            _instance.Invalidate();

            int currentId = ++_displayCounter;
            _ = _instance.AnimateHudAsync(currentId);
        }
    }

    private async Task AnimateHudAsync(int displayId)
    {
        while (Opacity < 0.95 && displayId == _displayCounter)
        {
            Opacity = Math.Min(0.95, Opacity + 0.2);
            await Task.Delay(15);
        }

        await Task.Delay(1000);

        while (Opacity > 0.0 && displayId == _displayCounter)
        {
            Opacity = Math.Max(0.0, Opacity - 0.15);
            await Task.Delay(20);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var bgPath = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 16);
        using (var bgBrush = new SolidBrush(Color.FromArgb(235, 20, 24, 34)))
        {
            g.FillPath(bgBrush, bgPath);
        }
        using (var borderPen = new Pen(Color.FromArgb(100, 56, 189, 248), 1.5f))
        {
            g.DrawPath(borderPen, bgPath);
        }

        string text = $"☀️  Яркость: {_percentage}%";
        using var font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        g.DrawString(text, font, textBrush, new PointF(16, 10));

        int barX = 16;
        int barY = 36;
        int barWidth = Width - 32;
        int barHeight = 8;

        using var trackPath = GetRoundedRect(new Rectangle(barX, barY, barWidth, barHeight), 4);
        using (var trackBrush = new SolidBrush(Color.FromArgb(50, 60, 80)))
        {
            g.FillPath(trackBrush, trackPath);
        }

        int fillWidth = (int)(barWidth * (_percentage / 100.0));
        if (fillWidth > 4)
        {
            using var fillPath = GetRoundedRect(new Rectangle(barX, barY, fillWidth, barHeight), 4);
            using var fillBrush = new LinearGradientBrush(
                new Point(barX, barY),
                new Point(barX + barWidth, barY),
                Color.FromArgb(255, 210, 50),
                Color.FromArgb(56, 189, 248)
            );
            g.FillPath(fillBrush, fillPath);
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
