using System.Windows.Forms;
using Oddmon.Core;

namespace Oddmon.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var config = ConfigStore.Load();

        using var monitor = new DiskMonitor(config.DiskSensitivity);
        using var power = new PowerMonitor();
        using var mic = new MicMonitor();
        // Mute all sounds while the mic is in use (you're in a call). LEDs keep working.
        string soundDir = config.SoundSetPath ?? Path.Combine(AppContext.BaseDirectory, "sounds");
        using var sound = new SeekSoundPlayer(
            () => monitor.Current != ActivityLevel.Idle && !mic.InCall, config.Volume, soundSetDir: soundDir)
        {
            Enabled = config.SoundEnabled,
        };

        var menu = BuildMenu(sound, config);

        using var trayIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(ActivityLevel.Idle, TurboState.Off),
            Text = "oddmon — disk idle · turbo off",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // Monitors raise on timer threads; marshal back to the UI thread to touch
        // the NotifyIcon. A hidden control gives us a SynchronizationContext.
        using var sync = new Control();
        sync.CreateControl();

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

        monitor.Start();
        power.Start();
        mic.Start();
        sound.Start();
        Application.Run();

        trayIcon.Visible = false;
    }

    private static ContextMenuStrip BuildMenu(SeekSoundPlayer sound, OddmonConfig config)
    {
        var menu = new ContextMenuStrip();

        var localConfig = config; // captured, updated on change then re-saved
        var mute = new ToolStripMenuItem("Mute sounds") { CheckOnClick = true, Checked = !sound.Enabled };
        mute.CheckedChanged += (_, _) =>
        {
            sound.Enabled = !mute.Checked;
            localConfig = localConfig with { SoundEnabled = sound.Enabled };
            ConfigStore.Save(localConfig);
        };
        menu.Items.Add(mute);

        var volume = new ToolStripMenuItem("Volume");
        foreach (int pct in new[] { 25, 50, 75, 100 })
        {
            var item = new ToolStripMenuItem($"{pct}%");
            item.Click += (_, _) =>
            {
                sound.Volume = pct / 100f;
                localConfig = localConfig with { Volume = sound.Volume };
                ConfigStore.Save(localConfig);
            };
            volume.DropDownItems.Add(item);
        }
        menu.Items.Add(volume);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Application.Exit());
        return menu;
    }
}
