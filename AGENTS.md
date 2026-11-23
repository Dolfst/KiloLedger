# Instructions for AI agents

KiloLedger is an offline vehicle expense tracker: fuel, repairs, other car costs, and household utilities, for one or more cars. Three clients share the same text files. The product name is **KiloLedger**. Folders, namespaces, and some asset names stay `Alfa156`. Android id: `com.kiloledger.app`. Repo: `Dolfst/KiloLedger` (old `Dolfst/alfa` redirects here).

Read this file once. Then read only `git status`, the latest commits, and the smallest section you need. Do not reread this file, `README.md`, or the whole diff unless the task requires it.

## Clients

| Client | Folder | Stack |
| --- | --- | --- |
| Windows desktop | `Alfa156.Desktop` | .NET Framework 4.8, WinForms. Old-style csproj. |
| Android | `Alfa156.Android` | .NET 10 (`net10.0-android`), min API 23. Published build is ARM64. |
| Windows Mobile | `Alfa156/Alfa156` | .NET Compact Framework 3.5, WinForms. Solution `Alfa156/Alfa156.sln`. |

Domain code is copied on purpose. Change every client that owns the behavior. Do not add APIs newer than CF 3.5 in `Alfa156/Alfa156`. `build/verify_cf_apis.py` enforces that.

## Data files

Contract is in `docs/snapshot-schema.md`. Codecs: `Alfa156.Desktop/Data/AlfaCodec.cs`, `Alfa156.Android/ExpenseDataCodec.cs`, `Alfa156/Alfa156/DataStore.cs`. Ids: `RecordIds.cs` (`NewId` = prefix + 8 hex from `Guid`). Templates: `RecordTemplates.cs`.

- UTF-8, no BOM (`new UTF8Encoding(false)`).
- One record per line via `StreamWriter.WriteLine`. That is CRLF on Desktop and the handheld, LF on Android. `ReadLine` accepts both.
- Fields split by `|`. `\` escapes `\` and `|`. A CR or LF inside a field is stored as a space.
- Stable id is the last field. Old rows without an id still load; `EnsureIds` assigns one on save.
- `deleted.dat` is tombstones (`type|id|UTC`), trimmed to the last 5000. Deletes must not come back on sync.

| File | Id prefix |
| --- | --- |
| `cars.dat` | `c_` |
| `fuel.dat` | `f_` |
| `repairs.dat` | `r_` |
| `utilities.dat` | `u_` |
| `repair-categories.dat` | `k_` |
| `expenses.dat` | `e_` |
| `expense-categories.dat` | `p_` |
| `cars-meta.dat` | `m_` |
| `templates.dat` | `t_` |
| `reminders.dat` | `n_` |
| `notes.dat` | `j_` |
| `trips.dat` | `g_` |

`settings.dat` is key/value pairs (no record id). Receipt photos are `Receipts/{recordId}.jpg`.

## Localization

Five languages: `ru`, `en`, `es`, `fr`, `de`. Default `ru`. Source tables: `localization/resources/strings.*.json`. Each client has its own copy and embeds it:

- Desktop: `Alfa156.Desktop/resources/`
- Android: `Alfa156.Android/Assets/`
- Handheld: `Alfa156/Alfa156/resources/`

`build/l10n_resolver.py` resolves `L10n.Get` against `localization/resources/strings.ru.json` during smoke checks. A new UI string must be added to the shared table and all three client copies, in all five files. `localization/LOCALIZATION.md` is an older note; the five languages are already shipped.

## Build on Linux

Installed and used successfully: .NET SDK **10.0.404**, package `Microsoft.NETFramework.ReferenceAssemblies.net48` **1.0.3**, Microsoft OpenJDK **17** (Ubuntu’s OpenJDK 21 version string is rejected by the Android SDK), Android SDK **platform 36** and **build-tools 36.0.0**, `dotnet workload install android` for SDK 10.0.404.

Desktop (no Mono required):

```sh
dotnet build Alfa156.Desktop/Alfa156.Desktop.csproj -c Release --nologo \
  -p:FrameworkPathOverride=/path/to/net48/build/.NETFramework/v4.8
