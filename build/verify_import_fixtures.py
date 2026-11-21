#!/usr/bin/env python3
"""Verify stable import fixtures for required .dat files and optional CSV fields."""
from __future__ import annotations

import csv
import io
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FIXTURES = ROOT / "build" / "sample_data"
REQUIRED_DAT = ("cars.dat", "fuel.dat", "repairs.dat", "utilities.dat")


def verify_dat_fixture() -> list[str]:
    errors: list[str] = []
    ids: dict[str, set[str]] = {}
    prefixes = {
        "cars.dat": "c_",
        "fuel.dat": "f_",
        "repairs.dat": "r_",
        "expenses.dat": "e_",
        "utilities.dat": "u_",
    }
    for name in REQUIRED_DAT + ("expenses.dat",):
        path = FIXTURES / name
        if not path.exists():
            errors.append(f"missing fixture {name}")
            continue
        for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            fields = line.split("|")
            record_id = fields[-1].strip() if fields else ""
            prefix = prefixes[name]
            if not record_id.startswith(prefix):
                errors.append(f"{name}:{line_number}: expected id prefix {prefix}")
                continue
            if record_id in ids.setdefault(name, set()):
                errors.append(f"{name}:{line_number}: duplicate id {record_id}")
            ids[name].add(record_id)

    return errors


def verify_csv_fixture() -> list[str]:
    errors: list[str] = []
    path = FIXTURES / "fuel_optional.csv"
    with path.open("r", encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if len(rows) != 1:
        return [f"{path.name}: expected one fixture row"]
    row = rows[0]
    required = ("date", "car", "km", "liters", "cost")
    optional = ("price", "comment", "fullTank", "purpose", "station")
    missing = [name for name in required if not (row.get(name) or "").strip()]
    if missing:
        errors.append(f"{path.name}: missing required fields {', '.join(missing)}")
    for name in optional:
        if name not in row:
            errors.append(f"{path.name}: missing optional column {name}")
    if row.get("fullTank") != "0":
        errors.append(f"{path.name}: fullTank fixture must exercise partial fill")
    if row.get("station") != "A-100":
        errors.append(f"{path.name}: station fixture not preserved")
    return errors


def verify_archive_round_trip() -> list[str]:
    errors: list[str] = []
    archive = io.BytesIO()
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as package:
        for name in REQUIRED_DAT + ("expenses.dat",):
            package.write(FIXTURES / name, arcname=name)
    archive.seek(0)
    with zipfile.ZipFile(archive, "r") as package:
        names = set(package.namelist())
        missing = [name for name in REQUIRED_DAT + ("expenses.dat",) if name not in names]
        if missing:
            errors.append("fixture ZIP missing " + ", ".join(missing))
        for name in names:
            info = package.getinfo(name)
            if info.file_size <= 0:
                errors.append(f"fixture ZIP contains empty {name}")
    return errors


def main() -> int:
    errors = verify_dat_fixture() + verify_csv_fixture() + verify_archive_round_trip()
    if errors:
        print("FAIL verify_import_fixtures:")
        for error in errors:
            print(" ", error)
        return 1
    print("verify_import_fixtures: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
