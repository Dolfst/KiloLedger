# KiloLedger

[English](README.md) · [Русский](README.ru.md)

**Vehicle expense tracker for Windows, Android, and Windows Mobile.**

KiloLedger keeps fuel, service, other car costs, and household utility bills for one or more vehicles. A Windows PC, an Android phone, and a Windows Mobile handheld share the same text files, the same version number, and the same red-circle logo. The apps work offline. Cloud sync is optional and uses a Google Sheet that you host yourself.

Current version: **1.39.3**, from [`VERSION`](VERSION). Ready-to-run builds are in [`dist/download`](dist/download).

The product name is **KiloLedger**. Project folders, namespaces, and some asset file names are still `Alfa156` (`com.kiloledger.app` on Android).

## What it is for

The three clients cover the same job: write down what the car cost, see the cost per kilometre and the fuel use, and move the ledger to another device without a mandatory account. Windows Mobile has no direct Google connection. It exchanges data with Android over local Wi-Fi, or through a folder or ZIP that a PC can open.

There is no installer for the PC app and no store listing in this repository. You run a published executable, an APK, or a Windows Mobile `.exe`.

## Features

Confirmed in the three clients and in [`docs/snapshot-schema.md`](docs/snapshot-schema.md):

- Several vehicles, with a passport (`year`, plate, VIN, tank size, note) and a default vehicle.
- Fuel rows: date, odometer, litres, cost, price per litre, comment, full-tank flag, purpose, station or brand.
- Repairs and service, with editable categories.
- Other expenses, with their own categories.
- One utility row per month: utility bills, electricity, gas, landline phone, home internet, rental tax, and rent.
- Trips with a start and end odometer, purpose, and driver. Mileage is `endKm − startKm`.
- Notes, date reminders (insurance, inspection, or custom), and templates for fuel, repair, and expense rows.
- Receipt photos stored as `Receipts/{recordId}.jpg`. Android and Desktop can attach or remove a photo. The handheld can open or delete a photo already stored for the selected row.
- Summary with a 12-month chart, cost per kilometre, fuel use in l/100 km between full tanks, a yearly total, a car comparison, and a period comparison.
- A service forecast from recent odometer readings (average km/day and an estimate until the next oil and timing-belt service).
- A soft monthly budget (`budget_month`, warning at `budget_warn_pct`, default 80). Utilities stay outside that car-cost total.
- Period report. Desktop can save `.txt` or `.html` and print. Android can share text or HTML. The handheld saves a text file and can also save HTML.
- Search, filters, duplicate search, an odometer check, and a data-integrity check.
- Full ZIP or folder backups, including a `snapshot.json` file and a safety copy taken before a restore.
- Local Wi-Fi sync between Android and the handheld, and optional Google Sheets sync on Android and Desktop.
- Interface languages: Russian, English, Spanish, French, and German. Dictionaries are embedded in the Desktop executable and in the Android assets. The default language is Russian.

Default service intervals in a new database are 10 000 km for oil (warn at 1 000 km) and 60 000 km for the timing belt (warn at 5 000 km). The default currency code is `BYN`. All of these live in `settings.dat` and can be changed in the app.

## Tech stack

| Client | Folder | Stack |
| --- | --- | --- |
| Windows Mobile handheld | `Alfa156/Alfa156` | .NET Compact Framework 3.5, Windows Forms, Pocket PC |
| Android | `Alfa156.Android` | .NET 10 (`net10.0-android`), application id `com.kiloledger.app`, minimum Android 6.0 (API 23) |
| Windows PC | `Alfa156.Desktop` | .NET Framework 4.8, Windows Forms |
| Google Sheet bridge | `google-sync/Alfa156Sync.gs` | Google Apps Script web app |

Shared domain code is copied into each client on purpose. A change to behaviour or to a `.dat` file has to land in every client that owns that behaviour.

Published Android builds are ARM64 APKs. GitHub Actions signs them with `Alfa156.Android/alfa156-upload.keystore`.

## Repository layout

