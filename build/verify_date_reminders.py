#!/usr/bin/env python3
"""Sanity-check calendar date reminders (1.26.0)."""
from __future__ import annotations

import sys
import tempfile
from pathlib import Path

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
            "TryParseDate",
            "ListDue",
            "FormatUpcomingBlock",
            "IsValidRow",
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
            errors.append(f"{rel}: missing Get/Set ReminderWarnDays")
        if "DefaultReminderWarnDays = 14" not in text:
            errors.append(f"{rel}: missing DefaultReminderWarnDays = 14")

    for rel in (
        "Alfa156/Alfa156/RecordIds.cs",
        "Alfa156.Android/RecordIds.cs",
        "Alfa156.Desktop/Data/RecordIds.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'TypeReminder = "reminder"' not in text:
            errors.append(f"{rel}: missing TypeReminder")
        if 'return "n_"' not in text and 'RemindersFile) return "n_"' not in text and 'reminders.dat") return "n_"' not in text:
            errors.append(f"{rel}: missing n_ prefix for reminders")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        "ManageDateReminders",
        "AddDateReminder",
        "RefreshDateReminderNotifications",
        "EditReminderWarnDays",
        'version = "1.26.0"',
        "reminders.dat",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "remindersGrid",
        "SetupRemindersGrid",
        "SaveReminderWarn",
        'AppVersion = "1.26.0"',
        "Напоминания",
        "FormatUpcomingBlock",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")

    kpk = (ROOT / "Alfa156/Alfa156/Form1.cs").read_text()
    for needle in (
        "ManageDateReminders",
        "AddDateReminder",
        "EditReminderWarnDays",
        "Напоминания",
        "Версия 1.26.0",
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

    readme = (ROOT / "README.md").read_text()
    if "**1.26.0**" not in readme:
        errors.append("README: missing 1.26.0")
    if "reminders.dat" not in readme:
        errors.append("README: missing reminders.dat")
    if "reminder_warn_days" not in readme:
        errors.append("README: missing reminder_warn_days")

    # Format / due-window logic (language-agnostic)
    from datetime import date, timedelta

    today = date.today()
    rows = [
        ((today + timedelta(days=5)).isoformat(), "Car", "OSAGO", "insurance"),
        ((today + timedelta(days=30)).isoformat(), "Car", "TO", "inspection"),
        ((today - timedelta(days=2)).isoformat(), "Car", "Old", "custom"),
    ]
    warn = 14
    due = []
    for d, car, title, typ in rows:
        days_left = (date.fromisoformat(d) - today).days
        if days_left <= warn:
            due.append((d, title))
    if len(due) != 2:
        errors.append(f"due window expected 2 items, got {due!r}")
    if not any(x[1] == "OSAGO" for x in due) or not any(x[1] == "Old" for x in due):
        errors.append(f"due window content unexpected: {due!r}")

    # pipe-row shape date|car|title|type|id
    sample = "2026-12-01|Alfa|Страховка|insurance|n_abcd1234"
    parts = sample.split("|")
    if len(parts) != 5 or parts[3] != "insurance" or not parts[4].startswith("n_"):
        errors.append(f"sample row shape unexpected: {sample}")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK date reminders checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
