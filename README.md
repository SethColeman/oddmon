# oddmon

An **AUD**itory (and visual) **MON**itor for Windows — a lightweight tray utility
that brings back the sounds and front-panel lights of older PCs, tied to what your
machine is actually doing. It stays quiet automatically while you're in a call.

See [`docs/oddmon-scope.md`](docs/oddmon-scope.md) for the full scope.

## Features

Working today:

- **HDD activity LED** — a tray LED that lights green (read) / red (write) / amber
  (mixed) on real disk activity, gated on `% Idle Time` so background noise doesn't
  trigger it. Sensitivity is configurable.
- **Turbo LED** — a power-aware LED reflecting AC/battery + the Windows power-mode
  slider (bright / dim / off), echoing the old 386/486 Turbo light.
- **Combined tray icon** — both LEDs stacked in a single tray slot; the menu follows
  the OS light/dark theme.
- **HDD sounds** — a real (CC0) hard-drive recording loops while the disk is busy,
  with a synthesized fallback when no sound set is present.
- **Meeting auto-mute** — sounds go silent while any app is using the microphone
  (you're in a call); the LEDs keep working.
- **Desktop panel** — an optional always-on-top widget showing both LEDs with labels,
  draggable, with its position remembered.
- **Quiet hours** — a daily window (set in `config.json`) where sounds stay silent on
  top of the meeting auto-mute and manual mute.
- **Start with Windows** — opt-in launch at login (per-user, removable from Task
  Manager → Startup).

Connection / dial-up modem sounds (M6) were dropped — the VPN "connecting" phase
isn't reliably detectable without admin (see
[docs/oddmon-scope.md](docs/oddmon-scope.md) §6).

## Build & run

Requires the **.NET 10 SDK** (current LTS).

```sh
dotnet build Oddmon.slnx
dotnet test Oddmon.slnx                                   # unit tests
dotnet test Oddmon.slnx --filter Category=Integration     # live disk test (manual)
dotnet run --project src/Oddmon.App
```

> The original scope named .NET 8; the project targets **.NET 10** (current LTS,
> the installed SDK). It uses the new `.slnx` solution format.

## Tray menu

Right-click the tray icon:

- **Mute sounds** — manual mute toggle
- **Volume** — slider, 0–100%
- **Disk threshold** — slider, 0–100%; the disk-busy % that lights the LED / plays sound (lower = more sensitive)
- **Output device** — choose the playback device (default: Windows default)
- **Show panel** — toggle the desktop LED panel
- **Start with Windows** — opt-in launch at login
- **Edit settings (config.json)…** — opens `config.json` in Notepad

All persist across restarts. Edits made directly in `config.json` apply on next launch.

## Configuration

Settings live in `config.json` at `%APPDATA%\Oddmon\`, written by the tray menu and
**hand-editable**:

| Key | Meaning | Default |
|-----|---------|---------|
| `DiskSensitivity` | Disk-busy % to light the LED / play sound; lower = more sensitive | `8` |
| `VolumePercent` | Master sound volume, 0–100 | `15` |
| `SoundEnabled` | Sounds on/off (manual mute) | `true` |
| `SoundSetPath` | Folder of WAV clips; `null` uses the bundled set | `null` |
| `OutputDevice` | Playback device name (substring of the friendly name); `null` uses the Windows default | `null` |
| `OverlayEnabled` / `OverlayX` / `OverlayY` | Desktop panel visibility & position | off |
| `QuietHoursStart` / `QuietHoursEnd` | Silence window as `"HH:mm"`; wraps midnight (e.g. `"22:00"`–`"07:00"`); `null` disables | `null` |
| `Autostart` | Launch at login (mirrors the tray toggle) | `false` |

### Custom sound sets

Drop `.wav` files in a folder and point `SoundSetPath` at it (or replace the WAV in
`assets/sounds/`). A recording longer than a click loops while the disk is busy; with
no playable WAVs, oddmon falls back to synthesized clicks. Bundle only CC0 / public-
domain audio — see [`assets/sounds/CREDITS.md`](assets/sounds/CREDITS.md).

## Layout

```
oddmon/
├─ src/
│  ├─ Oddmon.App/         # tray host, desktop panel, entry point
│  ├─ Oddmon.Core/        # monitors, audio engine, config
│  └─ Oddmon.Core.Tests/  # unit + integration tests
├─ assets/sounds/         # default CC0 sound set (+ CREDITS.md)
├─ docs/                  # scope and design notes
└─ .github/              # CI (build+test), release workflow, issue templates
```

## Releases

Push a `vX.Y.Z` tag and the [`release` workflow](.github/workflows/release.yml)
publishes a single-file, self-contained win-x64 build (no .NET install needed),
zips it with the `sounds/` folder, and creates a GitHub release:

```sh
git tag v0.2.0 && git push origin v0.2.0
```

Builds are **not code-signed** (no certificate yet), so Windows SmartScreen may warn
on first run — "More info → Run anyway".

## License

[MIT](LICENSE) © 2026 Seth. Bundled audio is CC0 — see
[`assets/sounds/CREDITS.md`](assets/sounds/CREDITS.md).
