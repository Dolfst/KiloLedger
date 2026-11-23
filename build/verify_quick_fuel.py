#!/usr/bin/env python3
"""Sanity-check quick fuel entry UI hooks (1.29.0)."""
from __future__ import annotations

import sys
from pathlib import Path

import l10n_resolver

l10n_resolver.install()

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors: list[str] = []
    version = (ROOT / "VERSION").read_text().strip()
    parts = version.split(".")
    try:
        major, minor = int(parts[0]), int(parts[1])
        ok = major > 1 or (major == 1 and minor >= 29)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.29.0, got {version!r}")

    android = (ROOT / "KiloLedger.Android/MainActivity.cs").read_text()
    for needle in (
        "Быстрая заправка",
        "ShowQuickFuelEntry",
        "BuildQuickFuelRow",
        "WireQuickFuelPrice",
        "LastFuelFullTank",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")
    if f'version = "{version}"' not in android:
        errors.append(f"Android MainActivity: missing version {version}")

    desktop = (ROOT / "KiloLedger.Desktop/MainForm.cs").read_text()
    for needle in (
        "Быстрая заправка",
        "OpenQuickFuelEntry",
        "LastFuelKmForCar",
        "LastFuelPricePerLiterForCar",
        "LastFuelFullTankForCar",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")
    if f'AppVersion = "{version}"' not in desktop:
        errors.append(f"Desktop MainForm: missing AppVersion {version}")

    kpk = (ROOT / "KiloLedger/KiloLedger/Form1.cs").read_text()
    for needle in (
        "Быстрая заправка",
        "QuickFuelFromMenu",
        "QuickFuel()",
        "LastFuelFullTank",
        "Проверьте данные: пробег не меньше 0",
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")
    if f"Версия {version}" not in kpk:
        errors.append(f"KPK Form1: missing version {version}")

    readme = (ROOT / "README.ru.md").read_text()
    if "**1.29.0**" not in readme:
        errors.append("README: missing 1.29.0")
    if "быстрая заправка" not in readme.lower() and "Быстрая заправка" not in readme:
        errors.append("README: missing Быстрая заправка")

    download = (ROOT / "dist/download/README.md").read_text()
    if "1.29.0" not in download:
        errors.append("dist/download/README: missing 1.29.0")

    csproj = (ROOT / "KiloLedger.Android/KiloLedger.Android.csproj").read_text()
    if f"<ApplicationDisplayVersion>{version}</ApplicationDisplayVersion>" not in csproj:
        errors.append(f"Android csproj: missing ApplicationDisplayVersion {version}")

    desktop = (ROOT / "KiloLedger.Desktop/MainForm.cs").read_text()
    if "Проверьте данные: пробег не меньше 0" not in desktop:
        errors.append("Desktop MainForm: missing quick-fuel positive-value validation")
    android = (ROOT / "KiloLedger.Android/MainActivity.cs").read_text()
    if "Проверьте данные: пробег не меньше 0" not in android:
        errors.append("Android MainActivity: missing quick-fuel positive-value validation")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK quick fuel entry checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
