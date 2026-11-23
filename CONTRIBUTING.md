# Contributing to KiloLedger

[English](CONTRIBUTING.md) · [Русский](CONTRIBUTING.ru.md)

KiloLedger is three clients and one text database: Windows Mobile (`Alfa156/Alfa156`, .NET Compact Framework 3.5), Android (`Alfa156.Android`, .NET 10), and Windows (`Alfa156.Desktop`, .NET Framework 4.8). Read [`README.md`](README.md) for what the apps actually do. The Russian overview is [`README.ru.md`](README.ru.md).

## Before you change behaviour

- Match the change in every client that implements that behaviour. The copies are intentional. One client does not call the others.
- Keep `.dat`, ZIP, and `snapshot.json` compatible with files already in the wild. The column rules are in [`docs/snapshot-schema.md`](docs/snapshot-schema.md). New optional fields go at the end of the record, before the stable id. Raising `schemaVersion` is only for a break that old clients must reject.
- On the handheld, use only APIs that Compact Framework 3.5 has. `python3 build/verify_cf_apis.py` checks this.
- The version people see is the single line in [`VERSION`](VERSION). Release file names are `KiloLedger-<version>…`. Do not invent a second version number in one client and leave the others behind.
- Do not commit new passwords, tokens, keystores, or local `*.dat` journals. `SYNC_TOKEN` and `%AppData%\KiloLedger\google-sync.json` stay on the user’s machine.

## Checks

Smoke checks do not need a device:

```sh
python3 build/run_verifies.py
```

Add or extend a `build/verify_*.py` script when the change can be seen in source text or in a fixture (format, report numbers, import rules). Checks that look for Russian release notes read `README.ru.md`, not the English `README.md`. Workflow **Smoke verifies** runs the same script, and it also builds the handheld with `./build/build-kpk.sh Release` on Ubuntu.

Build the client you touched when the machine can:

- Handheld: `./build/build-kpk.sh Release` or `build/build-kpk.ps1`.
- Desktop on Windows: MSBuild, configuration `Release`. On Linux, the repository notes use `dotnet build Alfa156.Desktop/Alfa156.Desktop.csproj -p:FrameworkPathOverride=/usr/lib/mono/4.8-api`.
- Android: .NET 10 SDK, JDK 17, Android SDK. See the build section in the README. CI publishes an ARM64 APK.

## Documentation

User-facing docs are bilingual.

- [`README.md`](README.md) is English. [`README.ru.md`](README.ru.md) is the full Russian text. Both start with links to each other.
- This file and [`CONTRIBUTING.ru.md`](CONTRIBUTING.ru.md) follow the same rule.
- Update both languages when a fact changes. Do not describe a feature that is only on the backlog in [`ROADMAP.md`](ROADMAP.md) or [`docs/competitive-analysis.md`](docs/competitive-analysis.md).
- [`localization/LOCALIZATION.md`](localization/LOCALIZATION.md) is an older note. Shipped languages are `ru`, `en`, `es`, `fr`, and `de` inside each client.

Commit messages in this repository are bilingual, for example: `Уточнить установку в README / Clarify install steps in the README`.

## Bugs and ideas

Open an issue on [Dolfst/KiloLedger](https://github.com/Dolfst/KiloLedger/issues). Say which client, which version (`VERSION` or the About screen), and whether the data came from a `.dat` folder, a ZIP, Wi-Fi, or Google Sheets.

There is no `LICENSE` file yet. Do not add one on a contributor’s behalf. The owner chooses the license. See the License section of the README.

Automated contributors also follow [`AGENTS.md`](AGENTS.md) and `.github/skills/`.
