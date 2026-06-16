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
        using var power = new PowerMonitor();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Quit", null, (_, _) => Application.Exit());

        using var trayIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(ActivityLevel.Idle, TurboState.Off),
            Text = "oddmon — disk idle · turbo off",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // DiskMonitor raises on a timer thread; marshal back to the UI thread to
        // touch the NotifyIcon. A hidden control gives us a SynchronizationContext.
        using var sync = new Control();
        sync.CreateControl();

        // Both LEDs share one icon; either change redraws it from current state.
        void Refresh() => sync.BeginInvoke(() =>
        {
            var old = trayIcon.Icon;
            trayIcon.Icon = TrayIconFactory.Create(monitor.Current, power.Current);
            old?.Dispose();
            trayIcon.Text = $"oddmon — disk {monitor.Current.ToString().ToLowerInvariant()} · " +
                            $"turbo {power.Current.ToString().ToLowerInvariant()}";
        });

        monitor.LevelChanged += _ => Refresh();
        power.TurboChanged += _ => Refresh();

        using var mic = new MicMonitor();
        // Mute all sounds while the mic is in use (you're in a call). LEDs keep working.
        using var sound = new SeekSoundPlayer(() => monitor.Current != ActivityLevel.Idle && !mic.InCall);

        monitor.Start();
        power.Start();
        mic.Start();
        sound.Start();
        Application.Run();

        trayIcon.Visible = false;
    }
}
