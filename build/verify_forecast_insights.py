#!/usr/bin/env python3
"""Sanity-check forecast / «Оценка пробега» hooks (1.30.0)."""
from __future__ import annotations

import math
import sys
from datetime import date, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def money(value: float) -> str:
    text = f"{value:.2f}".rstrip("0").rstrip(".")
    return text if text else "0"


def avg_km_per_day(readings: list[tuple[date, float]], today: date) -> tuple[float, int] | None:
    """Mirror ForecastInsights.TryAverageKmPerDay for a pure-Python check."""
    window_start = today - timedelta(days=90)
    in_window = [(d, km) for d, km in readings if window_start <= d <= today and km >= 0]
    if len(in_window) < 2:
        return None
    earliest = min(in_window, key=lambda r: (r[0], r[1]))
    latest = max(in_window, key=lambda r: (r[0], r[1]))
    span = (latest[0] - earliest[0]).days
    if span < 30 or span > 90:
        return None
    delta = latest[1] - earliest[1]
    if delta <= 0:
        return None
    avg = delta / span
    return (avg, span) if avg > 0 else None


def service_line(title: str, current: float, last: float, interval: float, avg: float) -> str:
    remaining = interval - (current - last)
    if remaining <= 0:
        return f"{title}: срок прошёл (оценка)"
    days = int(remaining / avg + 0.5)
    if days < 1:
        days = 1
    return f"{title}: ~{money(remaining)} км ≈ {days} дн. (оценка)"


def main() -> int:
    errors: list[str] = []
    version = (ROOT / "VERSION").read_text().strip()
    parts = version.split(".")
    try:
        major, minor = int(parts[0]), int(parts[1])
        ok = major > 1 or (major == 1 and minor >= 30)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.30.0, got {version!r}")

    for rel in (
        "Alfa156.Android/ForecastInsights.cs",
        "Alfa156.Desktop/Data/ForecastInsights.cs",
        "Alfa156/Alfa156/ForecastInsights.cs",
    ):
        text = (ROOT / rel).read_text()
        for needle in (
            "class ForecastInsights",
            "TryAverageKmPerDay",
            "FormatBlock",
            "Оценка пробега",
            "Средний:",
            "До замены масла",
            "До ремня ГРМ",
            "(оценка)",
            "MinSpanDays = 30",
            "MaxLookbackDays = 90",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        "ForecastInsights.FormatBlock",
        f'version = "{version}"',
        "Оценка пробега в сводке",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "ForecastInsights.FormatBlock",
        f'AppVersion = "{version}"',
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")

    desktop_csproj = (ROOT / "Alfa156.Desktop/Alfa156.Desktop.csproj").read_text()
    if "ForecastInsights.cs" not in desktop_csproj:
        errors.append("Desktop csproj: missing ForecastInsights.cs")

    kpk = (ROOT / "Alfa156/Alfa156/Form1.cs").read_text()
    for needle in (
        "ForecastInsights.FormatBlock",
        f"Версия {version}",
        "Оценка пробега в сводке",
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    kpk_csproj = (ROOT / "Alfa156/Alfa156/Alfa156.csproj").read_text()
    if "ForecastInsights.cs" not in kpk_csproj:
        errors.append("KPK csproj: missing ForecastInsights.cs")

    readme = (ROOT / "README.md").read_text()
    if "**1.30.0**" not in readme:
        errors.append("README: missing 1.30.0")
    if "оценк" not in readme.lower():
        errors.append("README: missing оценка")

    download = (ROOT / "dist/download/README.md").read_text()
    if "1.30.0" not in download:
        errors.append("dist/download/README: missing 1.30.0")

    csproj = (ROOT / "Alfa156.Android/Alfa156.Android.csproj").read_text()
    if f"<ApplicationDisplayVersion>{version}</ApplicationDisplayVersion>" not in csproj:
        errors.append(f"Android csproj: missing ApplicationDisplayVersion {version}")

    # Logic sample: 60 days, +2400 km → 40 km/day; oil remaining 4000 → 100 days
    today = date(2026, 11, 19)
    readings = [
        (today - timedelta(days=60), 10000.0),
        (today - timedelta(days=30), 11200.0),
        (today, 12400.0),
    ]
    got = avg_km_per_day(readings, today)
    if got is None:
        errors.append("logic: expected avg from sample readings")
    else:
        avg, span = got
        if span != 60:
            errors.append(f"logic: expected span 60, got {span}")
        if not math.isclose(avg, 40.0, rel_tol=1e-9):
            errors.append(f"logic: expected avg 40, got {avg}")
        line = service_line("До замены масла", 12400.0, 8400.0, 10000.0, avg)
        if line != "До замены масла: ~6000 км ≈ 150 дн. (оценка)":
            errors.append(f"logic: unexpected oil line {line!r}")

    # Insufficient: only 10-day span
    short = avg_km_per_day(
        [(today - timedelta(days=10), 1000.0), (today, 1400.0)], today
    )
    if short is not None:
        errors.append("logic: short span should be insufficient")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK forecast insights checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
