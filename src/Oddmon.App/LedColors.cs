using System.Drawing;
using Oddmon.Core;

namespace Oddmon.App;

/// <summary>Shared LED colors for the tray icon and the desktop panel.</summary>
internal static class LedColors
{
    public static Color Activity(ActivityLevel level) => level switch
    {
        ActivityLevel.Read => Color.FromArgb(40, 220, 60),   // green
        ActivityLevel.Write => Color.FromArgb(230, 50, 50),  // red
        ActivityLevel.Mixed => Color.FromArgb(245, 175, 30), // amber
        _ => Color.FromArgb(45, 55, 50),                     // idle: dim
    };

    public static Color Turbo(TurboState state) => state switch
    {
        TurboState.Bright => Color.FromArgb(255, 215, 0),    // bright gold
        TurboState.Dim => Color.FromArgb(110, 95, 0),        // dim gold
        _ => Color.FromArgb(50, 48, 30),                     // off
    };
}
