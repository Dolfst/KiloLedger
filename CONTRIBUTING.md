# Contributing to KiloLedger

[English](CONTRIBUTING.md) · [Русский](CONTRIBUTING.ru.md)

KiloLedger is three clients and one text database: Windows Mobile (`KiloLedger/KiloLedger`, .NET Compact Framework 3.5), Android (`KiloLedger.Android`, .NET 10), and Windows (`KiloLedger.Desktop`, .NET Framework 4.8). Read [`README.md`](README.md) for what the apps actually do. The Russian overview is [`README.ru.md`](README.ru.md).

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
- Desktop on Windows: MSBuild, configuration `Release`. On Linux, the repository notes use `dotnet build KiloLedger.Desktop/KiloLedger.Desktop.csproj -p:FrameworkPathOverride=/usr/lib/mono/4.8-api`.
- Android: .NET 10 SDK, JDK 17, Android SDK. See the build section in the README. CI publishes an ARM64 APK.

## Documentation

User-facing docs are bilingual.

- [`README.md`](README.md) is English. [`README.ru.md`](README.ru.md) is the full Russian text. Both start with links to each other.
- This file and [`CONTRIBUTING.ru.md`](CONTRIBUTING.ru.md) follow the same rule.
- Update both languages when a fact changes. Do not describe a feature that is only on the backlog in [`ROADMAP.md`](ROADMAP.md) or [`docs/competitive-analysis.md`](docs/competitive-analysis.md).
- [`localization/LOCALIZATION.md`](localization/LOCALIZATION.md) is an older note. Shipped languages are `ru`, `en`, `es`, `fr`, and `de` inside each client.

Commit messages in this repository are bilingual, for example: `Уточнить установку в README / Clarify install steps in the README`.

## License and the contributor agreement

The program is under the GNU AGPL version 3 or any later version. Copyright (C) 2026 Dolfst. The text is [LICENSE](LICENSE).

By opening a pull request you agree to the [Contributor License Agreement](CLA.md) for the work in that request. You keep the copyright. You grant Dolfst a perpetual, worldwide, irrevocable, royalty-free license that includes the right to sublicense and to relicense the contribution, including under proprietary terms, plus the patent license and the originality statement in the CLA. The English [CLA.md](CLA.md) prevails over [CLA.ru.md](CLA.ru.md).

To sign, tick the Contributor License Agreement checkbox in the pull request template ([`.github/pull_request_template.md`](.github/pull_request_template.md)). If the template is not in the description, write a sentence that you agree to `CLA.md`. The [CLA Assistant](https://github.com/cla-assistant/cla-assistant) GitHub app can record signatures automatically. This repository does not require that app.

## Bugs and ideas

Open an issue on [Dolfst/KiloLedger](https://github.com/Dolfst/KiloLedger/issues). Say which client, which version (`VERSION` or the About screen), and whether the data came from a `.dat` folder, a ZIP, Wi-Fi, or Google Sheets.

Automated contributors also follow [`AGENTS.md`](AGENTS.md) and `.github/skills/`.
