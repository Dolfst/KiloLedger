#!/usr/bin/env python3
"""Check docs/snapshot-schema.md matches snapshot producers and sample data."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOC = ROOT / "docs" / "snapshot-schema.md"

FILES = (
    "cars.dat", "fuel.dat", "repairs.dat", "utilities.dat", "repair-categories.dat",
    "expenses.dat", "expense-categories.dat", "settings.dat", "cars-meta.dat",
    "templates.dat", "reminders.dat", "notes.dat", "trips.dat", "deleted.dat",
)
PREFIXES = ("c_", "f_", "r_", "e_", "u_", "g_", "m_", "k_", "p_", "t_", "n_", "j_")
FIELDS = ("schema", "schemaVersion", "appVersion", "createdUtc", "deviceToken")
PRODUCERS = (
    "KiloLedger/KiloLedger/DataStore.cs",
    "KiloLedger.Android/MainActivity.cs",
    "KiloLedger.Desktop/Data/KiloLedgerRepository.cs",
)


def main() -> int:
    errors: list[str] = []
    doc = DOC.read_text(encoding="utf-8")
    for name in FILES:
        if name == "settings.dat" or name in doc:
            continue
        # categories are documented together in one row
        errors.append(f"doc: file {name!r} not documented")
    for prefix in PREFIXES:
        if f"`{prefix}`" not in doc:
            errors.append(f"doc: id prefix {prefix!r} not documented")
    for field in FIELDS:
        if f"`{field}`" not in doc:
            errors.append(f"doc: snapshot field {field!r} not documented")
    if '"schemaVersion":1' not in doc:
        errors.append("doc: schemaVersion 1 example missing")

    for rel in PRODUCERS:
        text = (ROOT / rel).read_text(encoding="utf-8")
        if 'kiloledger.snapshot' not in text or 'schemaVersion\\":1' not in text:
            errors.append(f"{rel}: snapshot producer does not match schemaVersion 1")
        for field in FIELDS:
            if field not in text:
                errors.append(f"{rel}: missing snapshot field {field!r}")

    fuel = (ROOT / "build/sample_data/fuel.dat").read_text(encoding="utf-8").splitlines()
    for line in fuel:
        if line and len(re.split(r"(?<!\\)\|", line)) != 11:
            errors.append(f"sample fuel.dat row does not match documented layout: {line!r}")

    if errors:
        print("\n".join(errors))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
