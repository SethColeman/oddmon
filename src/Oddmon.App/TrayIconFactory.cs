using System.Drawing;
using Oddmon.Core;

namespace Oddmon.App;

/// <summary>
/// Builds the tray icon at runtime: two stacked rectangular LEDs in one 16x16
/// icon — HDD activity on top, Turbo below. No binary icon assets to ship.
/// </summary>
internal static class TrayIconFactory
{
    private static Color ActivityColor(ActivityLevel level) => level switch
    {
        ActivityLevel.Read => Color.FromArgb(40, 220, 60),    // green
        ActivityLevel.Write => Color.FromArgb(230, 50, 50),   // red
        ActivityLevel.Mixed => Color.FromArgb(245, 175, 30),  // amber
        _ => Color.FromArgb(45, 55, 50),                      // idle: dim
    };

    private static Color TurboColor(TurboState state) => state switch
    {
        TurboState.Bright => Color.FromArgb(255, 215, 0),     // bright gold
        TurboState.Dim => Color.FromArgb(110, 95, 0),         // dim gold
        _ => Color.FromArgb(50, 48, 30),                      // off
    };

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
            DrawLed(g, new Rectangle(2, 3, 12, 4), ActivityColor(level)); // top: HDD
            DrawLed(g, new Rectangle(2, 9, 12, 4), TurboColor(turbo));    // bottom: Turbo
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