```

The reference-assembly path is `build/.NETFramework/v4.8` inside the `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 nupkg. Output: `Alfa156.Desktop/bin/Release/Alfa156.Desktop.exe`. The exe does not run on Linux.

Android ARM64:

```sh
export JAVA_HOME=/path/to/jdk-17
export ANDROID_HOME=/path/to/android-sdk   # platforms/android-36, build-tools/36.0.0
dotnet workload install android
dotnet build Alfa156.Android/Alfa156.Android.csproj -c Release \
  -p:RuntimeIdentifier=android-arm64 \
  -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AcceptAndroidSDKLicenses=True
```

Signed APK: `Alfa156.Android/bin/Release/net10.0-android/android-arm64/com.kiloledger.app-Signed.apk`.

Handheld: `./build/build-kpk.sh Release` (package `mono-devel`: `mcs` and `resgen`). References are in `build/cf35/WindowsCE`.

## Checks and CI

```sh
python3 build/run_verifies.py
```

That runs every `build/verify_*.py`. They read source and fixtures. They do not boot an emulator. Run the one script that covers your change first; run the full set before you finish a behavior change.

- **Smoke verifies** (`.github/workflows/smoke-verifies.yml`): the script above, then `./build/build-kpk.sh Release`. Pull requests and pushes to `main` when app code, `build/`, `VERSION`, `README.md`, `README.ru.md`, or `google-sync/` change.
- **PR compile** (`.github/workflows/pr-compile.yml`): Desktop via the net48 reference assemblies, and the Android ARM64 build, on pull requests that touch those projects. No release publish.
- **Desktop EXE**, **Android APK**, **KPK EXE**: publish `dist/download` and rolling releases on push to `main`. Do not delete a release before the replacement artifact exists.

Russian release-note checks read `README.ru.md` (`## Что нового в x.y.z`, `## История версий`). English `README.md` must keep `Current version: **x.y.z**`.

## Versioning

`VERSION` is the only version string. A versioned release updates it and every display field the release skill lists (`Alfa156.Android` csproj, Desktop `AssemblyInfo` and `MainForm.AppVersion`, handheld `AssemblyInfo` and About text). `build/verify_version_consistency.py` checks them. Do not bump `VERSION` for docs-only work or a small support link. Commit messages are bilingual: `Кратко по-русски / Short English summary`.

## Google Sync

`google-sync/Alfa156Sync.gs` is the Apps Script the user deploys. Script property `SYNC_TOKEN`. A script that is not bound to the sheet also needs `SPREADSHEET_ID`. Desktop and Android call it. The handheld does not. Payload: cars, fuel, repairs, expenses, utilities, repair categories, expense categories. Trips, notes, reminders, templates, the car passport, and receipt photos stay local. Details: `GOOGLE_SYNC.md` and `google-sync/README.md`.

## Repo conventions

- Docs are bilingual. `README.md` is English. `README.ru.md` is the full Russian text. Same for `CONTRIBUTING` and `CLA`. The English CLA prevails.
- License: GNU AGPL version 3 or any later version (`AGPL-3.0-or-later`). Copyright (C) 2026 Dolfst. Text: `LICENSE`.
- A pull request agrees to `CLA.md` by ticking the box in `.github/pull_request_template.md`. CLA Assistant is optional.
- Support URL, in the app and in `.github/FUNDING.yml`: `https://boosty.to/kiloledger`. About screens also link to `https://github.com/Dolfst/alfa#support`, which redirects to the English README anchor.
- Docs-only tasks do not change application code. Keep pull requests small. App behavior and docs that can merge separately go in separate PRs.
- Do not commit new passwords, tokens, keystores, or local `*.dat` files. Do not rewrite shared history.

## Working rules

- Start from `git status`. Leave unrelated changes alone.
- Batch searches. One narrow task stays on the main agent.
- For a behavior or format change, update every affected client and add or extend a `build/verify_*.py` check.
- End with a short handoff: what changed, commits, checks, and what is left. Do not paste long logs.

Skills: `.github/skills/alfa-cross-platform/SKILL.md`, `alfa-sync-data`, `alfa-ci`, `alfa-release`.