```text
Alfa156/Alfa156/     Windows Mobile solution (Alfa156.sln)
Alfa156.Android/     Android project
Alfa156.Desktop/     Windows desktop project
google-sync/         Apps Script and the illustrated sync guide
branding/            Shared logo and BrandTheme.md
localization/        String tables (ru, en, es, fr, de) and older i18n notes
build/               Handheld build scripts, CF 3.5 reference assemblies, smoke checks
docs/                Snapshot schema and a competitor comparison
dist/download/       Versioned APK, Desktop exe, and handheld exe/zip
VERSION              Single version string used by release file names
```

Also at the root: [`INSTALL.md`](INSTALL.md) (short Russian install notes), [`GOOGLE_SYNC.md`](GOOGLE_SYNC.md), [`ROADMAP.md`](ROADMAP.md), and [`AGENTS.md`](AGENTS.md) for automated contributors.

## Requirements

To **run** a published build:

- **Android:** Android 6.0 or newer, 64-bit ARM. Allow install from the file manager if the system asks.
- **Windows PC:** .NET Framework 4.8. The `.exe` is not an installer.
- **Windows Mobile:** .NET Compact Framework 3.5 on the device. Copy the `.exe` with Windows Mobile Device Center or ActiveSync. The handheld zip in `dist/download` is described there as the app plus the CF 3.5 CAB.

To **build**:

- Handheld on Windows: `build/build-kpk.ps1`, `ResGen.exe`, and `csc.exe`, plus the reference assemblies already in `build/cf35/WindowsCE`. Visual Studio 2008 with the Windows Mobile SDK can open `Alfa156/Alfa156.sln` instead.
- Handheld on Linux or macOS: Mono (`mono-devel`) and `./build/build-kpk.sh`.
- Desktop on Windows: MSBuild and the .NET Framework 4.8 targeting pack.
- Desktop on Linux, as used by this repository’s agent notes: the .NET SDK with `FrameworkPathOverride` pointed at Mono’s 4.8 reference assemblies.
- Android: .NET 10 SDK, the Android workload, JDK 17, and an Android SDK. CI installs platform 36 and build-tools 36.0.0.

Smoke checks need Python 3. CI uses Python 3.12. They do not need a device or an emulator.

## Installation

### Published builds

Version **1.39.3** in [`dist/download`](dist/download):

| File | Platform |
| --- | --- |
| `KiloLedger-1.39.3.apk` | Android ARM64 |
| `KiloLedger-1.39.3-Desktop.exe` | Windows PC |
| `KiloLedger-1.39.3-KPK.exe` | Windows Mobile |
| `KiloLedger-1.39.3-KPK.zip` | Handheld package (app and CF 3.5 CAB) |

