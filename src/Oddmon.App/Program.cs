using System.Windows.Forms;
using Oddmon.Core;

namespace Oddmon.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var monitor = new DiskMonitor();
        using var trayIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(ActivityLevel.Idle),
            Text = "oddmon — disk idle",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Quit", null, (_, _) => Application.Exit());
        trayIcon.ContextMenuStrip = menu;

        // DiskMonitor raises on a timer thread; marshal back to the UI thread to
        // touch the NotifyIcon. A hidden control gives us a SynchronizationContext.
        using var sync = new Control();
        sync.CreateControl();

        monitor.LevelChanged += level => sync.BeginInvoke(() =>
        {
            var old = trayIcon.Icon;
            trayIcon.Icon = TrayIconFactory.Create(level);
            old?.Dispose();
            trayIcon.Text = $"oddmon — disk {level.ToString().ToLowerInvariant()}";
        });

        using var mic = new MicMonitor();
        // Mute all sounds while the mic is in use (you're in a call). LEDs keep working.
        using var sound = new SeekSoundPlayer(() => monitor.Current != ActivityLevel.Idle && !mic.InCall);

        monitor.Start();
        mic.Start();
        sound.Start();
        Application.Run();

        trayIcon.Visible = false;
    }
}
