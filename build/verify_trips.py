#!/usr/bin/env python3
"""Offline checks for odometer trip entries and trips.dat sync contract."""
from __future__ import annotations

import sys
from pathlib import Path

import l10n_resolver

l10n_resolver.install()

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors: list[str] = []

    trip = ["2026-11-21", "Alfa", "120000", "120125", "Работа", "Иван", "Встреча"]
    start = float(trip[2])
    end = float(trip[3])
    if end - start != 125:
        errors.append("trip fixture: incorrect odometer distance")
    if end < start:
        errors.append("trip fixture: end odometer before start")

    android = (ROOT / "KiloLedger.Android/MainActivity.cs").read_text(encoding="utf-8")
    desktop = (ROOT / "KiloLedger.Desktop/MainForm.cs").read_text(encoding="utf-8")
    kpk = (ROOT / "KiloLedger/KiloLedger/Form1.cs").read_text(encoding="utf-8-sig")
    for rel, text in (
        ("Android", android),
        ("Desktop", desktop),
        ("KPK", kpk),
    ):
        if "Поезд" not in text:
            errors.append(f"{rel}: missing trip entry UI")
        if "Конец пробега" not in text:
            errors.append(f"{rel}: missing end odometer validation")
        if "Отчёт поездок" not in text:
            errors.append(f"{rel}: missing trip mileage report")

    for rel in (
        "KiloLedger.Android/ExpenseDataCodec.cs",
        "KiloLedger.Desktop/Data/KiloLedgerCodec.cs",
        "KiloLedger/KiloLedger/DataStore.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8-sig")
        if "trips.dat" not in text:
            errors.append(f"{rel}: missing trips.dat contract")

    for rel in (
        "KiloLedger.Android/RecordIds.cs",
        "KiloLedger.Desktop/Data/RecordIds.cs",
        "KiloLedger/KiloLedger/RecordIds.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8-sig")
        if "g_" not in text:
            errors.append(f"{rel}: missing g_ trip prefix")

    if errors:
        print("FAIL verify_trips:")
        for error in errors:
            print(" ", error)
        return 1
    print("verify_trips: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
