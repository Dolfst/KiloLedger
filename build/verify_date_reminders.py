#!/usr/bin/env python3
"""Sanity-check date reminders (1.26.0)."""
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
        ok = major > 1 or (major == 1 and minor >= 26)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.26.0, got {version!r}")

    for rel in (
        "Alfa156/Alfa156/DateReminders.cs",
        "Alfa156.Android/DateReminders.cs",
        "Alfa156.Desktop/Data/DateReminders.cs",
    ):
        text = (ROOT / rel).read_text()
        for needle in (
            'FileName = "reminders.dat"',
            "TypeInsurance",
            "TypeInspection",
            "TypeCustom",
            "IsValidRow",
            "FormatUpcomingBlock",
            "DaysUntil",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    for rel in (
        "Alfa156/Alfa156/ToSettings.cs",
        "Alfa156.Android/ToSettings.cs",
        "Alfa156.Desktop/Data/ToSettings.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'ReminderWarnDaysKey = "reminder_warn_days"' not in text:
            errors.append(f"{rel}: missing ReminderWarnDaysKey")
        if "GetReminderWarnDays" not in text or "SetReminderWarnDays" not in text:
            errors.append(f"{rel}: missing GetReminderWarnDays/SetReminderWarnDays")
        if "DefaultReminderWarnDays = 14" not in text:
            errors.append(f"{rel}: missing DefaultReminderWarnDays = 14")
        if "Decimal.Truncate(value)" not in text:
            errors.append(f"{rel}: reminder warning days must be an integer")

    for rel in (
        "Alfa156/Alfa156/RecordIds.cs",
        "Alfa156.Android/RecordIds.cs",
        "Alfa156.Desktop/Data/RecordIds.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'TypeReminder = "reminder"' not in text:
            errors.append(f"{rel}: missing TypeReminder")
        if 'return "n_"' not in text and 'RemindersFile) return "n_"' not in text and \
                'reminders.dat") return "n_"' not in text:
            errors.append(f"{rel}: missing n_ prefix for reminders")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        "ManageDateReminders",
        "RefreshDateReminderNotifications",
        "EditReminderWarnDays",
        "Напоминания",
        "reminders.dat",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")
    if f'version = "{version}"' not in android:
        errors.append(f"Android MainActivity: missing version {version}")

    for rel in (
        "Alfa156/Alfa156/DateReminders.cs",
        "Alfa156.Android/DateReminders.cs",
        "Alfa156.Desktop/Data/DateReminders.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'lower == "osago"' not in text or 'lower == "осago"' not in text:
            errors.append(f"{rel}: missing cross-platform OSAGO aliases")
        if 'lower == "inspection"' not in text:
            errors.append(f"{rel}: missing English inspection alias")
        if "не более 80 символов" not in text:
            errors.append(f"{rel}: reminder titles must not be silently truncated")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "AddDateReminder",
        "DeleteDateReminder",
        "SaveReminderWarnDays",
        "button.add_reminder",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")
    if f'AppVersion = "{version}"' not in desktop:
        errors.append(f"Desktop MainForm: missing AppVersion {version}")

    kpk = (ROOT / "Alfa156/Alfa156/Form1.cs").read_text()
    for needle in (
        "ManageRemindersFromMenu",
        "Напоминания",
        "EditReminderWarnDays",
        version,
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    for rel, needle in (
        ("Alfa156.Android/ExpenseDataCodec.cs", "RemindersFile"),
        ("Alfa156.Desktop/Data/AlfaCodec.cs", "RemindersFile"),
        ("Alfa156/Alfa156/DataStore.cs", "RemindersFile"),
        ("Alfa156.Android/ExpenseSyncService.cs", "RemindersFile"),
        ("Alfa156/Alfa156/SyncMerge.cs", "reminders.dat"),
        ("build/build-kpk.ps1", "DateReminders.cs"),
    ):
        if needle not in (ROOT / rel).read_text():
            errors.append(f"{rel}: missing {needle!r}")

    readme = (ROOT / "README.ru.md").read_text()
    if "**1.26.0**" not in readme:
        errors.append("README: missing 1.26.0")
    if "reminders.dat" not in readme:
        errors.append("README: missing reminders.dat")
    if "reminder_warn_days" not in readme:
        errors.append("README: missing reminder_warn_days")

    # Format rule: date|car|title|type|id → 4 content fields
    sample = "2026-12-01|Alfa|ОСАГО|insurance|n_abcd1234"
    parts = sample.split("|")
    if len(parts) != 5:
        errors.append("sample reminder line should have 5 fields")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK date reminders checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
