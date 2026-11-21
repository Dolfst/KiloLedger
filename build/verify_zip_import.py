#!/usr/bin/env python3
"""Verify Desktop ZIP import validates backup archive paths and contents."""
from __future__ import annotations

import sys
from pathlib import Path

import l10n_resolver

l10n_resolver.install()

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors = []
    path = ROOT / "Alfa156.Desktop/Exchange/ImportPreview.cs"
    source = path.read_text(encoding="utf-8")
    for token in (
        "ValidateZipEntries(archive)",
        'entryName.IndexOf(\'\\\\\') >= 0',
        'parts[p] == ".."',
        "несколько корневых папок",
        "ReceiptStore.IsSafeReceiptFileName(relative[1])",
        "Повторяющийся файл в ZIP",
    ):
        if token not in source:
            errors.append(f"ImportPreview.cs: missing ZIP validation {token!r}")

    if errors:
        print("FAIL verify_zip_import:")
        for error in errors:
            print(" ", error)
        return 1

    print("verify_zip_import: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
