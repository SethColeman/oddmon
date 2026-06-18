# Config & Audio Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add percent-based volume with a tray slider, a self-documenting `config.json`, and user-selectable audio output device.

**Architecture:** Config and audio logic live in `Oddmon.Core`; the tray UI in `Oddmon.App`. Volume is stored as an integer percent and converted to the engine's native 0–1 at the App boundary. `config.json` gains a fixed comment header that the loader skips. Output device selection rebuilds only the NAudio output sink, keeping the mixer/providers intact.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`), WinForms, NAudio 2.2.1, System.Text.Json, xUnit.

## Global Constraints

- Target framework: `net10.0-windows`. No new NuGet dependencies (NAudio 2.2.1 already referenced).
- Volume default: **15** (percent). Output device default: **null** (Windows default).
- `config.json` is hand-editable; loader must tolerate `//` / `/* */` comments and trailing commas.
- Follow existing code style: file-scoped namespaces, `record` config, `ponytail:` comments for deliberate shortcuts.
- Branch: `feat/audio-settings`. Commit after each task.

---

### Task 1: Config — VolumePercent, OutputDevice, self-documenting save/load

**Files:**
- Modify: `src/Oddmon.Core/OddmonConfig.cs`
- Test: `src/Oddmon.Core.Tests/ConfigStoreTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `OddmonConfig.VolumePercent` (`int`, default 15), `OddmonConfig.OutputDevice` (`string?`, default null). `ConfigStore.Load`/`Save`/`SaveIfMissing` unchanged signatures; saved files now begin with a `//` header and load tolerates comments.

- [ ] **Step 1: Update existing round-trip test + add new failing tests**

In `src/Oddmon.Core.Tests/ConfigStoreTests.cs`, change the round-trip test's `Volume = 0.5f` to `VolumePercent = 50`, and add three tests:

```csharp
[Fact]
public void VolumePercent_DefaultsTo15() =>
    Assert.Equal(15, new OddmonConfig().VolumePercent);

[Fact]
public void LegacyVolumeKey_IsIgnored()
{
    string path = Path.Combine(Path.GetTempPath(), $"oddmon-legacy-{Guid.NewGuid():N}.json");
    try
    {
        File.WriteAllText(path, "{\"Volume\":0.3}"); // pre-percent format
        Assert.Equal(15, ConfigStore.Load(path).VolumePercent);
    }
    finally { File.Delete(path); }
}

[Fact]
public void Load_ToleratesCommentsAndTrailingCommas()
{
    string path = Path.Combine(Path.GetTempPath(), $"oddmon-cmt-{Guid.NewGuid():N}.json");
    try
    {
        File.WriteAllText(path, "// a comment\n{\n  \"VolumePercent\": 42,\n}");
        Assert.Equal(42, ConfigStore.Load(path).VolumePercent);
    }
    finally { File.Delete(path); }
}
```

Also update the existing `Save_ThenLoad_RoundTrips` body:

```csharp
var c = new OddmonConfig { DiskSensitivity = 12.5, VolumePercent = 50, SoundEnabled = false };
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Oddmon.slnx --filter FullyQualifiedName~ConfigStoreTests`
Expected: FAIL — `OddmonConfig` has no `VolumePercent` (compile error).

- [ ] **Step 3: Update the config record**

In `src/Oddmon.Core/OddmonConfig.cs`, replace the `Volume` property:

```csharp
    public int VolumePercent { get; init; } = 15;
```

(delete `public float Volume { get; init; } = 0.3f;`)

And add, next to `SoundSetPath`:

```csharp
    /// <summary>Output device friendly-name substring; null uses the Windows default (scope §4).</summary>
    public string? OutputDevice { get; init; }
```

- [ ] **Step 4: Add comment-tolerant load + documented save**

In `ConfigStore`, replace the `Opts` field and `Load(path)` / `Save(config, path)` members:

