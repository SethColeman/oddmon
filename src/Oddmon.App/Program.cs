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
        // Re-load from disk before each write so a menu toggle merges onto hand edits
        // instead of clobbering them with the stale launch-time snapshot.
        void Update(Func<OddmonConfig, OddmonConfig> change) =>
            ConfigStore.Save(config = change(ConfigStore.Load()));

        using var monitor = new DiskMonitor(config.DiskSensitivity);
        using var power = new PowerMonitor();
        using var mic = new MicMonitor();
        // Mute all sounds while the mic is in use (you're in a call). LEDs keep working.
        string soundDir = config.SoundSetPath ?? Path.Combine(AppContext.BaseDirectory, "sounds");
        // Silent while idle, in a call (mic in use), or during quiet hours. LEDs keep working.
        using var sound = new SeekSoundPlayer(
            () => monitor.Current != ActivityLevel.Idle && !mic.InCall && !config.InQuietHours(DateTime.Now),
            config.VolumePercent / 100f, soundSetDir: soundDir, outputDevice: config.OutputDevice)
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
            Update(c => c with { OverlayX = overlay.Location.X, OverlayY = overlay.Location.Y });
        };

        var menu = new ContextMenuStrip();

        var mute = new ToolStripMenuItem("Mute sounds") { CheckOnClick = true, Checked = !config.SoundEnabled };
        mute.CheckedChanged += (_, _) =>
        {
            sound.Enabled = !mute.Checked;
            Update(c => c with { SoundEnabled = sound.Enabled });
        };
        menu.Items.Add(mute);

        var volLabel = new ToolStripMenuItem($"Volume: {config.VolumePercent}%") { Enabled = false };
        menu.Items.Add(volLabel);

        var slider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = config.VolumePercent,
            TickStyle = TickStyle.None,
            AutoSize = false,
            Width = 180,
            Height = 28,
        };
        slider.ValueChanged += (_, _) =>
        {
            sound.Volume = slider.Value / 100f;       // live
            volLabel.Text = $"Volume: {slider.Value}%";
        };
        // Persist once when the user finishes (drag release or keyboard), not on every tick.
        void PersistVolume(object? _, EventArgs __) => Update(c => c with { VolumePercent = slider.Value });
        slider.MouseUp += PersistVolume;
        slider.KeyUp += PersistVolume;
        menu.Items.Add(new ToolStripControlHost(slider) { AutoSize = false, Width = 190, Height = 30 });

        var output = new ToolStripMenuItem("Output device");
        output.DropDownOpening += (_, _) =>
        {
            output.DropDownItems.Clear();

            var def = new ToolStripMenuItem("System default")
            {
                Checked = string.IsNullOrWhiteSpace(config.OutputDevice),
            };
            def.Click += (_, _) =>
            {
                sound.SetOutputDevice(null);
                Update(c => c with { OutputDevice = null });
            };
            output.DropDownItems.Add(def);

            var names = AudioOutputs.Names();
            string? active = AudioOutputs.Match(names, config.OutputDevice);
            foreach (var name in names)
            {
                var item = new ToolStripMenuItem(name) { Checked = name == active };
                item.Click += (_, _) =>
                {
                    sound.SetOutputDevice(name);
                    Update(c => c with { OutputDevice = name });
                };
                output.DropDownItems.Add(item);
            }
        };
        menu.Items.Add(output);

        var panel = new ToolStripMenuItem("Show panel") { CheckOnClick = true, Checked = config.OverlayEnabled };
        panel.CheckedChanged += (_, _) =>
        {
            if (panel.Checked) overlay.Show(); else overlay.Hide();
            Update(c => c with { OverlayEnabled = panel.Checked });
        };
        menu.Items.Add(panel);

        menu.Items.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = Autostart.IsEnabled() };
        autostart.CheckedChanged += (_, _) =>
        {
            Autostart.Set(autostart.Checked);
            Update(c => c with { Autostart = autostart.Checked });
        };
        menu.Items.Add(autostart);

        // The config file is the "settings window": sensitivity, sound-set path and
        // quiet hours are hand-edited there (scope §4). Edits apply on next launch.
        menu.Items.Add("Edit settings (config.json)…", null, (_, _) =>
        {
            ConfigStore.SaveIfMissing(config); // create on first open only; never clobber hand edits
            // Open in Notepad directly: ShellExecute silently no-ops when .json has no
            // file association, so we don't depend on one. ArgumentList quotes the path.
            var psi = new System.Diagnostics.ProcessStartInfo("notepad.exe");
            psi.ArgumentList.Add(ConfigStore.FilePath);
            System.Diagnostics.Process.Start(psi);
        });

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
