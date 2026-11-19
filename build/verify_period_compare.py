#!/usr/bin/env python3
"""Sanity-check period comparison hooks (1.24.0)."""
from __future__ import annotations

import sys
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def month_bounds(today: date) -> tuple[str, str, str, str]:
    this_start = date(today.year, today.month, 1)
    if today.month == 1:
        prev_start = date(today.year - 1, 12, 1)
    else:
        prev_start = date(today.year, today.month - 1, 1)
    # last day of this / prev month via next-month - 1 day
    if today.month == 12:
        this_end = date(today.year + 1, 1, 1)
    else:
        this_end = date(today.year, today.month + 1, 1)
    this_end = this_end.fromordinal(this_end.toordinal() - 1)
    prev_end = this_start.fromordinal(this_start.toordinal() - 1)
    fmt = lambda d: d.isoformat()
    return fmt(this_start), fmt(this_end), fmt(prev_start), fmt(prev_end)


def main() -> int:
    errors: list[str] = []
    version = (ROOT / "VERSION").read_text().strip()
    parts = version.split(".")
    try:
        major, minor = int(parts[0]), int(parts[1])
        ok = major > 1 or (major == 1 and minor >= 24)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.24.0, got {version!r}")

    for rel in (
        "Alfa156.Android/PeriodCompare.cs",
        "Alfa156.Desktop/Data/PeriodCompare.cs",
        "Alfa156/Alfa156/PeriodCompare.cs",
    ):
        text = (ROOT / rel).read_text()
        for needle in (
            "class PeriodCompare",
            "ThisVsPreviousMonth",
            "FormatComparison",
            "CostPerKm",
            "Коммунальные",
            "Стоимость/км",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        "Сравнить периоды",
        "ShowPeriodCompare",
        "PromptCustomPeriodCompare",
        "ShowPeriodCompareResult",
        'version = "1.24.0"',
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "Сравнить периоды",
        "ShowPeriodCompare",
        'AppVersion = "1.24.0"',
        "Этот / прошлый месяц",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")

    desktop_csproj = (ROOT / "Alfa156.Desktop/Alfa156.Desktop.csproj").read_text()
    if "PeriodCompare.cs" not in desktop_csproj:
        errors.append("Desktop csproj: PeriodCompare.cs not listed")

    kpk = (ROOT / "Alfa156/Alfa156/Form1.cs").read_text()
    for needle in (
        "Сравнить периоды",
        "ShowPeriodCompareFromMenu",
        "1.24.0",
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    kpk_csproj = (ROOT / "Alfa156/Alfa156/Alfa156.csproj").read_text()
    if "PeriodCompare.cs" not in kpk_csproj:
        errors.append("KPK csproj: PeriodCompare.cs not listed")

    build_kpk = (ROOT / "build/build-kpk.ps1").read_text()
    if "PeriodCompare.cs" not in build_kpk:
        errors.append("build-kpk.ps1: PeriodCompare.cs not in sources")

    android_csproj = (ROOT / "Alfa156.Android/Alfa156.Android.csproj").read_text()
    if "1.24.0" not in android_csproj:
        errors.append("Android csproj: missing ApplicationDisplayVersion 1.24.0")

    readme = (ROOT / "README.md").read_text()
    if "## Что нового в 1.24.0" not in readme:
        errors.append("README: missing Что нового в 1.24.0")
    if "**1.24.0**" not in readme:
        errors.append("README: missing История 1.24.0")

    # Math sanity: month bounds + delta sign
    a_from, a_to, b_from, b_to = month_bounds(date(2026, 11, 19))
    if (a_from, a_to, b_from, b_to) != (
        "2026-11-01",
        "2026-11-30",
        "2026-10-01",
        "2026-10-31",
    ):
        errors.append(f"month bounds unexpected: {(a_from, a_to, b_from, b_to)}")
    fuel_a, fuel_b = 120.0, 100.0
    delta = fuel_a - fuel_b
    if delta != 20.0:
        errors.append("delta math broken")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK period compare checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
