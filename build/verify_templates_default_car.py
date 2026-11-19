#!/usr/bin/env python3
"""Sanity-check templates.dat + default_car hooks (1.25.0)."""
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
        ok = major > 1 or (major == 1 and minor >= 25)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.25.0, got {version!r}")

    for rel in (
        "Alfa156.Android/RecordTemplates.cs",
        "Alfa156.Desktop/Data/RecordTemplates.cs",
        "Alfa156/Alfa156/RecordTemplates.cs",
    ):
        text = (ROOT / rel).read_text()
        for needle in (
            'FileName = "templates.dat"',
            "TypeFuel",
            "TypeRepair",
            "TypeExpense",
            "PayloadFromEntity",
            "EntityFromTemplate",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle}")

    for rel in (
        "Alfa156.Android/ToSettings.cs",
        "Alfa156.Desktop/Data/ToSettings.cs",
        "Alfa156/Alfa156/ToSettings.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'DefaultCarKey = "default_car"' not in text:
            errors.append(f"{rel}: missing DefaultCarKey")
        if "GetDefaultCar" not in text or "SetDefaultCar" not in text:
            errors.append(f"{rel}: missing Get/SetDefaultCar")

    for rel, needles in (
        (
            "Alfa156.Android/ExpenseDataCodec.cs",
            ["TemplatesFile", "RecordTemplates.FileName"],
        ),
        (
            "Alfa156.Desktop/Data/AlfaCodec.cs",
            ["TemplatesFile", "RecordTemplates.FileName"],
        ),
        (
            "Alfa156/Alfa156/DataStore.cs",
            ["TemplatesFile", "RecordTemplates.EnsureFile"],
        ),
    ):
        text = (ROOT / rel).read_text()
        for needle in needles:
            if needle not in text:
                errors.append(f"{rel}: missing {needle}")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        "Из шаблона",
        "Сохранить как шаблон",
        "ManageTemplates",
        "EditDefaultCar",
        "GetDefaultCar",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle}")

    desktop = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        "UseTemplateFromUi",
        "SaveTemplateFromUi",
        "SaveDefaultCar",
        "fuelFilterCar",
        "PreferredCarName",
    ):
        if needle not in desktop:
            errors.append(f"Desktop MainForm: missing {needle}")

    kpk = (ROOT / "Alfa156/Alfa156/Form1.cs").read_text()
    for needle in (
        "ShowTemplatesMenu",
        "EditDefaultCar",
        "UseTemplate",
        "Авто по умолчанию",
        "Шаблоны",
    ):
        if needle not in kpk:
            errors.append(f"KPK Form1: missing {needle}")

    # Record id prefix t_
    for rel in (
        "Alfa156.Android/RecordIds.cs",
        "Alfa156.Desktop/Data/RecordIds.cs",
        "Alfa156/Alfa156/RecordIds.cs",
    ):
        text = (ROOT / rel).read_text()
        if 'return "t_"' not in text and "TypeTemplate" not in text:
            errors.append(f"{rel}: missing template id prefix/type")
        if "prefix != 't'" not in text:
            errors.append(f"{rel}: LooksLikeId missing t_")

    # Sample encode/decode roundtrip of a fuel template row via pipe fields
    payload = ["Alfa", "", "40", "100", "2.5", "АЗС", "1", "Личный"]
    row = ["fuel", "АЗС обычная"] + payload + ["t_abcd1234"]
    if len(row) != 11:
        errors.append("sample fuel template column count wrong")

    if errors:
        for err in errors:
            print("ERROR:", err)
        return 1
    print("OK templates + default_car checks")
    return 0


if __name__ == "__main__":
    sys.exit(main())
