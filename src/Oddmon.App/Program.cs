using System.Drawing;
using System.Windows.Forms;
using Oddmon.Core;

namespace Oddmon.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
#pragma warning disable WFO5001 // SetColorMode is experimental on some SDKs
        Application.SetColorMode(SystemColorMode.System); // follow OS light/dark for the tray menu
#pragma warning restore WFO5001

        var config = ConfigStore.Load();
        void Save() => ConfigStore.Save(config);

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

        using var overlay = new OverlayForm();
        if (config.OverlayX is int ox && config.OverlayY is int oy)
            overlay.Location = new Point(ox, oy);
        else
        {
            var wa = Screen.PrimaryScreen!.WorkingArea;
            overlay.Location = new Point(wa.Right - overlay.Width - 16, wa.Top + 16);
        }
        overlay.ResizeEnd += (_, _) =>
        {
            config = config with { OverlayX = overlay.Location.X, OverlayY = overlay.Location.Y };
            Save();
        };

        var menu = new ContextMenuStrip();

        var mute = new ToolStripMenuItem("Mute sounds") { CheckOnClick = true, Checked = !config.SoundEnabled };
        mute.CheckedChanged += (_, _) =>
        {
            sound.Enabled = !mute.Checked;
            config = config with { SoundEnabled = sound.Enabled };
            Save();
        };
        menu.Items.Add(mute);

        var volume = new ToolStripMenuItem("Volume");
        foreach (int pct in new[] { 25, 50, 75, 100 })
        {
            var item = new ToolStripMenuItem($"{pct}%");
            item.Click += (_, _) =>
            {
                sound.Volume = pct / 100f;
                config = config with { Volume = sound.Volume };
                Save();
            };
            volume.DropDownItems.Add(item);
        }
        menu.Items.Add(volume);

        var panel = new ToolStripMenuItem("Show panel") { CheckOnClick = true, Checked = config.OverlayEnabled };
        panel.CheckedChanged += (_, _) =>
        {
            if (panel.Checked) overlay.Show(); else overlay.Hide();
            config = config with { OverlayEnabled = panel.Checked };
            Save();
        };
        menu.Items.Add(panel);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Application.Exit());

        using var trayIcon = new NotifyIcon
        {
            Icon = TrayIconFactory.Create(ActivityLevel.Idle, TurboState.Off),
            Text = "oddmon — disk idle · turbo off",
            Visible = true,
            ContextMenuStrip = menu,
        };

        // Monitors raise on timer threads; marshal back to the UI thread for the UI.
        using var sync = new Control();
        sync.CreateControl();

        void Refresh() => sync.BeginInvoke(() =>
        {
            var old = trayIcon.Icon;
            trayIcon.Icon = TrayIconFactory.Create(monitor.Current, power.Current);
            old?.Dispose();
            trayIcon.Text = $"oddmon — disk {monitor.Current.ToString().ToLowerInvariant()} · " +
                            $"turbo {power.Current.ToString().ToLowerInvariant()}";
            overlay.SetActivity(monitor.Current);
            overlay.SetTurbo(power.Current);
        });

        monitor.LevelChanged += _ => Refresh();
        power.TurboChanged += _ => Refresh();

        if (config.OverlayEnabled)
            overlay.Show();

        monitor.Start();
        power.Start();
        mic.Start();
        sound.Start();
        Application.Run();

        trayIcon.Visible = false;
    }
}
