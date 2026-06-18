# Config & audio settings — design

Date: 2026-06-18
Status: approved (design); implementation pending

## Goal

Three related user-facing settings changes to oddmon:

1. Volume expressed and edited as a **percent**, set via a **slider** in the tray menu.
2. **Self-documenting `config.json`** — a comment header listing every field, its valid
   values, and an example, that survives the tray menu rewriting the file.
3. User-selectable **audio output device** (e.g. laptop speakers instead of the Jabra
   conference device), chosen from a tray submenu and remembered.

No new dependencies — NAudio 2.2.1 (already referenced) covers device enumeration and
WASAPI playback.

## Affected components

- `Oddmon.Core/OddmonConfig.cs` — config record (`VolumePercent`, `OutputDevice`) and
  `ConfigStore` (documented header on save, comment-tolerant load).
- `Oddmon.Core/SeekSoundPlayer.cs` — rebuildable output sink + device selection.
- `Oddmon.Core/AudioOutputs.cs` (new) — render-device enumeration + name matching
  (the matching logic is a pure, unit-testable function).
- `Oddmon.App/Program.cs` — tray menu: volume slider replacing the presets submenu, and
  an "Output device" submenu.
- `README.md` — config table updates.

---

## 1. Volume as percent + tray slider

### Config
- Rename `Volume` (`float`, 0–1) → `VolumePercent` (`int`, 0–100), default **15**.
- Existing files contain `"Volume": 0.3`. After the rename that key is an unknown member
  and is ignored on deserialize, so old configs fall back to the 15% default. Acceptable
  (~6 internal installs); noted in the README.

### Engine
- `SeekSoundPlayer` keeps its natural 0–1 `Volume` property (it is an audio engine; 0–1
  is the correct internal unit). The App converts at the boundary: `VolumePercent / 100f`.

### UI
- Remove the `Volume` submenu (the 25/50/75/100 items).
- Add, in the tray `ContextMenuStrip`:
  - A label item `Volume: 15%` (disabled menu item, text updated live).
  - A `TrackBar` (`Minimum = 0`, `Maximum = 100`, small/large step = 1) hosted via
    `ToolStripControlHost`, initialized to `VolumePercent`.
- Behaviour:
  - On `Scroll` / value change: set `sound.Volume = value / 100f` and update the label —
    **live**, no file write.
  - On drag release (`MouseUp` on the TrackBar): persist once via
    `Update(c => c with { VolumePercent = value })`.
  - Rationale: live audio feedback while dragging, a single disk write when done, so we
    do not hammer `config.json` on every tick.

### Caveat
A `TrackBar` inside a tray `ContextMenuStrip` (via `ToolStripControlHost`) is a slightly
unusual host. It works, but if it feels janky in practice the fallback is to move the
slider onto the desktop panel (`OverlayForm`). Validated during build.

---

## 2. Self-documenting config.json

### Save
`ConfigStore.Save` writes a fixed `//`-comment header above the JSON on **every** save, so
the documentation always survives a tray-menu rewrite. The header documents each field,
its valid range/values, and an example. Illustrative shape:

```
// oddmon config — https://github.com/SethColeman/oddmon
// Edits apply on next launch. The tray menu rewrites this file; this header is preserved,
// but any comments you add elsewhere are not.
// VolumePercent      : 0–100        master volume, e.g. 15
// DiskSensitivity    : 0–100        disk-busy % to trigger LED/sound; lower = more sensitive, e.g. 8
// SoundEnabled       : true | false manual mute
// SoundSetPath       : null | "C:\\path\\to\\wavs"   null = bundled set
// OutputDevice       : null | "name substring"       null = Windows default, e.g. "Speakers"
// OverlayEnabled     : true | false ; OverlayX / OverlayY : int | null
// QuietHoursStart/End: "HH:mm" | null   wraps midnight, e.g. "22:00" / "07:00"
// Autostart          : true | false launch at login
{
  "VolumePercent": 15,
  ...
}
```

`System.Text.Json` cannot emit comments, so the header is concatenated as a string in
front of the serialized JSON.

### Load
Parse with `JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip,
AllowTrailingCommas = true }` so both oddmon's header and any comments the user adds are
skipped rather than throwing. (Load currently uses default options; add a dedicated
read-options instance.)

### Trade-off
oddmon **owns** the header. The valuable documented values always persist; the user's own
free-form comments are dropped on the next menu write. Stated in the header itself.

---

## 3. Output device selection

### Config
- Add `OutputDevice` (`string?`, default `null`). `null` = Windows default output.
- Stores the device **friendly name**. At runtime oddmon plays to the first **active
  render** device whose friendly name contains the stored string (readable, consistent
  with the JSON-docs theme, and tolerant of minor name changes). Missing / unplugged →
  fall back to the Windows default.

### New helper — `AudioOutputs` (Core)
- `IReadOnlyList<string> Names()` — active render-device friendly names (WASAPI via
  `MMDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)`),
  wrapped in try/catch → empty list on failure.
- `static string? Match(IReadOnlyList<string> names, string? saved)` — **pure**: returns
  the first name containing `saved` (case-insensitive), or `null` if `saved` is null/blank
  or no match. This is the unit-tested core of the feature; keeps NAudio in Core.
- A resolve helper returning the `MMDevice` for a chosen name (used by the player).

### Engine — `SeekSoundPlayer`
- Make the output sink rebuildable. `SetOutputDevice(string? name)`:
  `_output.Stop()` → `_output.Dispose()` → create a new sink → `Init(_master)` →
  `Play()`. The mixer / master / sample providers are unchanged; only the sink is swapped
  (you cannot retarget a live NAudio output).
- `name == null` or no match → default sink = today's `WaveOutEvent` (unchanged path).
- A named device → `WasapiOut(mmDevice, Shared, ...)`. The source is IEEE-float mono
  44.1 kHz; adapt to the device with `MonoToStereoSampleProvider` + a resampler
  (`WdlResamplingSampleProvider` / `MediaFoundationResampler`) to the device mix format as
  needed so `Init` does not throw.
- Constructor takes the initial `OutputDevice` so startup honours the saved choice.

### UI
- Add an "Output device" tray submenu, repopulated on `DropDownOpening` (catches
  hot-plug). Items: "System default" + one per active render device, radio-checked to the
  current selection.
- Click → `sound.SetOutputDevice(name)` then `Update(c => c with { OutputDevice = name })`
  ("System default" sets `null`).

### Error handling
- Enumeration throws → empty list → submenu shows only "System default".
- Switching to a device that fails to open → catch, revert to the default sink, keep
  running. Optional: a tray balloon noting the fallback.
- Startup with a saved device that is absent → default sink.

---

## Testing

- **Config** (`ConfigStoreTests`): `VolumePercent` round-trips; default is 15; a legacy
  `"Volume"` key is ignored; a file written with the comment header (and one with extra
  user comments) loads without error.
- **Device matching** (`AudioOutputsTests`, new): pure `Match()` cases — exact, substring,
  case-insensitive, no-match → null, null/blank saved → null, first-of-multiple.
- **Slider UI** and **live device switch**: manual (WinForms + real hardware).
- Update the existing `ConfigStoreTests.Save_ThenLoad_RoundTrips` (currently uses
  `Volume = 0.5f`) to the new field.

## Out of scope

- Per-sound (click vs loop) device routing — single output for all oddmon audio.
- Exclusive-mode / low-latency WASAPI — shared mode is fine for effect playback.
- Persisting the exact device ID (vs. name) — revisit only if name collisions bite.
