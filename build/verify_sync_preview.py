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
    must_contain(ROOT / "KiloLedger" / "KiloLedger" / "SyncMerge.cs", "BuildPreview")
    must_contain(ROOT / "KiloLedger.Desktop" / "Data" / "SyncMerge.cs", "BuildPreview")
    must_contain(ROOT / "KiloLedger.Android" / "ExpenseSyncService.cs", "BuildPreview")
    must_contain(ROOT / "KiloLedger.Android" / "RecordIds.cs", "ConflictKeptLocal")
    must_contain(ROOT / "KiloLedger.Android" / "RecordIds.cs", "ConflictKeptIncoming")
    must_contain(ROOT / "KiloLedger.Android" / "RecordIds.cs", "RollbackReason")
    must_contain(ROOT / "KiloLedger.Android" / "RecordIds.cs", "Причина отката")
    must_contain(ROOT / "KiloLedger.Desktop" / "Data" / "RecordIds.cs", "RollbackReason")
    must_contain(ROOT / "KiloLedger" / "KiloLedger" / "RecordIds.cs", "RollbackReason")
    must_contain(ROOT / "KiloLedger.Android" / "ExpenseSyncService.cs", "ConflictKeptIncoming")
    must_contain(ROOT / "KiloLedger.Desktop" / "Data" / "RecordIds.cs", "ConflictMerged")
    must_contain(ROOT / "KiloLedger.Desktop" / "Data" / "SyncMerge.cs", "ConflictMerged")
    must_contain(ROOT / "KiloLedger" / "KiloLedger" / "RecordIds.cs", "оставлено локальное")
    must_contain(ROOT / "KiloLedger" / "KiloLedger" / "SyncMerge.cs", "ConflictKeptIncoming")
    must_contain(ROOT / "KiloLedger.Android" / "ExpenseSyncService.cs", 'FormatExample("R", title, row)')
    must_contain(ROOT / "KiloLedger" / "KiloLedger" / "SyncMerge.cs", 'FormatExample("L", title, local)')
    must_contain(ROOT / "KiloLedger.Desktop" / "Data" / "SyncMerge.cs", 'FormatExample("M", title, localRow)')
    must_contain(ROOT / "KiloLedger.Android" / "ExpenseDataStore.cs", "Safety_")
    must_contain(ROOT / "KiloLedger" / "KiloLedger" / "DataStore.cs", "Safety_")
    must_contain(ROOT / "KiloLedger.Desktop" / "Data" / "KiloLedgerRepository.cs", "Safety_")
    must_contain(ROOT / "KiloLedger" / "KiloLedger" / "SyncOptionsForm.cs", "Применить")
    must_contain(ROOT / "KiloLedger.Desktop" / "SyncOptionsForm.cs", "Применить")
    must_contain(ROOT / "KiloLedger.Android" / "MainActivity.cs", "Применить")
    must_contain(ROOT / "KiloLedger" / "KiloLedger" / "RecordIds.cs", "FormatPreview")
    must_contain(ROOT / "KiloLedger.Android" / "RecordIds.cs", "MaxExamples = 15")
    must_contain(ROOT / "README.ru.md", "**1.16.0**")
    must_contain(ROOT / "README.ru.md", "**1.17.0**")
    must_contain(ROOT / "KiloLedger.Android" / "MainActivity.cs", "ShowGlobalSearch")
    must_contain(ROOT / "google-sync" / "KiloLedgerSync.gs", "EXPENSE_PREFIX")

    safety_re = re.compile(r'Safety_"\s*\+\s*\n?\s*DateTime\.Now\.ToString\("yyyyMMdd_HHmmss"')
    for rel in (
        "KiloLedger/KiloLedger/DataStore.cs",
        "KiloLedger.Android/ExpenseDataStore.cs",
        "KiloLedger.Desktop/Data/KiloLedgerRepository.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        if not safety_re.search(text):
            raise SystemExit(f"Safety_yyyyMMdd_HHmmss pattern not found in {rel}")

    print("verify_sync_preview: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
