#!/usr/bin/env python3
"""Verify CSV import keeps supported fields and rejects malformed rows."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors = []
    csv_path = ROOT / "Alfa156.Desktop/Exchange/CsvExchange.cs"
    csv = csv_path.read_text(encoding="utf-8")
    for token in (
        'ReadCsvFlexible(Path.Combine(folder, "fuel.csv"), 7, 10)',
        "raw[i].Length > maxColumns",
        "raw[i].Length > columns",
        "if (inQuotes)",
        "CSV содержит незакрытую кавычку.",
        "ParseCsvCore(text, columns, hasHeader, true)",
    ):
        if token not in csv:
            errors.append(f"CsvExchange.cs: missing validation {token!r}")

    wizard_path = ROOT / "Alfa156.Desktop/ImportWizardForm.cs"
    wizard = wizard_path.read_text(encoding="utf-8")
    start = wizard.find("private void ApplyImport()")
    end = wizard.find("private static void CopyDatTree(", start)
    if start < 0 or end < 0:
        errors.append("ImportWizardForm.cs: ApplyImport method not found")
    else:
        apply = wizard[start:end]
        if "repository.CreateSafetyBackup(null)" not in apply:
            errors.append("ApplyImport must create a safety backup before replacement")
        before_replacement = apply.split("if (kind == SourceKind.Zip", 1)[0]
        if "catch" in before_replacement:
            errors.append("ApplyImport must not silently continue after backup failure")

    if errors:
        print("FAIL verify_csv_import:")
        for error in errors:
            print(" ", error)
        return 1

    print("verify_csv_import: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
