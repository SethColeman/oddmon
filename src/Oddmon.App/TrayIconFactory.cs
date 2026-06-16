using System.Drawing;
using System.Drawing.Drawing2D;
using Oddmon.Core;

namespace Oddmon.App;

/// <summary>
/// Builds tray icons at runtime as a glowing LED dot, one color per
/// <see cref="ActivityLevel"/> — no binary icon assets to ship or license.
/// </summary>
internal static class TrayIconFactory
{
    private static Color ColorFor(ActivityLevel level) => level switch
    {
        ActivityLevel.Read => Color.FromArgb(40, 220, 60),    // green
        ActivityLevel.Write => Color.FromArgb(230, 50, 50),   // red
        ActivityLevel.Mixed => Color.FromArgb(245, 175, 30),  // amber
        _ => Color.FromArgb(45, 55, 50),                      // idle: dim
    };

    /// <summary>
    /// Caller owns the returned <see cref="Icon"/> and must dispose the previous
    /// one — GDI handles from GetHicon are not garbage-collected.
    /// </summary>
    public static Icon Create(ActivityLevel level)
    {
        const int size = 16;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var dot = new Rectangle(2, 2, size - 4, size - 4);
            using var brush = new SolidBrush(ColorFor(level));
            g.FillEllipse(brush, dot);
        }

        IntPtr hicon = bmp.GetHicon();
        try
        {
            // Clone so the Icon owns its own copy; then free the GDI handle.
            using var temp = Icon.FromHandle(hicon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hicon);
        }
    }
}