```csharp
    private static readonly JsonSerializerOptions WriteOpts = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // Written on every save so the docs survive a tray-menu rewrite (scope §4).
    private const string Header = """
// oddmon config — https://github.com/SethColeman/oddmon
// Edits apply on next launch. The tray menu rewrites this file; this header is
// preserved, but any comments you add elsewhere are not.
// VolumePercent      : 0-100        master volume, e.g. 15
// DiskSensitivity    : 0-100        disk-busy % to trigger LED/sound; lower = more sensitive, e.g. 8
// SoundEnabled       : true | false manual mute
// SoundSetPath       : null | "C:\\path\\to\\wavs"   null = bundled set
// OutputDevice       : null | "name substring"       null = Windows default, e.g. "Speakers"
// OverlayEnabled     : true | false ; OverlayX / OverlayY : int | null
// QuietHoursStart/End: "HH:mm" | null   wraps midnight, e.g. "22:00" / "07:00"
// Autostart          : true | false launch at login

""";

    public static OddmonConfig Load(string path)
    {
        try { return JsonSerializer.Deserialize<OddmonConfig>(File.ReadAllText(path), ReadOpts) ?? new(); }
        catch { return new(); } // missing or corrupt -> defaults
    }

    public static void Save(OddmonConfig config, string path) =>
        File.WriteAllText(path, Header + JsonSerializer.Serialize(config, WriteOpts));
```

