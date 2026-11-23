#!/usr/bin/env python3
"""Sanity-check record templates + default_car (1.25.0)."""
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
        ok = major > 1 or (major == 1 and minor >= 25)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.25.0, got {version!r}")

    for rel in (
        "KiloLedger/KiloLedger/RecordTemplates.cs",
        "KiloLedger.Android/RecordTemplates.cs",
        "KiloLedger.Desktop/Data/RecordTemplates.cs",
    ):
        text = (ROOT / rel).read_text()
        for needle in (
            'FileName = "templates.dat"',
            "TypeFuel",
            "TypeRepair",
            "TypeExpense",
            "PayloadFromEntity",
            "BuildEntityRow",
            "IsValidRow",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")
        if "не более 64 символов" not in text:
            errors.append(f"{rel}: template names must not be silently truncated")

    for rel in (
        "KiloLedger/KiloLedger/ToSettings.cs",
        "KiloLedger.Android/ToSettings.cs",
        "KiloLedger.Desktop/Data/ToSettings.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'DefaultCarKey = "default_car"' not in text:
            errors.append(f"{rel}: missing DefaultCarKey")
        if "GetDefaultCar" not in text or "SetDefaultCar" not in text:
            errors.append(f"{rel}: missing GetDefaultCar/SetDefaultCar")
        if "ResolvePreferredCar" not in text:
            errors.append(f"{rel}: missing ResolvePreferredCar")

    for rel in (
        "KiloLedger/KiloLedger/RecordIds.cs",
        "KiloLedger.Android/RecordIds.cs",
        "KiloLedger.Desktop/Data/RecordIds.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'TypeTemplate = "template"' not in text:
            errors.append(f"{rel}: missing TypeTemplate")
        if 'return "t_"' not in text and "templates.dat\") return \"t_\"" not in text:
            if 'TemplatesFile) return "t_"' not in text and 'templates.dat") return "t_"' not in text:
                errors.append(f"{rel}: missing t_ prefix for templates")

    android = (ROOT / "KiloLedger.Android/MainActivity.cs").read_text()
    for needle in (
        "Из шаблона",
        "Сохранить как шаблон",
        "AddRecordFromTemplate",
        "EditDefaultCar",
        "Авто по умолчанию",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")
    if f'version = "{version}"' not in android:
        errors.append(f"Android MainActivity: missing version {version}")

    desktop = (ROOT / "KiloLedger.Desktop/MainForm.cs").read_text()
    for needle in (
        "ApplyTemplateToActiveGrid",
        "SaveActiveRowAsTemplate",
        "SaveDefaultCar",
        "Из шаблона",
        "Сохранить как шаблон",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle!r}")
    if f'AppVersion = "{version}"' not in desktop:
        errors.append(f"Desktop MainForm: missing AppVersion {version}")

    kpk = (ROOT / "KiloLedger/KiloLedger/Form1.cs").read_text()
    for needle in (
        "ManageTemplatesFromMenu",
        "EditDefaultCar",
        "Шаблоны",
        "Авто по умолчанию",
        "Избранный",
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle!r}")

    for rel, needle in (
        ("KiloLedger.Android/ExpenseDataCodec.cs", "TemplatesFile"),
        ("KiloLedger.Desktop/Data/KiloLedgerCodec.cs", "TemplatesFile"),
        ("KiloLedger/KiloLedger/DataStore.cs", "TemplatesFile"),
        ("KiloLedger.Android/ExpenseSyncService.cs", "TemplatesFile"),
        ("KiloLedger/KiloLedger/SyncMerge.cs", "templates.dat"),
    ):
        if needle not in (ROOT / rel).read_text():
            errors.append(f"{rel}: missing {needle!r}")

    readme = (ROOT / "README.ru.md").read_text()
    if "**1.25.0**" not in readme:
        errors.append("README: missing 1.25.0")
    if "templates.dat" not in readme:
        errors.append("README: missing templates.dat")
    if "default_car" not in readme:
        errors.append("README: missing default_car")

    # Payload length rules (fuel includes station as of 1.32.0)
    fuel_payload_max, repair_payload_max, expense_payload_max = 9, 8, 5
    if not (fuel_payload_max == 9 and repair_payload_max == 8 and expense_payload_max == 5):
        errors.append("payload max lengths unexpected")
    for rel in (
        "KiloLedger.Android/RecordTemplates.cs",
        "KiloLedger.Desktop/Data/RecordTemplates.cs",
        "KiloLedger/KiloLedger/RecordTemplates.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        if "payloadLen > 9" not in text and "len > 9" not in text:
            errors.append(f"{rel}: expected fuel payload max 9")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("OK templates + default_car checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
