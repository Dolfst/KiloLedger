#!/usr/bin/env python3
"""Cross-platform scenario checks over shared sample_data fixtures (stage E).

Recomputes the period report numbers in pure Python the same way
PeriodCompare.Build + PeriodReport do on Android/Desktop/КПК and asserts the
sample snapshot produces identical values. Run before a version bump to prove
that one snapshot yields the same report on all platforms.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

import l10n_resolver

l10n_resolver.install()

ROOT = Path(__file__).resolve().parents[1]
SAMPLE = ROOT / "build" / "sample_data"

ID_RE = re.compile(r"^[a-z]_[0-9a-fA-F]{8,}$")


def parse_row(line: str) -> list[str]:
    fields: list[str] = []
    buf: list[str] = []
    escaped = False
    for ch in line:
        if escaped:
            buf.append(ch)
            escaped = False
        elif ch == "\\":
            escaped = True
        elif ch == "|":
            fields.append("".join(buf))
            buf = []
        else:
            buf.append(ch)
    if escaped:
        buf.append("\\")
    fields.append("".join(buf))
    return fields


def load(name: str) -> list[list[str]]:
    path = SAMPLE / name
    if not path.exists():
        return []
    return [parse_row(line) for line in path.read_text(encoding="utf-8").splitlines() if line]


def strip_id(row: list[str]) -> list[str]:
    if row and ID_RE.match(row[-1]):
        return row[:-1]
    return row


def num(value: str) -> float:
    try:
        return float((value or "0").replace(",", "."))
    except ValueError:
        return 0.0


def in_range(day: str, from_d: str, to_d: str) -> bool:
    d = day[:10]
    return (not from_d or d >= from_d) and (not to_d or d <= to_d)


def is_full_tank(row: list[str]) -> bool:
    content = strip_id(row)
    if len(content) <= 7:
        return True
    flag = (content[7] or "").strip()
    return flag == "" or flag == "1"


def totals(car: str, from_d: str, to_d: str) -> dict:
    result = {
        "fuel": 0.0, "liters": 0.0, "repair": 0.0, "expense": 0.0,
        "personal_km": 0.0, "work_km": 0.0,
        "consumption": None,
    }
    fills: list[tuple[float, float, int]] = []
    for row in load("fuel.dat"):
        content = strip_id(row)
        if len(content) < 5:
            continue
        if content[1].strip() != car or not in_range(content[0], from_d, to_d):
            continue
        result["fuel"] += num(content[4])
        liters = num(content[3])
        result["liters"] += liters
        km = num(content[2])
        if liters > 0:
            fills.append((km, liters, 1 if is_full_tank(row) else 0))
    fulls = [f for f in fills if f[2] == 1]
    if len(fulls) >= 2:
        min_km = min(f[0] for f in fulls)
        max_km = max(f[0] for f in fulls)
        if max_km > min_km:
            liters = sum(f[1] for f in fills if min_km < f[0] <= max_km)
            if liters > 0:
                result["consumption"] = liters * 100.0 / (max_km - min_km)
    for row in load("repairs.dat"):
        content = strip_id(row)
        if len(content) >= 8 and content[1].strip() == car and in_range(content[0], from_d, to_d):
            result["repair"] += num(content[7])
    for row in load("expenses.dat"):
        content = strip_id(row)
        if len(content) >= 5 and content[1].strip() == car and in_range(content[0], from_d, to_d):
            result["expense"] += num(content[4])
    for row in load("trips.dat"):
        content = strip_id(row)
        if len(content) < 7 or content[1].strip() != car or not in_range(content[0], from_d, to_d):
            continue
        start_km, end_km = num(content[2]), num(content[3])
        if end_km < start_km:
            continue
        key = "work_km" if content[4].strip().lower() == "работа" else "personal_km"
        result[key] += end_km - start_km
    return result


def main() -> int:
    errors: list[str] = []
    version = (ROOT / "VERSION").read_text().strip()

    t = totals("Alfa", "2026-10-01", "2026-10-31")
    expected = {
        "fuel": 225.0, "liters": 75.0, "repair": 80.0, "expense": 25.0,
        # full tanks at 10000 (40 л) and 10300 (35 л) -> 35 л over 300 км
        "consumption": 35.0 * 100.0 / 300.0,
        "personal_km": 0.0, "work_km": 0.0,
    }
    for key, want in expected.items():
        got = t[key]
        ok = got == want if want is None else got is not None and abs(got - want) < 0.01
        if not ok:
            errors.append(f"scenario period 2026-10: {key}: expected {want!r}, got {got!r}")

    # report must mention every produced metric; all three clients format the same labels
    for rel in (
        "KiloLedger/KiloLedger/PeriodReport.cs",
        "KiloLedger.Android/PeriodReport.cs",
        "KiloLedger.Desktop/Data/PeriodReport.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        for needle in ("Топливо", "Заправлено", "Расход", "Бюджет", "Поездки личные"):
            if needle not in text:
                errors.append(f"{rel}: missing label {needle!r}")

    # version gate: scenario checks run before any version bump
    if not re.match(r"^\d+\.\d+\.\d+$", version):
        errors.append(f"VERSION must be X.Y.Z before bump, got {version!r}")

    if errors:
        print("\n".join(errors))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
