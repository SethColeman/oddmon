using System.Drawing;
using Oddmon.Core;

namespace Oddmon.App;

/// <summary>
/// Builds the tray icon at runtime: two stacked rectangular LEDs in one 16x16
/// icon — HDD activity on top, Turbo below. No binary icon assets to ship.
/// </summary>
internal static class TrayIconFactory
{
    /// <summary>
    /// Caller owns the returned <see cref="Icon"/> and must dispose the previous
    /// one — GDI handles from GetHicon are not garbage-collected.
    /// </summary>
    public static Icon Create(ActivityLevel level, TurboState turbo)
    {
        const int size = 16;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            DrawLed(g, new Rectangle(2, 3, 12, 4), LedColors.Activity(level)); // top: HDD
            DrawLed(g, new Rectangle(2, 9, 12, 4), LedColors.Turbo(turbo));    // bottom: Turbo
        }

        IntPtr hicon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hicon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hicon);
        }
    }

    private static void DrawLed(Graphics g, Rectangle rect, Color color)
    {
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, rect);
    }
}
