#!/usr/bin/env python3
"""Smoke-check SyncJournal preview formatting and Safety_ naming (no C# runtime)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

import l10n_resolver

l10n_resolver.install()

ROOT = Path(__file__).resolve().parents[1]


def must_contain(path: Path, needle: str) -> None:
    text = path.read_text(encoding="utf-8")
    if needle not in text:
        raise SystemExit(f"missing {needle!r} in {path.relative_to(ROOT)}")


def version_at_least(min_major: int, min_minor: int) -> None:
    raw = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    parts = raw.split(".")
    major, minor = int(parts[0]), int(parts[1])
    if major < min_major or (major == min_major and minor < min_minor):
        raise SystemExit(f"VERSION {raw!r} expected >= {min_major}.{min_minor}.0")


def main() -> int:
    version_at_least(1, 16)
    must_contain(ROOT / "Alfa156" / "Alfa156" / "SyncMerge.cs", "BuildPreview")
    must_contain(ROOT / "Alfa156.Desktop" / "Data" / "SyncMerge.cs", "BuildPreview")
    must_contain(ROOT / "Alfa156.Android" / "ExpenseSyncService.cs", "BuildPreview")
    must_contain(ROOT / "Alfa156.Android" / "RecordIds.cs", "ConflictKeptLocal")
    must_contain(ROOT / "Alfa156.Android" / "RecordIds.cs", "ConflictKeptIncoming")
    must_contain(ROOT / "Alfa156.Android" / "RecordIds.cs", "RollbackReason")
    must_contain(ROOT / "Alfa156.Android" / "RecordIds.cs", "Причина отката")
    must_contain(ROOT / "Alfa156.Desktop" / "Data" / "RecordIds.cs", "RollbackReason")
    must_contain(ROOT / "Alfa156" / "Alfa156" / "RecordIds.cs", "RollbackReason")
    must_contain(ROOT / "Alfa156.Android" / "ExpenseSyncService.cs", "ConflictKeptIncoming")
    must_contain(ROOT / "Alfa156.Desktop" / "Data" / "RecordIds.cs", "ConflictMerged")
    must_contain(ROOT / "Alfa156.Desktop" / "Data" / "SyncMerge.cs", "ConflictMerged")
    must_contain(ROOT / "Alfa156" / "Alfa156" / "RecordIds.cs", "оставлено локальное")
    must_contain(ROOT / "Alfa156" / "Alfa156" / "SyncMerge.cs", "ConflictKeptIncoming")
    must_contain(ROOT / "Alfa156.Android" / "ExpenseSyncService.cs", 'FormatExample("R", title, row)')
    must_contain(ROOT / "Alfa156" / "Alfa156" / "SyncMerge.cs", 'FormatExample("L", title, local)')
    must_contain(ROOT / "Alfa156.Desktop" / "Data" / "SyncMerge.cs", 'FormatExample("M", title, localRow)')
    must_contain(ROOT / "Alfa156.Android" / "ExpenseDataStore.cs", "Safety_")
    must_contain(ROOT / "Alfa156" / "Alfa156" / "DataStore.cs", "Safety_")
    must_contain(ROOT / "Alfa156.Desktop" / "Data" / "AlfaRepository.cs", "Safety_")
    must_contain(ROOT / "Alfa156" / "Alfa156" / "SyncOptionsForm.cs", "Применить")
    must_contain(ROOT / "Alfa156.Desktop" / "SyncOptionsForm.cs", "Применить")
    must_contain(ROOT / "Alfa156.Android" / "MainActivity.cs", "Применить")
    must_contain(ROOT / "Alfa156" / "Alfa156" / "RecordIds.cs", "FormatPreview")
    must_contain(ROOT / "Alfa156.Android" / "RecordIds.cs", "MaxExamples = 15")
    must_contain(ROOT / "README.md", "**1.16.0**")
    must_contain(ROOT / "README.md", "**1.17.0**")
    must_contain(ROOT / "Alfa156.Android" / "MainActivity.cs", "ShowGlobalSearch")
    must_contain(ROOT / "google-sync" / "Alfa156Sync.gs", "EXPENSE_PREFIX")

    safety_re = re.compile(r'Safety_"\s*\+\s*\n?\s*DateTime\.Now\.ToString\("yyyyMMdd_HHmmss"')
    for rel in (
        "Alfa156/Alfa156/DataStore.cs",
        "Alfa156.Android/ExpenseDataStore.cs",
        "Alfa156.Desktop/Data/AlfaRepository.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        if not safety_re.search(text):
            raise SystemExit(f"Safety_yyyyMMdd_HHmmss pattern not found in {rel}")

    print("verify_sync_preview: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
