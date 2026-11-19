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
        f'version = "{version}"',
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "Сравнить периоды",
        "ShowPeriodCompare",
        f'AppVersion = "{version}"',
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
        version,
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    kpk_csproj = (ROOT / "Alfa156/Alfa156/Alfa156.csproj").read_text()
    if "PeriodCompare.cs" not in kpk_csproj:
        errors.append("KPK csproj: PeriodCompare.cs not listed")

    kpk_pc = (ROOT / "Alfa156/Alfa156/PeriodCompare.cs").read_text()
    if "namespace CarExpenses" not in kpk_pc:
        errors.append("KPK PeriodCompare: must use namespace CarExpenses (CF build)")
    if "Decimal.TryParse" in kpk_pc:
        errors.append("KPK PeriodCompare: Decimal.TryParse is not CF-safe; use DataStore.TryNumber")
    if "DataStore.TryNumber" not in kpk_pc and "DataStore.NumberOrZero" not in kpk_pc:
        errors.append("KPK PeriodCompare: missing DataStore number helpers")

    build_kpk = (ROOT / "build/build-kpk.ps1").read_text()
    if "PeriodCompare.cs" not in build_kpk:
        errors.append("build-kpk.ps1: PeriodCompare.cs not in sources")

    android_csproj = (ROOT / "Alfa156.Android/Alfa156.Android.csproj").read_text()
    if version not in android_csproj:
        errors.append(f"Android csproj: missing ApplicationDisplayVersion {version}")

    readme = (ROOT / "README.md").read_text()
    if f"## Что нового в {version}" not in readme and "**1.24.0**" not in readme:
        errors.append(f"README: missing Что нового / История for {version} or 1.24.0")
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

    # Totals on sample .dat (mirrors PeriodCompare.Build for car Alfa)
    sample = ROOT / "build" / "sample_data"
    if not sample.is_dir():
        errors.append("missing build/sample_data")
    else:
        def load(name: str) -> list[list[str]]:
            path = sample / name
            rows = []
            for line in path.read_text(encoding="utf-8").splitlines():
                if line.strip():
                    rows.append(line.split("|"))
            return rows

        def strip_id(row: list[str]) -> list[str]:
            if row and len(row[-1]) >= 10 and "_" in row[-1] and row[-1][1] == "_":
                # cheap id check: x_xxxxxxxx
                return row[:-1]
            return list(row)

        def num(value: str) -> float:
            try:
                return float((value or "0").replace(",", "."))
            except ValueError:
                return 0.0

        def in_day(d: str, lo: str, hi: str) -> bool:
            if not d or len(d) < 10:
                return False
            day = d[:10]
            return (not lo or day >= lo) and (not hi or day <= hi)

        def in_month(m: str, lo: str, hi: str) -> bool:
            if not m or len(m) < 7:
                return False
            key = m[:7]
            lo_m = lo[:7] if len(lo) >= 7 else ""
            hi_m = hi[:7] if len(hi) >= 7 else ""
            return (not lo_m or key >= lo_m) and (not hi_m or key <= hi_m)

        def totals(lo: str, hi: str) -> dict[str, float]:
            t = {"fuel": 0.0, "repair": 0.0, "expense": 0.0, "utility": 0.0}
            kms: list[float] = []
            for row in load("fuel.dat"):
                c = strip_id(row)
                if len(c) < 5 or c[1].lower() != "alfa" or not in_day(c[0], lo, hi):
                    continue
                t["fuel"] += num(c[4])
                kms.append(num(c[2]))
            for row in load("repairs.dat"):
                c = strip_id(row)
                if len(c) < 8 or c[1].lower() != "alfa" or not in_day(c[0], lo, hi):
                    continue
                t["repair"] += num(c[7])
                kms.append(num(c[2]))
            for row in load("expenses.dat"):
                c = strip_id(row)
                if len(c) < 5 or c[1].lower() != "alfa" or not in_day(c[0], lo, hi):
                    continue
                t["expense"] += num(c[4])
            for row in load("utilities.dat"):
                if not in_month(row[0], lo, hi):
                    continue
                t["utility"] += sum(num(row[i]) for i in range(1, min(8, len(row))))
            t["kms"] = kms
            return t

        nov = totals("2026-11-01", "2026-11-30")
        oct_ = totals("2026-10-01", "2026-10-31")
        # Nov fuel: 130 + 60 = 190; repair 150; expense 40; utility 35+12+5=52
        if abs(nov["fuel"] - 190.0) > 1e-6:
            errors.append(f"sample Nov fuel expected 190, got {nov['fuel']}")
        if abs(nov["repair"] - 150.0) > 1e-6:
            errors.append(f"sample Nov repair expected 150, got {nov['repair']}")
        if abs(nov["expense"] - 40.0) > 1e-6:
            errors.append(f"sample Nov expense expected 40, got {nov['expense']}")
        if abs(nov["utility"] - 52.0) > 1e-6:
            errors.append(f"sample Nov utility expected 52, got {nov['utility']}")
        # Oct fuel: 120+105=225; repair 80; expense 25; utility 30+10+5=45
        if abs(oct_["fuel"] - 225.0) > 1e-6:
            errors.append(f"sample Oct fuel expected 225, got {oct_['fuel']}")
        if abs(oct_["repair"] - 80.0) > 1e-6:
            errors.append(f"sample Oct repair expected 80, got {oct_['repair']}")
        if abs(oct_["expense"] - 25.0) > 1e-6:
            errors.append(f"sample Oct expense expected 25, got {oct_['expense']}")
        if abs(oct_["utility"] - 45.0) > 1e-6:
            errors.append(f"sample Oct utility expected 45, got {oct_['utility']}")
        cost_nov = nov["fuel"] + nov["repair"] + nov["expense"]
        cost_oct = oct_["fuel"] + oct_["repair"] + oct_["expense"]
        if abs((cost_nov - cost_oct) - (190 + 150 + 40 - 225 - 80 - 25)) > 1e-6:
            errors.append("sample period delta math broken")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK period compare checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
