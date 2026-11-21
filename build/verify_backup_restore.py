#!/usr/bin/env python3
"""Check restore safety and legacy receipt-folder behavior across clients."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def section(text: str, start: str, end: str, path: str) -> str:
    start_index = text.find(start)
    if start_index < 0:
        raise AssertionError(f"{path}: missing {start!r}")
    end_index = text.find(end, start_index + len(start))
    if end_index < 0:
        raise AssertionError(f"{path}: missing end marker {end!r}")
    return text[start_index:end_index]


def main() -> int:
    errors = []

    kpk_path = "Alfa156/Alfa156/DataStore.cs"
    kpk = (ROOT / kpk_path).read_text(encoding="utf-8-sig")
    receipt_copy = section(
        kpk,
        "private static void CopyReceiptsFolder(",
        "internal static string[] ListBackupNames()",
        kpk_path,
    )
    missing_receipts_guard = "if (!Directory.Exists(source))\n                return;"
    if missing_receipts_guard not in receipt_copy:
        errors.append("KPK: absent Receipts/ source must return without clearing destination")
    elif receipt_copy.index(missing_receipts_guard) > receipt_copy.index(
        "File.Delete(oldFiles[i])"
    ):
        errors.append("KPK: Receipts/ source guard must precede destination cleanup")

    kpk_restore = section(
        kpk,
        "private static void RestoreBackup(string folderName, string backupRoot)",
        "internal static void WriteSyncSnapshot",
        kpk_path,
    )
    for needle in (
        "AllocateSafetyBackupPath",
        "CopyBackupFiles(backupPath)",
        "CopyBackupFiles(safetyBackup)",
    ):
        if needle not in kpk_restore:
            errors.append(f"KPK restore: missing safety/rollback step {needle}")
    if kpk_restore.index("AllocateSafetyBackupPath") > kpk_restore.index(
        "CopyBackupFiles(backupPath)"
    ):
        errors.append("KPK restore: safety backup must be allocated before replacing data")

    android_path = "Alfa156.Android/ExpenseDataStore.cs"
    android = (ROOT / android_path).read_text(encoding="utf-8")
    android_copy = section(
        android,
        "private static void CopyData(string source, string destination)",
        "private static void CopyOptionalFile",
        android_path,
    )
    if android_copy.index("if (Directory.Exists(receiptsSource))") > android_copy.index(
        "Directory.Delete(receiptsDest, true)"
    ):
        errors.append("Android: source folder guard must precede receipt cleanup")

    android_restore = section(
        android,
        "internal static void RestoreBackup(string dataDirectory, string backupName)",
        "private static void CopyData(",
        android_path,
    )
    for needle in ("CreateSafetyBackup", "CopyData(source, dataDirectory)", "CopyData(safetyPath, dataDirectory)"):
        if needle not in android_restore:
            errors.append(f"Android restore: missing safety/rollback step {needle}")

    if errors:
        print("FAIL verify_backup_restore:")
        for error in errors:
            print(" ", error)
        return 1

    print("verify_backup_restore: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