The same files are also attached to the rolling GitHub releases [`android-latest`](https://github.com/Dolfst/alfa/releases/tag/android-latest), [`desktop-latest`](https://github.com/Dolfst/alfa/releases/tag/desktop-latest), and [`kpk-latest`](https://github.com/Dolfst/alfa/releases/tag/kpk-latest). Workflows **Android APK**, **Desktop EXE**, and **KPK EXE** rebuild them on pushes that touch the matching project or `VERSION`, and they can be started by hand from the Actions tab.

On the handheld, run the new `KiloLedger-<version>-KPK.exe` once. It rewrites the Start-menu shortcut so it points at that build.

An APK signed with a different key, or an older install that used another application id, cannot be updated in place. Export a ZIP first, uninstall, install the new APK, then import the ZIP.

### Build from source

Handheld:

```powershell
powershell -ExecutionPolicy Bypass -File build/build-kpk.ps1 -Configuration Release
```

```sh
./build/build-kpk.sh Release
```

Desktop on Windows:

```bat
msbuild Alfa156.Desktop\Alfa156.Desktop.csproj /p:Configuration=Release /p:Platform=AnyCPU
```

The output is `Alfa156.Desktop\bin\Release\Alfa156.Desktop.exe`.

Desktop on Linux, when Mono 4.8 reference assemblies are installed:

```sh
dotnet build Alfa156.Desktop/Alfa156.Desktop.csproj --nologo -p:FrameworkPathOverride=/usr/lib/mono/4.8-api
```

Android ARM64, after `ANDROID_HOME` and `JAVA_HOME` are set and the Android workload is installed:

```sh
dotnet workload install android
dotnet build Alfa156.Android/Alfa156.Android.csproj \
  -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AcceptAndroidSDKLicenses=True
dotnet build Alfa156.Android/Alfa156.Android.csproj -c Release \
  -p:RuntimeIdentifier=android-arm64 \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AcceptAndroidSDKLicenses=True
```

The signed APK is under `Alfa156.Android/bin/Release/net10.0-android/android-arm64/` and its name ends with `-Signed.apk`.

## Configuration

The apps do not read a `.env` file.

| What | Where |
| --- | --- |
| Handheld database | `\My Documents\KiloLedger\` |
| Android database | `files/KiloLedger` inside the app’s private files directory (`FilesDir`) |
| Desktop database | A folder you pick. **Open folder** / **New folder** creates or opens a `KiloLedger` directory. An import that has no folder yet uses `Documents\KiloLedger` |
| Desktop UI language | `%AppData%\KiloLedger\language.txt` (and the `language` key in `settings.dat` once a database is open) |
| Desktop Google sync | `%AppData%\KiloLedger\google-sync.json` — `scriptUrl`, `token`, `spreadsheetUrl` |
| Android Google sync | private preferences file `google-sync` (`scriptUrl`, `token`) |
| Apps Script secret | script property `SYNC_TOKEN`. A standalone script (not bound to the sheet) also needs `SPREADSHEET_ID` |

`settings.dat` holds pairs such as `oil_interval`, `oil_warn`, `belt_interval`, `belt_warn`, `currency`, `budget_month`, `budget_warn_pct`, `backup_remind_days`, `default_car`, `reminder_warn_days`, `language`, and `snapshot_device_token`.

Build-time variables for Android are `ANDROID_HOME` and `JAVA_HOME`. Do not publish `SYNC_TOKEN`, the web-app URL, or `google-sync.json`.

Step-by-step Google setup, with screenshots: [`google-sync/README.md`](google-sync/README.md) and [`GOOGLE_SYNC.md`](GOOGLE_SYNC.md).

## Usage

**Desktop.** Launch the `.exe`. On the first start the app asks for a language and stores it in `%AppData%\KiloLedger\language.txt`. Open or create a data folder from the **Database** menu. Sections are Vehicles, Fuel, Repairs, Expenses, Utilities, Summary, and Settings. **Exchange** covers ZIP, Excel, CSV, the import wizard, and Google sync (**Google: Wizard**, **Google: Settings**, **Google: Sync**). **Tools** includes **About**. Settings cards hold service intervals, currency, budget, backup reminder, default vehicle, and language.

**Android.** Private storage is created on first use. With an empty database, **Data → Start from scratch** asks for the first vehicle. Import a handheld folder or a ZIP from **Data → Import a copy**. The same **Data** menu exports a ZIP, runs Wi-Fi sync, and opens Google sync settings.

**Windows Mobile.** Data is always `\My Documents\KiloLedger\`. The form is designed at QVGA width (240 px) and scaled to the device. **Data** creates a copy in device memory or on the storage card and starts Wi-Fi sync.

Fuel consumption in l/100 km is shown only from one full tank to the next full tank with a higher odometer. Cost per kilometre uses the entered costs and the span between the lowest and highest recorded odometer. It ignores gaps and it ignores utility bills.

## Data and backups

Files are UTF-8 text. One record per line. Fields are separated by `|`. A backslash escapes `|` and itself. Each record ends with a stable id (`c_`, `f_`, `r_`, `u_`, `e_`, `k_`, `p_`, `m_`, `t_`, `n_`, `j_`, or `g_` plus 8 hex digits). Older files without ids are still read. Ids are assigned on save. Deletes go to `deleted.dat` (`type|id|UTC`), trimmed to the last 5 000 rows, so a later sync does not resurrect them.

The column layout, `snapshot.json` (`schema` = `kiloledger.snapshot`, `schemaVersion` 1), and the rule that unknown schemas are rejected are specified in [`docs/snapshot-schema.md`](docs/snapshot-schema.md). Copies made before 1.36.0 have no `snapshot.json` and remain valid.

Handheld copies:

- Device memory: `\My Documents\KiloLedger_LocalBackups\`
- Storage card: `\Storage Card\KiloLedger_Backups\yyyyMMdd_HHmmss\`

A restore saves the current data first. Restoring an old copy that has no `Receipts/` folder keeps the photos already on the device. Android writes a `Backups/Safety_…` folder before replacing a full local set. Desktop does the same with a `Safety_yyyyMMdd_HHmmss` folder next to the database.

## Synchronization

```text
Windows Mobile  ←── Wi-Fi, port 45816 ──→  Android  ←── Apps Script ──→  Google Sheets  ←→  Windows PC
```

**Android ↔ handheld.** Put both devices on one Wi-Fi network, or join the handheld to the phone’s hotspot. Internet access is not required. On Android, **Data → Wi-Fi sync** shows an address (a hotspot address is often `192.168.43.1`) and a six-digit code. Enter that address and code on the handheld. The payload is checked in a temporary folder before anything is replaced. Both sides show a merge preview and write only after you apply it. A `Safety_` copy is taken first. From version 1.12 the session is obfuscated with a key derived from the pairing code (`SYNC2`, and `KILOLEDGER2R` when photos are included). That is not TLS. Use a network you trust. Photo transfer allows up to 50 files and 2 MB each.

The Wi-Fi exchange is implemented only on Android and Windows Mobile. The PC does not speak that protocol. Bluetooth is not used.

Stable ids are matched first. Otherwise the clients fall back to natural keys (for fuel: date + vehicle + odometer). The Wi-Fi payload includes the `.dat` files those clients already share, including templates, reminders, notes, the car passport, and trips on current versions.

**Android / PC ↔ Google Sheets.** You paste `google-sync/Alfa156Sync.gs` into the sheet, set `SYNC_TOKEN`, and deploy a web app that runs as you and is reachable by anyone who has the URL. The script’s pull/push payload is cars, fuel, repairs, other expenses, utilities, repair categories, and expense categories. Sheets are named `Заправки {car}`, `Ремонт {car}`, `Прочие {car}`, `Коммунальные`, `Справочник`, and `Кат. прочих`. Trips, notes, reminders, templates, the car passport, and receipt photos are not part of that payload. Record ids stay in the local `.dat` files and in ZIP backups. Desktop Excel import also recognizes a Dengi-style workbook when the sheet names match that layout.

## Tests

Offline smoke checks live in `build/verify_*.py`. They read source and fixtures. They do not install an APK or launch Windows Forms.

```sh
python3 build/run_verifies.py
```

The runner executes every `verify_*.py` and returns a non-zero status if any script fails. Coverage includes stable ids and tombstones, ZIP and CSV import, the snapshot schema, period reports, trips, budget, sync preview and retry, and a Compact Framework API guard (`verify_cf_apis.py`). `verify_version_consistency.py` checks that version fields follow `VERSION`.

GitHub Actions workflow **Smoke verifies** runs that script and `./build/build-kpk.sh Release` on Ubuntu with Mono. It runs on pull requests and on pushes to `main` when application code, `build/`, `VERSION`, `README.md`, or `google-sync/` changes.

## Status and roadmap

Version 1.39.3 is the current line. Recent desktop work (1.38.3–1.39.3) reworked navigation into a sidebar and compact header, moved settings onto their own cards, fixed an empty Settings tab, embedded language resources in the single `.exe`, and added an **About** screen with a link back to this repository on all three clients.

[`ROADMAP.md`](ROADMAP.md) is the staged backlog. Most items through the reporting, trip, snapshot, and release-quality stages are marked done. Still open there: keeping every version field sourced from `VERSION`, and migrations for future entities (existing rows already receive ids on load). [`docs/competitive-analysis.md`](docs/competitive-analysis.md) compares the app with Drivvo, Fuelio, Simply Auto, aCar, and Fuelly and lists a longer backlog (unified quick-entry, optional local receipt OCR, PDF export). Those items are not implemented unless `ROADMAP.md` marks them done.

`localization/LOCALIZATION.md` describes an earlier i18n sketch. The five languages above are already shipped in the clients.

## Version history

- **1.39.3** — About screen on Desktop, Android, and the handheld, with a project link and a support section.
- **1.39.2** — Desktop Settings tab uses `TableLayoutPanel` and no longer renders empty. Default intervals and the Google Sync card are filled in.
- **1.39.1** — Language resources and the icon are embedded in the desktop `.exe`. The header no longer clips the search box.
- **1.39.0** — Desktop visual pass, brand mark in the sidebar, language saved for the next launch, live translation of headers and filters.
- **1.38.5** — Desktop Settings screen. Summary is charts and reports only.
- **1.38.4** — Desktop Google Sync centre, row action bars, one-line status bar.
- **1.38.3** — Desktop sidebar and compact header.
- **1.38.2** — Desktop language choice on first launch, switch without restart.
- **1.38.1** — Handheld Start-menu icon and scrolling when the on-screen keyboard opens. Public data files no longer contain a personal spreadsheet id.
- **1.38.0** — Five languages on all three clients.
- **1.37.1** — First-launch file creation. Handheld layout scales from the QVGA design.
- **1.37.0** — Period report: litres, l/100 km, budget. Handheld year report and HTML. Snapshot schema documented.
- **1.36.0** — Trips and a personal/work mileage report. Versioned `snapshot.json`.
- **1.35.0** — Richer sync logs, receipt checks, full cost in period reports.
- **1.34.1** — Safer Desktop ZIP/CSV import. Old handheld restores keep receipt photos.
- **1.34.0** — Stronger integrity checks. Handheld build on Linux via Mono.
- **1.33.0** — Printable and shareable period report.
- **1.32.0** — Fuel station / brand.
- **1.31.0** — Per-car notes.
- **1.30.0** — Service forecast on the summary.
- **1.29.0** — Quick fuel entry.
- **1.28.0** — Smoke checks in CI. CF API guard.
- **1.27.0** — Desktop import wizard. Android import preview. CSV templates.
- **1.26.0** — Date reminders.
- **1.25.0** — Templates and a favourite vehicle.
- **1.24.0** — Period comparison.
- **1.23.0** — Backup reminder and a fuller ZIP export.
- **1.22.0** — Monthly budget.
- **1.21.0** — Car comparison and the integrity check.
- **1.20.0** — Car passport and the expense timeline.
- **1.19.0** — Fuel purpose, yearly totals, first-run hint.
- **1.18.0** — Summary export, duplicate search, last price per litre.
- **1.17.0** — Odometer check, global search, other expenses on Google Sheets.
- **1.16.0** — Merge preview and the automatic `Safety_` copy.
- **1.15.0** — Currency setting. Receipt photos over Wi-Fi.
- **1.14.0** — 12-month chart. Photos inside ZIP backups.
- **1.13.0** — Desktop filters. Full-tank flag.
- **1.12.0** — Wi-Fi session obfuscation. Desktop Google wizard.
- **1.11.0** — Other expenses. Configurable service intervals.
- **1.10.0** — Stable ids and `deleted.dat`.
- **1.9.0** — Desktop summary, price per litre, Android service notifications.

<a id="support"></a>

## Support

KiloLedger is free and has no ads. The in-app About screen links here (`https://github.com/Dolfst/alfa#support`).

- Star [Dolfst/alfa](https://github.com/Dolfst/alfa) if the project is useful to you.
- Paid support links (Boosty, Tribute, YooMoney) are not in the repository yet. Add them in this section when you choose them. The apps already point at this anchor.

Issues: [github.com/Dolfst/alfa/issues](https://github.com/Dolfst/alfa/issues).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and [CONTRIBUTING.ru.md](CONTRIBUTING.ru.md). Short version: keep the three clients and the `.dat` format in step, stay inside the Compact Framework 3.5 API on the handheld, and run `python3 build/run_verifies.py` before you finish. Commit messages in this repository are bilingual (Russian and English).

## License

This repository has **no `LICENSE` file**. Source is published on GitHub, and the About screen calls the project open source, but a license has not been chosen here. Until the owner adds one, do not assume a particular OSI license. The owner should pick a license and add a `LICENSE` file.
