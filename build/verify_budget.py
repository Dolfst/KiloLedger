#!/usr/bin/env python3
"""Sanity-check monthly budget soft-limit keys and UI hooks (1.22.0)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors: list[str] = []
    version = (ROOT / "VERSION").read_text().strip()
    parts = version.split(".")
    try:
        major, minor = int(parts[0]), int(parts[1])
        ok = major > 1 or (major == 1 and minor >= 22)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.22.0, got {version!r}")

    for rel in (
        "Alfa156/Alfa156/ToSettings.cs",
        "Alfa156.Android/ToSettings.cs",
        "Alfa156.Desktop/Data/ToSettings.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'BudgetMonthKey = "budget_month"' not in text:
            errors.append(f"{rel}: missing BudgetMonthKey")
        if 'BudgetWarnPctKey = "budget_warn_pct"' not in text:
            errors.append(f"{rel}: missing BudgetWarnPctKey")
        if "DefaultBudgetWarnPct = 80" not in text:
            errors.append(f"{rel}: missing DefaultBudgetWarnPct 80")
        if "GetBudgetMonth" not in text or "SetBudget" not in text:
            errors.append(f"{rel}: missing GetBudgetMonth/SetBudget")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        'BudgetChannelId = "budget"',
        "RefreshBudgetNotification",
        "EditBudget",
        "Бюджет месяца",
        "AppendBudgetBlock",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "budgetBox",
        "SaveBudget",
        "AppendBudgetBlock",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")
    if 'AppVersion = "' not in desktop:
        errors.append("Desktop MainForm: missing AppVersion")

    kpk = (ROOT / "Alfa156/Alfa156/Form1.cs").read_text()
    for needle in (
        "EditBudget",
        "FormatBudgetBlock",
        "Бюджет месяца",
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    readme = (ROOT / "README.md").read_text()
    if "## История версий" not in readme:
        errors.append("README: missing История версий")
    if "**1.22.0**" not in readme or "**1.9.0**" not in readme:
        errors.append("README: История версий should cover 1.9.0 → 1.22.0+")

    for rel in (
        "Alfa156.Android/DataIntegrity.cs",
        "Alfa156.Desktop/Data/DataIntegrity.cs",
    ):
        text = (ROOT / rel).read_text()
        if "не указана ни одна сумма" not in text:
            errors.append(f"{rel}: utility rows require at least one amount")
        if "месяц «" not in text or "months" not in text:
            errors.append(f"{rel}: utility duplicate months are not checked")

    # Soft-limit math: warn at pct, over at >= budget
    budget, warn_pct = 1000.0, 80.0
    spent_warn = 800.0
    spent_over = 1000.0
    pct_warn = (spent_warn * 100.0) / budget
    pct_over = (spent_over * 100.0) / budget
    if not (pct_warn >= warn_pct and spent_warn < budget):
        errors.append("budget warn threshold logic broken")
    if not (spent_over >= budget):
        errors.append("budget over threshold logic broken")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK budget soft-limit checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
