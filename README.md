# oddmon

An **AUD**itory (and visual) **MON**itor for Windows — a lightweight tray utility
that brings back the sounds and front-panel lights of older PCs, tied to what your
machine is actually doing: HDD seek clicks on real disk I/O, a glowing HDD activity
LED, a power-aware "Turbo" LED, and the classic dial-up handshake when a VPN or
Wi-Fi connection is coming up. It stays quiet automatically while you're in a call.

See [`docs/oddmon-scope.md`](docs/oddmon-scope.md) for the full scope.

## Status

Early scaffold (milestone **M0**): solution, projects, and CI are in place. No
features are implemented yet — see the milestones in the scope doc.

## Build & run

Requires the **.NET 10 SDK** (current LTS).

```sh
dotnet build Oddmon.slnx
dotnet test Oddmon.slnx
dotnet run --project src/Oddmon.App
```

> Note: the scope doc originally named .NET 8; the project targets **.NET 10**,
> which is the current LTS and the installed SDK.

## Layout

```
oddmon/
├─ src/
│  ├─ Oddmon.App/         # tray host, settings UI, entry point
│  ├─ Oddmon.Core/        # monitors, coordinator, audio engine
│  └─ Oddmon.Core.Tests/  # unit tests
├─ assets/                # icons, panel art, default CC0 sound sets
├─ docs/                  # scope and design notes
└─ .github/workflows/     # CI
```

## License

[MIT](LICENSE) © 2026 Seth