(The parameterless `Save`, `SaveIfMissing` overloads, `Dir`, and `FilePath` are unchanged.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Oddmon.slnx --filter FullyQualifiedName~ConfigStoreTests`
Expected: PASS (all ConfigStoreTests).

- [ ] **Step 6: Commit**

```bash
git add src/Oddmon.Core/OddmonConfig.cs src/Oddmon.Core.Tests/ConfigStoreTests.cs
git commit -m "Config: VolumePercent + OutputDevice + self-documenting config.json"
```

---

### Task 2: AudioOutputs helper — device enumeration + pure name match

**Files:**
- Create: `src/Oddmon.Core/AudioOutputs.cs`
- Test: `src/Oddmon.Core.Tests/AudioOutputsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `AudioOutputs.Names() -> IReadOnlyList<string>` (active render-device friendly names; empty on failure)
  - `AudioOutputs.Match(IReadOnlyList<string> names, string? saved) -> string?` (pure)
  - `AudioOutputs.Resolve(string? saved) -> NAudio.CoreAudioApi.MMDevice?` (caller disposes)

- [ ] **Step 1: Write the failing tests**

Create `src/Oddmon.Core.Tests/AudioOutputsTests.cs`:

```csharp
using Oddmon.Core;

namespace Oddmon.Core.Tests;

public class AudioOutputsTests
{
    private static readonly string[] Devices =
        { "Speakers (Realtek Audio)", "Jabra SPEAK 510 USB", "Headphones" };

    [Fact]
    public void Match_Substring_CaseInsensitive() =>
        Assert.Equal("Speakers (Realtek Audio)", AudioOutputs.Match(Devices, "speakers"));

    [Fact]
    public void Match_ExactName() =>
        Assert.Equal("Jabra SPEAK 510 USB", AudioOutputs.Match(Devices, "Jabra SPEAK 510 USB"));

    [Fact]
    public void Match_NoMatch_ReturnsNull() =>
        Assert.Null(AudioOutputs.Match(Devices, "Bluetooth"));

    [Fact]
    public void Match_NullOrBlank_ReturnsNull()
    {
        Assert.Null(AudioOutputs.Match(Devices, null));
        Assert.Null(AudioOutputs.Match(Devices, "   "));
    }

    [Fact]
    public void Match_FirstOfMultiple() =>
        Assert.Equal("Speakers (Realtek Audio)", AudioOutputs.Match(Devices, "a")); // first containing 'a'

    [Fact]
    public void Names_NeverThrows() =>
        Assert.NotNull(AudioOutputs.Names()); // empty or populated, but never throws
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Oddmon.slnx --filter FullyQualifiedName~AudioOutputsTests`
Expected: FAIL — `AudioOutputs` does not exist (compile error).

- [ ] **Step 3: Implement the helper**

Create `src/Oddmon.Core/AudioOutputs.cs`:

```csharp
using NAudio.CoreAudioApi;

namespace Oddmon.Core;

/// <summary>Render-device discovery for output selection; names match Windows Sound settings.</summary>
public static class AudioOutputs
{
    /// <summary>Active render-device friendly names; empty if enumeration fails.</summary>
    public static IReadOnlyList<string> Names()
    {
        try
        {
            using var en = new MMDeviceEnumerator();
            return en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                     .Select(d => d.FriendlyName).ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>First name containing <paramref name="saved"/> (case-insensitive), or null
    /// when <paramref name="saved"/> is null/blank or nothing matches. Pure.</summary>
    public static string? Match(IReadOnlyList<string> names, string? saved)
    {
        if (string.IsNullOrWhiteSpace(saved)) return null;
        return names.FirstOrDefault(n => n.Contains(saved, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Active render <see cref="MMDevice"/> whose name matches, else null. Caller disposes.</summary>
    public static MMDevice? Resolve(string? saved)
    {
        if (string.IsNullOrWhiteSpace(saved)) return null;
        try
        {
            // ponytail: enumerator not disposed here — devices outlive it and the player owns the chosen one.
            var en = new MMDeviceEnumerator();
            return en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                     .FirstOrDefault(d => d.FriendlyName.Contains(saved, StringComparison.OrdinalIgnoreCase));
        }
        catch { return null; }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Oddmon.slnx --filter FullyQualifiedName~AudioOutputsTests`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Oddmon.Core/AudioOutputs.cs src/Oddmon.Core.Tests/AudioOutputsTests.cs
git commit -m "Add AudioOutputs: render-device enumeration + pure name match"
```

---

### Task 3: SeekSoundPlayer — rebuildable output sink + device selection

**Files:**
- Modify: `src/Oddmon.Core/SeekSoundPlayer.cs`

**Interfaces:**
- Consumes: `AudioOutputs.Resolve(string?)` from Task 2.
- Produces: `SeekSoundPlayer(..., string? soundSetDir = null, string? outputDevice = null)` constructor; `void SetOutputDevice(string? deviceName)`.

> No automated test: constructing an NAudio output requires a real audio device, which CI runners lack. The pure selection logic is covered by Task 2; this task is verified by build + manual smoke. Do not add a test that constructs `SeekSoundPlayer`.

- [ ] **Step 1: Add usings and convert the output field**

In `src/Oddmon.Core/SeekSoundPlayer.cs`, the top usings become:

```csharp
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
```

Change the output field and add device/state fields (replace `private readonly WaveOutEvent _output;`):

```csharp
    private IWavePlayer _output = null!; // assigned by RebuildOutput in the ctor
    private MMDevice? _device;
    private bool _started;
```

- [ ] **Step 2: Take the device in the constructor and build via RebuildOutput**

Change the constructor signature to add `outputDevice`:

```csharp
    public SeekSoundPlayer(Func<bool> isBusy, float volume = 0.3f, int sampleRate = 44100,
        double clickIntervalMs = 85.0, string? soundSetDir = null, string? outputDevice = null)
```

Replace these two lines:

```csharp
        _output = new WaveOutEvent();
        _output.Init(_master);
```

with:

```csharp
        RebuildOutput(outputDevice);
```

- [ ] **Step 3: Add RebuildOutput + SetOutputDevice**

Add these methods to the class (e.g. just below the constructor):

```csharp
    /// <summary>Switch playback to <paramref name="deviceName"/> (null/blank/missing = Windows default).
    /// Rebuilds only the output sink; the mixer and providers are untouched.</summary>
    public void SetOutputDevice(string? deviceName) => RebuildOutput(deviceName);

    private void RebuildOutput(string? deviceName)
    {
        // ponytail: no lock around the swap; a tray utility's rare device change can tolerate a brief gap.
        _output?.Stop();
        _output?.Dispose();
        _device?.Dispose();
        _device = AudioOutputs.Resolve(deviceName);

        if (_device is null)
        {
            _output = new WaveOutEvent();        // Windows default device
            _output.Init(_master);               // WaveOut accepts the mono-float master directly
        }
        else
        {
            try
            {
                var mix = _device.AudioClient.MixFormat; // device shared-mode format
                ISampleProvider chain = _master;          // mono float, _format.SampleRate
                if (mix.Channels >= 2) chain = new MonoToStereoSampleProvider(chain);
                if (mix.SampleRate != _format.SampleRate)
                    chain = new WdlResamplingSampleProvider(chain, mix.SampleRate);
                var wasapi = new WasapiOut(_device, AudioClientShareMode.Shared, false, 200);
                wasapi.Init(chain);
                _output = wasapi;
            }
            catch
            {
                _device?.Dispose();
                _device = null;
                _output = new WaveOutEvent();    // fall back to default on any failure
                _output.Init(_master);
            }
        }

        if (_started) _output.Play();
    }
```

- [ ] **Step 4: Track started state and dispose the device**

Change `Start()`:

```csharp
    public void Start()
    {
        _started = true;
        _output.Play();
        _timer.Start();
    }
```

Change `Dispose()`:

```csharp
    public void Dispose()
    {
        _timer.Dispose();
        _output?.Stop();
        _output?.Dispose();
        _device?.Dispose();
    }
```

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build Oddmon.slnx -v quiet`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Run the full unit suite (no regressions)**

Run: `dotnet test Oddmon.slnx --filter "Category!=Integration"`
Expected: PASS (all existing + Task 1/2 tests).

- [ ] **Step 7: Commit**

```bash
git add src/Oddmon.Core/SeekSoundPlayer.cs
git commit -m "SeekSoundPlayer: rebuildable output sink + device selection"
```

---

### Task 4: Tray UI — volume slider replacing the presets submenu

**Files:**
- Modify: `src/Oddmon.App/Program.cs`

**Interfaces:**
- Consumes: `OddmonConfig.VolumePercent` (Task 1); `SeekSoundPlayer.Volume` (existing 0–1).
- Produces: tray volume slider; no new exported API.

> Verified by build + manual smoke (WinForms).

- [ ] **Step 1: Convert volume at the player boundary**

In `src/Oddmon.App/Program.cs`, in the `SeekSoundPlayer` construction, change `config.Volume` to `config.VolumePercent / 100f`:

```csharp
        using var sound = new SeekSoundPlayer(
            () => monitor.Current != ActivityLevel.Idle && !mic.InCall && !config.InQuietHours(DateTime.Now),
            config.VolumePercent / 100f, soundSetDir: soundDir)
        {
            Enabled = config.SoundEnabled,
        };
```

- [ ] **Step 2: Replace the Volume submenu with a slider**

Delete this block (note: it uses the `Update(...)` helper, not `Save()`):

```csharp
        var volume = new ToolStripMenuItem("Volume");
        foreach (int pct in new[] { 25, 50, 75, 100 })
        {
            var item = new ToolStripMenuItem($"{pct}%");
            item.Click += (_, _) =>
            {
                sound.Volume = pct / 100f;
                Update(c => c with { Volume = sound.Volume });
            };
            volume.DropDownItems.Add(item);
        }
        menu.Items.Add(volume);
```

Replace it with:

```csharp
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
```

- [ ] **Step 3: Build**

Run: `dotnet build Oddmon.slnx -v quiet`
Expected: `Build succeeded. 0 Error(s)`. (The menu already persists via the `Update(...)` helper, defined near the top of `Main`; reuse it — do not reintroduce a `Save()` local.)

- [ ] **Step 4: Manual smoke**

Stop any running instance, then:

Run: `Get-Process oddmon -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project src/Oddmon.App`
- Right-click tray → confirm `Volume: NN%` label + slider appear (no 25/50/75/100 submenu).
- Drag the slider while the disk is busy → volume changes live.
- Re-open the menu → slider/label reflect the new value (persisted).
Stop it: `Get-Process oddmon | Stop-Process`.

- [ ] **Step 5: Commit**

```bash
git add src/Oddmon.App/Program.cs
git commit -m "Tray: volume slider (percent) replacing the presets submenu"
```

---

### Task 5: Tray UI — Output device submenu

**Files:**
- Modify: `src/Oddmon.App/Program.cs`

**Interfaces:**
- Consumes: `AudioOutputs.Names()` / `AudioOutputs.Match` (Task 2); `SeekSoundPlayer.SetOutputDevice` + `outputDevice` ctor param (Task 3); `OddmonConfig.OutputDevice` (Task 1).
- Produces: tray "Output device" submenu.

> Verified by build + manual smoke (requires a second output device).

- [ ] **Step 1: Pass the saved device into the player**

In the `SeekSoundPlayer` construction (modified in Task 4), add `outputDevice: config.OutputDevice`:

```csharp
        using var sound = new SeekSoundPlayer(
            () => monitor.Current != ActivityLevel.Idle && !mic.InCall && !config.InQuietHours(DateTime.Now),
            config.VolumePercent / 100f, soundSetDir: soundDir, outputDevice: config.OutputDevice)
        {
            Enabled = config.SoundEnabled,
        };
```

- [ ] **Step 2: Add the Output device submenu**

Immediately after the volume slider's `menu.Items.Add(new ToolStripControlHost(...))` line, add:

```csharp
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
```

- [ ] **Step 3: Build**

Run: `dotnet build Oddmon.slnx -v quiet`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Manual smoke**

Run: `Get-Process oddmon -ErrorAction SilentlyContinue | Stop-Process -Force; dotnet run --project src/Oddmon.App`
- Right-click tray → "Output device" lists "System default" + your real devices, with the current one checked.
- Pick a non-default device (e.g. laptop speakers) → while the disk is busy, sound moves to that device.
- Re-open menu → the chosen device is checked; restart the app → choice persists (`config.json` `OutputDevice`).
- Pick "System default" → returns to default.
Stop it: `Get-Process oddmon | Stop-Process`.

- [ ] **Step 5: Commit**

```bash
git add src/Oddmon.App/Program.cs
git commit -m "Tray: output-device submenu (pick + persist)"
```

---

### Task 6: Docs — README config table + tray menu, final verification

**Files:**
- Modify: `README.md`

**Interfaces:** none.

- [ ] **Step 1: Update the config table**

In `README.md`, in the Configuration table, replace the `Volume` row and add an `OutputDevice` row:

```markdown
| `VolumePercent` | Master sound volume, 0–100 | `15` |
```

and, after the `SoundSetPath` row:

```markdown
| `OutputDevice` | Playback device name (substring of the friendly name); `null` uses the Windows default | `null` |
```

- [ ] **Step 2: Update the tray menu section**

In `README.md`, under "Tray menu", replace the Volume bullet and add an Output device bullet:

```markdown
- **Volume** — slider, 0–100%
- **Output device** — choose the playback device (default: Windows default)
```

- [ ] **Step 3: Full build + test**

Run: `dotnet build Oddmon.slnx -v quiet; dotnet test Oddmon.slnx --filter "Category!=Integration"`
Expected: `Build succeeded` and all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "Docs: VolumePercent + OutputDevice in README"
```

---

## Self-Review

**Spec coverage:**
- Volume as percent + default 15 → Task 1 (config) + Task 4 (slider). ✓
- 1% slider in tray menu replacing presets → Task 4. ✓
- Self-documenting config header + comment-tolerant load → Task 1. ✓
- Output device config field + name matching → Task 1 (field) + Task 2 (match). ✓
- Output device engine (rebuildable sink, WASAPI, fallback) → Task 3. ✓
- Output device tray submenu (repopulate, radio-check, persist) → Task 5. ✓
- README updates → Task 6. ✓
- Tests: config round-trip/default/legacy/comments (Task 1), pure Match (Task 2). ✓

**Placeholder scan:** No TBD/TODO; every code step shows complete code. Manual-only tasks (3,4,5) state why automation is infeasible and give concrete manual steps. ✓

**Type consistency:** `VolumePercent` (int), `OutputDevice` (string?), `SetOutputDevice(string?)`, `AudioOutputs.Names()`/`Match(...)`/`Resolve(...)`, `RebuildOutput`, `_output: IWavePlayer`, `_device: MMDevice?` used consistently across tasks. App→engine volume conversion `VolumePercent / 100f` consistent in Tasks 4 and 5. ✓
