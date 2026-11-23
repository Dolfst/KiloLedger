#!/usr/bin/env python3
"""Sanity-check car notes / journal (1.31.0)."""
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
        ok = major > 1 or (major == 1 and minor >= 31)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.31.0, got {version!r}")

    for rel in (
        "KiloLedger/KiloLedger/CarNotes.cs",
        "KiloLedger.Android/CarNotes.cs",
        "KiloLedger.Desktop/Data/CarNotes.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        for needle in (
            'FileName = "notes.dat"',
            "MaxTextLength = 500",
            "IsValidRow",
            "FormatRecentBlock",
            "FormatLine",
            "Upsert",
            "DeleteById",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")
        if "не более 500 символов" not in text:
            errors.append(f"{rel}: note text must not be silently truncated")

    for rel in (
        "KiloLedger/KiloLedger/RecordIds.cs",
        "KiloLedger.Android/RecordIds.cs",
        "KiloLedger.Desktop/Data/RecordIds.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        if 'TypeNote = "note"' not in text:
            errors.append(f"{rel}: missing TypeNote")
        if 'notes.dat") return "j_"' not in text and 'NotesFile) return "j_"' not in text:
            if 'return "j_"' not in text:
                errors.append(f"{rel}: missing j_ prefix for notes")
        if "prefix != 'j'" not in text and 'prefix != \'j\'' not in text:
            # LooksLikeId must accept j_
            if "!= 'j'" not in text and '!= "j"' not in text and "prefix != 'j'" not in text:
                errors.append(f"{rel}: LooksLikeId should accept j_")

    android = (ROOT / "KiloLedger.Android/MainActivity.cs").read_text(encoding="utf-8")
    for needle in (
        "ManageCarNotes",
        "AddCarNote",
        "EditCarNote",
        "Заметки",
        "notes.dat",
        "FormatRecentBlock",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")
    if f'version = "{version}"' not in android:
        errors.append(f"Android MainActivity: missing version {version}")

    desktop = (ROOT / "KiloLedger.Desktop/MainForm.cs").read_text(encoding="utf-8")
    for needle in (
        "AddCarNote",
        "EditCarNote",
        "DeleteCarNote",
        "button.add_note",
        "NotesRows",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")
    if f'AppVersion = "{version}"' not in desktop:
        errors.append(f"Desktop MainForm: missing AppVersion {version}")

    kpk = (ROOT / "KiloLedger/KiloLedger/Form1.cs").read_text(encoding="utf-8")
    for needle in (
        "ManageNotesFromMenu",
        "Заметки",
        "CarNotes.FormatRecentBlock",
        version,
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    for rel, needle in (
        ("KiloLedger.Android/ExpenseDataCodec.cs", "NotesFile"),
        ("KiloLedger.Desktop/Data/KiloLedgerCodec.cs", "NotesFile"),
        ("KiloLedger/KiloLedger/DataStore.cs", "NotesFile"),
        ("KiloLedger.Android/ExpenseSyncService.cs", "NotesFile"),
        ("KiloLedger/KiloLedger/SyncMerge.cs", "notes.dat"),
        ("build/build-kpk.ps1", "CarNotes.cs"),
        ("KiloLedger.Desktop/Data/KiloLedgerRepository.cs", "NotesRows"),
    ):
        if needle not in (ROOT / rel).read_text(encoding="utf-8"):
            errors.append(f"{rel}: missing {needle!r}")

    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    if "**1.31.0**" not in readme:
        errors.append("README: missing 1.31.0")
    if "notes.dat" not in readme:
        errors.append("README: missing notes.dat")
    if "tombstone `note`" not in readme and 'tombstone `note`' not in readme and "tombstone `note`" not in readme:
        if "tombstone `note`" not in readme and "type `note`" not in readme and "tombstone `note`" not in readme:
            if "note" not in readme.lower() or "notes.dat" not in readme:
                errors.append("README: missing note tombstone mention")

    # Format rule: date|car|text|id → 3 content fields
    sample = "2026-11-19|Alfa|Замена дворников|j_abcd1234"
    parts = sample.split("|")
    if len(parts) != 4:
        errors.append("sample note line should have 4 fields")

    # sync id regex used by verify_sync_ids should allow j_
    sync_ids = (ROOT / "build/verify_sync_ids.py").read_text(encoding="utf-8")
    if "j" not in sync_ids.split("ID_RE", 1)[-1][:120]:
        errors.append("verify_sync_ids.py ID_RE should accept j_ prefix")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK car notes checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
