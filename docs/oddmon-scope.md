# oddmon — Project Scope: Retro Drive & Connection Sound Tray Utility

**Name:** `oddmon` — a play on **AUD**itory **MON**itor (and reads like "oddment," fitting a quirky nostalgia utility)
**Platform:** Windows 11 (Windows 10 22H2 as secondary target)
**Stack:** C# / .NET 8 (LTS), single-file self-contained `.exe`
**License:** MIT
**Status:** Draft scope for review — not yet started

---

## 1. Summary

A lightweight Windows tray utility (with an optional skeuomorphic desktop "front panel") that brings back the *auditory and visual cues* of older computers, tied to what the machine is actually doing:

1. **Simulated LEDs** — glowing retro indicators shown either in the system tray or on an optional always-on-top desktop "case front panel" (user's choice):
   - **HDD activity LED** — flashes on real disk read/write activity, like the old front-panel light.
   - **Turbo LED** — lit when the machine is in a high-performance state (on charger + performance power mode), dim/off when throttled (on battery + power-saver), echoing the old 386/486 "Turbo" button light.
2. **HDD seek sounds** — mechanical hard-drive clicks/seeks played in response to actual disk I/O.
3. **Connection "modem" sound** — the classic dial-up handshake plays while a connection is being established. Triggers (all configurable):
   - VPN connecting (FortiClient first, others configurable)
   - Wi-Fi connecting / reconnecting
4. **Meeting-aware auto-mute** — when you're in a Teams, Zoom, or any other call, the app **silences its own sounds** automatically, then resumes afterward. Applies to both HDD and modem sounds. (Visual LEDs keep working — they're silent.)

The goal is faithful, low-effort nostalgia that never embarrasses you in a meeting and never gets in the way.

---

## 2. Goals & Non-Goals

### Goals
- Sounds and the tray light map to **real** system activity, not random playback.
- Zero-noise during calls — automatic, no manual toggling needed.
- Fully configurable triggers, volumes, sound sets, and quiet hours.
- Genuinely open source (MIT), easy to build, easy to contribute to.
- Tiny footprint: low CPU, low memory, no telemetry.

### Non-Goals (v1)
- Cross-platform (macOS/Linux). Architecture should not *preclude* it, but v1 is Windows-only.
- Microphone control / muting your actual mic (explicitly out of scope — auto-mute only silences the app).
- Bundling copyrighted sound packs. Ship CC0/royalty-free samples and let users supply their own.
- Per-application disk attribution ("which app is hitting the disk"). Whole-system activity is enough for v1.

---

## 3. Feature Detail & Detection Approaches

