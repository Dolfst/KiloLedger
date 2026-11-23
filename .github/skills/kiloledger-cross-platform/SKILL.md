---
name: kiloledger-cross-platform
description: Implement or fix KiloLedger application behavior across Android, Desktop, and Windows Mobile. Use when changing shared features, domain logic, UI flows, settings, reports, or data validation.
---

# Cross-platform KiloLedger changes

1. Identify which clients and shared file formats the change affects:
   - Android: `KiloLedger.Android/`
   - Desktop: `KiloLedger.Desktop/`
   - Windows Mobile / KPK: `KiloLedger/KiloLedger/`
2. Read the corresponding implementations on every affected client before editing. Similar code is often intentionally duplicated; do not assume one client delegates to another.
3. Keep user-visible behavior and `.dat` formats aligned. Preserve compatibility with existing files and older clients unless the task explicitly changes compatibility.
4. Keep KPK code compatible with .NET Compact Framework 3.5. Avoid APIs newer than CF 3.5 and run `build/verify_cf_apis.py`.
5. Add or extend a focused `build/verify_*.py` check for any behavior that can be verified offline. Run `python3 build/run_verifies.py`.
6. Build affected clients where the environment supports it:
   - KPK: `./build/build-kpk.sh Release`
   - Desktop on Linux with Mono reference assemblies: `dotnet build KiloLedger.Desktop/KiloLedger.Desktop.csproj --nologo -p:FrameworkPathOverride=/usr/lib/mono/4.8-api`
   - Android: `dotnet build KiloLedger.Android/KiloLedger.Android.csproj --no-restore --nologo`
7. Summarize affected clients, compatibility decisions, and verification results.
