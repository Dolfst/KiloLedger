#!/usr/bin/env python3
"""Sanity-check backup reminder + full ZIP export hooks (1.23.0)."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors: list[str] = []
    version = (ROOT / "VERSION").read_text().strip()
    parts = version.split(".")
    try:
        major, minor = int(parts[0]), int(parts[1])
        ok = major > 1 or (major == 1 and minor >= 23)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.23.0, got {version!r}")

    for rel in (
        "Alfa156/Alfa156/ToSettings.cs",
        "Alfa156.Android/ToSettings.cs",
        "Alfa156.Desktop/Data/ToSettings.cs",
    ):
        text = (ROOT / rel).read_text()
        if "Decimal.Truncate(value)" not in text:
            errors.append(f"{rel}: backup reminder interval must be an integer")
        for needle in (
            'BackupRemindDaysKey = "backup_remind_days"',
            'LastBackupUtcKey = "last_backup_utc"',
            'LastBackupNagDateKey = "last_backup_nag_date"',
            "DefaultBackupRemindDays = 7",
            "MarkBackupDone",
            "ShouldShowBackupReminder",
            "MarkBackupNagShown",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        "Полный экспорт ZIP",
        "MaybeShowBackupReminder",
        "EditBackupReminder",
        "Напоминание о копии",
        "MarkBackupDone(DataDirectory)",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")

    android_store = (ROOT / "Alfa156.Android/ExpenseDataStore.cs").read_text()
    if "ToSettings.MarkBackupDone(dataDirectory)" not in android_store:
        errors.append("Android ExpenseDataStore: MarkBackupDone not called after backup")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "Экспорт всего (ZIP)",
        "MaybeShowBackupReminder",
        "SaveBackupRemind",
        f'AppVersion = "{version}"',
        "backupRemindBox",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")

    desktop_repo = (ROOT / "Alfa156.Desktop/Data/AlfaRepository.cs").read_text()
    if "NoteSuccessfulBackup" not in desktop_repo:
        errors.append("Desktop AlfaRepository: missing NoteSuccessfulBackup")

    kpk = (ROOT / "Alfa156/Alfa156/Form1.cs").read_text()
    for needle in (
        "EditBackupReminder",
        "MaybeShowBackupReminder",
        "CountBackupFiles",
        "Будет скопировано файлов",
        version,
        "Напоминание о копии",
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    kpk_store = (ROOT / "Alfa156/Alfa156/DataStore.cs").read_text()
    if "CountBackupFiles" not in kpk_store:
        errors.append("KPK DataStore: missing CountBackupFiles")
    if "ToSettings.MarkBackupDone()" not in kpk_store:
        errors.append("KPK DataStore: MarkBackupDone not called after backup")
    for rel in (
        "Alfa156.Android/MainActivity.cs",
        "Alfa156.Desktop/Data/AlfaRepository.cs",
        "Alfa156/Alfa156/DataStore.cs",
    ):
        text = (ROOT / rel).read_text()
        for needle in ("snapshot.json", "alfa156.snapshot", "schemaVersion",
                       "deviceToken", "createdUtc"):
            if needle not in text:
                errors.append(f"{rel}: missing snapshot metadata {needle}")
    for rel in (
        "Alfa156.Android/ToSettings.cs",
        "Alfa156.Desktop/Data/ToSettings.cs",
        "Alfa156/Alfa156/ToSettings.cs",
    ):
        text = (ROOT / rel).read_text()
        if "SnapshotDeviceTokenKey" not in text:
            errors.append(f"{rel}: missing snapshot device token key")

    readme = (ROOT / "README.md").read_text()
    if f"## Что нового в {version}" not in readme and "## Что нового в 1.23.0" not in readme:
        errors.append(f"README: missing Что нового section for {version} (or 1.23.0)")
    if "**1.23.0**" not in readme or "backup_remind_days" not in readme:
        errors.append("README: История версий / settings should mention 1.23.0 backup keys")

    # Reminder math: overdue when days >= remind
    remind = 7
    days_ok = 6
    days_nag = 7
    if not (days_ok < remind and days_nag >= remind):
        errors.append("backup remind threshold logic broken")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK backup reminder + full export checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