### 3.1 LED display — tray and/or desktop "front panel"
The simulated LEDs render in **either or both** of two modes (user's choice in Settings):
- **System tray mode:** compact tray icons — an animated HDD-activity icon plus a Turbo-state icon. Zero screen real estate.
- **Desktop front-panel overlay:** a small, skeuomorphic, always-on-top widget styled like a retro PC case front — glowing HDD and Turbo LEDs with labels. Draggable, snap-to-corner, adjustable opacity/size, click-through optional. Built to hold more LEDs later (e.g. network, power).

Shared rendering concerns: smooth glow/fade animation, configurable colors, per-LED enable/disable, and a "dim when idle" option. The overlay should be lightweight (no per-frame CPU spikes) and remember its position.

### 3.2 HDD activity LED
- Reflects current disk state: **idle**, **read** (e.g. green), **write** (e.g. red), **mixed** (amber) — both as a tray icon frame and as a glowing LED on the panel.
- Driven by Windows performance counters in the `PhysicalDisk` / `LogicalDisk` category: `Disk Reads/sec`, `Disk Writes/sec`, `% Disk Time`. Poll at ~10–20 Hz.
- Threshold + smoothing so the light doesn't seizure on micro-activity. Configurable sensitivity. (Shares the same DiskMonitor that drives the seek sounds.)

### 3.3 Turbo LED (power-profile aware)
A nostalgic "Turbo" light that reflects whether the machine is currently running flat-out or throttled — mapped from AC/battery status and the Windows power mode.

**Default mapping (configurable):**

| AC/Battery | Power mode (overlay) | Turbo LED |
|------------|----------------------|-----------|
| On charger | Best Performance | **ON (bright)** |
| On charger | Balanced | ON (dim) *(configurable)* |
| On charger | Best Power Efficiency | OFF |
| On battery | Best Performance | ON (dim) *(configurable)* |
| On battery | Balanced / Power Saver | **OFF** |

- The user can redefine which states count as "turbo" (e.g. treat Balanced-on-AC as on or off).
- **Detection (C#/.NET):**
  - AC vs battery + battery saver: `GetSystemPowerStatus` / `SystemInformation.PowerStatus`, with live updates via `SystemEvents.PowerModeChanged`.
  - Power mode slider / overlay (Best efficiency / Balanced / Best performance): `PowerGetEffectiveOverlayScheme` (powrprof.dll) → maps known overlay GUIDs to a mode; optionally `PowerGetActiveScheme` for the classic power plan.
  - Subscribe to power-setting notifications so the LED updates instantly when you plug in / change modes.
- Right-click menu (tray) / panel context menu: enable/disable each LED, open Settings, quit.

### 3.4 HDD seek sounds
- Short one-shot WAV clicks triggered when disk activity crosses a threshold; intensity/frequency of clicks scales with I/O rate.
- Optional low ambient "platter spin" loop (toggle), plus "clicks only" mode — matching the behavior people liked in DiskClick.
- **Sound sets** = a folder of WAVs (e.g. `seek.wav`, `idle.wav`, `heavy.wav`). Ship a few CC0 sets; users can drop in their own.
- Audio engine must mix/overlap rapid one-shots without stutter.

**Detection options (documented trade-offs):**
- *Performance counters* (default): simple, no admin rights, good enough to know "disk is busy now."
- *ETW kernel disk I/O provider* (advanced/optional): per-I/O events for more authentic seek granularity, but needs elevation and more code. Offer as an opt-in "high-fidelity" mode later.

### 3.5 Connection / modem sound
A generic **Connection Monitor** with pluggable *triggers*. When a trigger enters the "connecting" state, play the dial-up handshake; when it reaches "connected," play the connect chime (or just stop). If it fails/times out, stop and optionally play a busy/failure tone.

**Triggers (v1):**
- **VPN (FortiClient, configurable):** Detecting the *connecting* phase precisely is the hardest part of the project — see Risks. Approaches, best-effort combined:
  - Watch for the VPN virtual network adapter appearing/coming up (`NetworkChange.NetworkAddressChanged`, `NetworkInterface` enumeration). Adapter-up ≈ "connected."
  - Watch the FortiClient process/service and, where available, its log files or service state for the connecting transition.
  - Config model = a **connection profile**: `{ name, detectionMethod (process | adapter | route | log), match pattern, connectingTimeout }`. FortiClient ships as a default profile; users add others (e.g. OpenVPN, Cisco AnyConnect).
- **Wi-Fi connecting:** subscribe to network/adapter state changes (and optionally the WLAN API / `netsh`/WlanApi events) to detect the wireless adapter moving into a connecting state, then connected.

All connection sounds also obey the meeting auto-mute gate.

### 3.6 Meeting-aware auto-mute (applies to all sounds)
The key reliability feature. v1 detection strategy, in order of robustness:
- **Microphone-in-use detection (primary, app-agnostic):** enumerate WASAPI capture sessions on the communications endpoint; if any session is `Active`, you're almost certainly in a call. This covers Teams, Zoom, Meet, Discord, Slack huddles, etc. without hard-coding app names.
- **Process/window heuristics (secondary, reduces false positives):** known call apps running (`ms-teams.exe`, `Zoom.exe`, etc.) + active mic.
- **Manual override:** a "Mute sounds" tray toggle and a global hotkey, plus **Quiet Hours** schedule.

When the meeting gate is active, the **MuteCoordinator** suppresses the audio engine output (the tray light keeps working — it's silent anyway). Sounds resume when the call ends.

---

## 4. Architecture

Modular core with independent **monitors** emitting events to a central coordinator:

```
┌─────────────────────────────────────────────────────┐
│                     App Host                          │
│      (lifecycle, config, autostart, settings UI)      │
└───────────────┬───────────────────────────────────────┘
                │ events
   ┌─────────┬──┴────────┬────────────┬──────────────┐
   ▼         ▼           ▼            ▼              ▼
DiskMonitor PowerMonitor ConnectionMonitor MeetingMonitor (future)
   │         │ (AC + mode) │(VPN / Wi-Fi)  │(mic-in-use)
   │         │            │               │
   ├─────────┴────────────┼───────────────┘ gate
   ▼ visual               ▼ sound          ▼
┌──────────────┐   ┌──────────────┐  ┌──────────────┐
│ LedController│   │ AudioEngine  │◄─│MuteCoordinator│
│ tray + panel │   │ (NAudio mix) │  └──────────────┘
└──────────────┘   └──────────────┘
```

- **DiskMonitor** → activity-level events → HDD LED animation + seek-sound triggers.
- **PowerMonitor** → AC/battery + power-mode change events → Turbo LED state.
- **ConnectionMonitor** → connecting/connected/failed events per profile → modem sounds.
- **MeetingMonitor** → in-call true/false → MuteCoordinator gates AudioEngine.
- **MuteCoordinator** centralizes all reasons to be silent (meeting, quiet hours, manual mute, feature disabled). Gates audio only — LEDs stay live.
- **LedController** drives both the tray icons and the desktop front-panel overlay from monitor state.
- **AudioEngine** wraps NAudio; one mixer, supports overlapping one-shots + ambient loop, master + per-feature volume.

### Suggested libraries (all permissively licensed)
- **NAudio** (MIT) — low-latency WAV playback, mixing, WASAPI session enumeration (covers both audio output *and* mic-in-use detection).
- **Hardcodet.NotifyIcon.Wpf** (CPOL/MIT-ish) *or* WinForms `NotifyIcon` (built-in) — tray icon + menu. WinForms `NotifyIcon` keeps deps minimal.
- **System.Diagnostics.PerformanceCounter** (built-in) — disk activity.
- **System.Net.NetworkInformation** + **WlanApi/Managed Wifi** — adapter & Wi-Fi state.
- **powrprof.dll** P/Invoke (`PowerGetEffectiveOverlayScheme`) + `Microsoft.Win32.SystemEvents` — power mode & AC/battery for the Turbo LED.
- **WPF** for the desktop front-panel overlay (transparent, always-on-top, glow effects) — even if the tray uses WinForms `NotifyIcon`.
- **System.Text.Json** (built-in) — config.

### Configuration
- Single `config.json` in `%APPDATA%\Oddmon\`. Settings UI (right-click → Settings) writes to it; hand-editing also supported.
- Includes: per-feature on/off, sensitivity, master/per-feature volume, sound-set paths, connection profiles, meeting-detection mode, quiet hours, autostart.

### Distribution & autostart
- `dotnet publish` single-file, self-contained (no .NET install needed). Optional MSIX or Inno Setup installer later.
- Autostart via a per-user **Startup folder** shortcut (preferred — transparent and user-removable) or `HKCU\...\Run`. *Persistence note:* this makes the app launch at login; it's a benign user-level autostart, surfaced as an opt-in toggle in Settings, not silent.

---

## 5. Milestones

| # | Milestone | Deliverable |
|---|-----------|-------------|
| M0 | Project setup | Repo, MIT license, README, .NET 8 solution, GitHub Actions build CI |
| M1 | HDD activity LED | Animated tray icon reflecting live disk read/write activity |
| M2 | HDD sound engine | Seek clicks tied to disk I/O; sound sets; volume; clicks-only/ambient modes |
| M3 | Meeting auto-mute | Mic-in-use detection + MuteCoordinator gating all audio |
| M4 | Turbo LED | PowerMonitor (AC/battery + power mode) → Turbo LED with configurable mapping |
| M5 | Desktop front-panel overlay | Optional WPF always-on-top LED panel (HDD + Turbo), draggable, snap, opacity |
| M6 | Connection sounds | VPN (FortiClient) + Wi-Fi connecting detection → modem handshake; configurable profiles |
| M7 | Settings UI + config | Settings window, `config.json`, quiet hours, autostart toggle, display-mode choice |
| M8 | Polish & release | Packaging, default CC0 sound sets, docs, signed/first release, issue templates |

A natural MVP is **M1 + M2 + M3 + M4** — the HDD and Turbo LEDs plus polite-in-meetings sounds, all in the tray. The desktop overlay (M5) and connection/modem sounds (M6) are fast follows.

---

## 6. Key Risks & Open Questions

- **Catching the VPN "connecting" phase precisely.** FortiClient doesn't expose a clean public "now connecting" signal. Best-effort detection (adapter state + process/log heuristics) may sometimes only catch "connected." Mitigation: configurable profiles; allow a manual "play on VPN launch" mode as fallback. *This is the riskiest piece — worth a spike early.*
- **Meeting detection false positives/negatives.** Voice typing or a music app using the mic could read as "in a call." Mitigation: combine mic-in-use with known-app heuristics; manual override always available.
- **Performance-counter fidelity vs. ETW.** Counters are easy but coarse; ETW is authentic but needs admin. Ship counters in v1, ETW as opt-in later.
- **Audio sample licensing.** Must ship only CC0/royalty-free HDD and dial-up samples, or generate/record originals. Don't bundle anything copyrighted. (The dial-up handshake melody itself is a real-world signal, but specific recordings can be copyrighted — source carefully.)
- **Audio latency/overlap.** Rapid overlapping one-shots must not stutter; validate NAudio mixer settings early.
- **Power-mode detection variance.** Some OEM laptops (Lenovo/Dell/HP) override the standard Windows power-mode slider with their own GUIDs/utilities. `PowerGetEffectiveOverlayScheme` covers the standard cases; the Turbo mapping is user-configurable as a fallback for non-standard overlays.

---

## 7. Suggested Repo Layout

```
oddmon/
├─ src/
│  ├─ Oddmon.App/             # tray host, settings UI, entry point
│  ├─ Oddmon.Core/            # monitors, coordinator, audio engine
│  └─ Oddmon.Core.Tests/      # unit tests
├─ assets/
│  ├─ icons/                  # idle/read/write/mixed + turbo on/off tray frames
│  ├─ panel/                  # front-panel overlay art, LED glow sprites
│  └─ sounds/                 # default CC0 sound sets
├─ docs/
├─ .github/workflows/build.yml
├─ LICENSE  (MIT)
└─ README.md
```

---

## 8. Next Steps
1. Confirm scope (this doc) and name.
2. Early spike: prototype VPN-connecting detection against FortiClient (highest risk).
3. Stand up M0 repo scaffold + CI.
4. Build MVP (M1–M3).
