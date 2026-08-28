using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ScreenDimmer;

public static class IconMaker
{
    public static void GenerateAppIcon(string outputPath)
    {
        int[] sizes = { 16, 32, 48, 64, 128, 256 };
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((short)0);
        bw.Write((short)1);
        bw.Write((short)sizes.Length);

        var pngStreams = new byte[sizes.Length][];

        for (int i = 0; i < sizes.Length; i++)
        {
            using var bmp = DrawModernDimmerIcon(sizes[i]);
            using var pngMs = new MemoryStream();
            bmp.Save(pngMs, ImageFormat.Png);
            pngStreams[i] = pngMs.ToArray();
        }

        int offset = 6 + (16 * sizes.Length);

        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            bw.Write((byte)(size == 256 ? 0 : size));
            bw.Write((byte)(size == 256 ? 0 : size));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((short)1);
            bw.Write((short)32);
            bw.Write(pngStreams[i].Length);
            bw.Write(offset);
            offset += pngStreams[i].Length;
        }

        for (int i = 0; i < sizes.Length; i++)
        {
            bw.Write(pngStreams[i]);
        }

        File.WriteAllBytes(outputPath, ms.ToArray());
    }

    private static Bitmap DrawModernDimmerIcon(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float s = size / 256f;

        using (var bgPath = GetRoundedRect(new RectangleF(16 * s, 16 * s, 224 * s, 224 * s), 48 * s))
        {
            using var bgBrush = new LinearGradientBrush(
                new PointF(0, 16 * s),
                new PointF(0, 240 * s),
                Color.FromArgb(255, 30, 36, 48),
                Color.FromArgb(255, 14, 18, 24)
            );
            g.FillPath(bgBrush, bgPath);

            using var borderPen = new Pen(Color.FromArgb(180, 56, 189, 248), 6 * s);
            g.DrawPath(borderPen, bgPath);
        }

        using (var innerPath = GetRoundedRect(new RectangleF(28 * s, 28 * s, 200 * s, 200 * s), 36 * s))
        {
            using var screenBrush = new LinearGradientBrush(
                new PointF(0, 28 * s),
                new PointF(0, 228 * s),
                Color.FromArgb(255, 18, 24, 38),
                Color.FromArgb(255, 8, 12, 18)
            );
            g.FillPath(screenBrush, innerPath);
        }

        using (var sunBrush = new LinearGradientBrush(
            new PointF(70 * s, 70 * s),
            new PointF(150 * s, 150 * s),
            Color.FromArgb(255, 255, 200, 50),
            Color.FromArgb(255, 255, 140, 20)
        ))
        {
            g.FillEllipse(sunBrush, 76 * s, 76 * s, 104 * s, 104 * s);
        }

        using (var moonBrush = new LinearGradientBrush(
            new PointF(110 * s, 60 * s),
            new PointF(190 * s, 180 * s),
            Color.FromArgb(255, 56, 189, 248),
            Color.FromArgb(255, 30, 60, 140)
        ))
        {
            using var moonPath = new GraphicsPath();
            moonPath.AddEllipse(100 * s, 68 * s, 96 * s, 96 * s);
            using var cutPath = new GraphicsPath();
            cutPath.AddEllipse(80 * s, 54 * s, 88 * s, 88 * s);

            var reg = new Region(moonPath);
            reg.Exclude(cutPath);
            g.FillRegion(moonBrush, reg);
        }

        using (var rayPen = new Pen(Color.FromArgb(220, 255, 215, 60), 7 * s))
        {
            rayPen.StartCap = LineCap.Round;
            rayPen.EndCap = LineCap.Round;

            g.DrawLine(rayPen, 128 * s, 46 * s, 128 * s, 60 * s);
            g.DrawLine(rayPen, 46 * s, 128 * s, 60 * s, 128 * s);
            g.DrawLine(rayPen, 70 * s, 70 * s, 80 * s, 80 * s);
            g.DrawLine(rayPen, 70 * s, 186 * s, 80 * s, 176 * s);
        }

        return bmp;
    }

    private static GraphicsPath GetRoundedRect(RectangleF rect, float radius)
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
