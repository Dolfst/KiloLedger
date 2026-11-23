#!/usr/bin/env python3
"""Check restore safety and legacy receipt-folder behavior across clients."""
from __future__ import annotations

import sys
import tempfile
import zipfile
from pathlib import Path

import l10n_resolver

l10n_resolver.install()

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
    fixtures = ROOT / "build" / "sample_data"

    with tempfile.TemporaryDirectory(prefix="kiloledger-restore-fixture-") as tmp:
        root = Path(tmp)
        restore = root / "restore"
        restore.mkdir()
        current_receipts = restore / "Receipts"
        current_receipts.mkdir()
        (current_receipts / "f_old00123.jpg").write_bytes(b"current receipt")

        legacy_zip = root / "legacy.zip"
        with zipfile.ZipFile(legacy_zip, "w", zipfile.ZIP_DEFLATED) as archive:
            for name in ("cars.dat", "fuel.dat", "repairs.dat", "expenses.dat", "utilities.dat"):
                archive.write(fixtures / name, arcname=name)

        with zipfile.ZipFile(legacy_zip, "r") as archive:
            archive.extractall(restore)
        if not (restore / "fuel.dat").exists():
            errors.append("fixture restore: legacy ZIP did not extract dat files")
        # Legacy backups contain no Receipts/: current receipts must be preserved.
        if not (current_receipts / "f_old00123.jpg").exists():
            errors.append("fixture restore: legacy restore removed current receipts")

        modern_zip = root / "modern.zip"
        modern_receipts = root / "Receipts"
        modern_receipts.mkdir()
        (modern_receipts / "f_fixture123.jpg").write_bytes(b"fixture receipt")
        with zipfile.ZipFile(modern_zip, "w", zipfile.ZIP_DEFLATED) as archive:
            for name in ("cars.dat", "fuel.dat", "repairs.dat", "expenses.dat", "utilities.dat"):
                archive.write(fixtures / name, arcname=name)
            archive.write(modern_receipts / "f_fixture123.jpg",
                          arcname="Receipts/f_fixture123.jpg")

        with zipfile.ZipFile(modern_zip, "r") as archive:
            archive.extractall(restore)
        if not (restore / "Receipts" / "f_fixture123.jpg").exists():
            errors.append("fixture restore: modern ZIP receipt was not restored")

    kpk_path = "KiloLedger/KiloLedger/DataStore.cs"
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

    android_path = "KiloLedger.Android/ExpenseDataStore.cs"
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

    integrity_paths = (
        "KiloLedger.Android/DataIntegrity.cs",
        "KiloLedger.Desktop/Data/DataIntegrity.cs",
        "KiloLedger/KiloLedger/DataIntegrity.cs",
    )
    for rel in integrity_paths:
        source = (ROOT / rel).read_text(encoding="utf-8-sig")
        for needle in (
            "чек с некорректным именем",
            "файл чека без записи",
            "пустой файл чека",
            '"warn"',
        ):
            if needle not in source:
                errors.append(f"{rel}: missing receipt integrity check {needle}")

    desktop_receipts = (ROOT / "KiloLedger.Desktop/Data/ReceiptStore.cs").read_text(encoding="utf-8")
    desktop_main = (ROOT / "KiloLedger.Desktop/MainForm.cs").read_text(encoding="utf-8")
    if "internal static void Delete(string dataDirectory, string recordId)" not in desktop_receipts:
        errors.append("Desktop: receipt Delete method missing")
    if "Удалить фото" not in desktop_main or "DeleteReceiptPhoto" not in desktop_main:
        errors.append("Desktop: receipt delete action missing")
    android_receipts = (ROOT / "KiloLedger.Android/ReceiptStore.cs").read_text(encoding="utf-8")
    android_main = (ROOT / "KiloLedger.Android/MainActivity.cs").read_text(encoding="utf-8")
    if "ReceiptStore.Delete(DataDirectory, editorReceiptId)" not in android_main:
        errors.append("Android: receipt delete action missing")
    if "IsSafeReceiptFileName" not in android_receipts:
        errors.append("Android: safe receipt filename contract missing")
    kpk_receipts = (ROOT / "KiloLedger/KiloLedger/DataStore.cs").read_text(encoding="utf-8-sig")
    kpk_main = (ROOT / "KiloLedger/KiloLedger/Form1.cs").read_text(encoding="utf-8-sig")
    if "internal static void DeleteReceipt(string recordId)" not in kpk_receipts:
        errors.append("KPK: receipt Delete method missing")
    if "OpenReceiptPhoto" not in kpk_main or "DeleteReceiptPhoto" not in kpk_main:
        errors.append("KPK: receipt open/delete actions missing")

    if errors:
        print("FAIL verify_backup_restore:")
        for error in errors:
            print(" ", error)
        return 1

    print("verify_backup_restore: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
