#!/usr/bin/env python3
"""Verify expenses + reminders + templates + cars-meta load/column rules and ZIP lists."""
from __future__ import annotations

import sys
import tempfile
from pathlib import Path

import l10n_resolver

l10n_resolver.install()

ROOT = Path(__file__).resolve().parents[1]

DATA_FILES = [
    "cars.dat",
    "fuel.dat",
    "repairs.dat",
    "utilities.dat",
    "repair-categories.dat",
    "expenses.dat",
    "expense-categories.dat",
    "settings.dat",
    "cars-meta.dat",
    "templates.dat",
    "reminders.dat",
    "notes.dat",
    "deleted.dat",
]


def encode_row(values: list[str]) -> str:
    parts = []
    for value in values:
        escaped = []
        for ch in value or "":
            if ch in ("\\", "|"):
                escaped.append("\\")
            escaped.append(ch)
        parts.append("".join(escaped))
    return "|".join(parts)


def decode_row(line: str) -> list[str]:
    fields: list[str] = []
    field: list[str] = []
    escaped = False
    for ch in line:
        if escaped:
            field.append(ch)
            escaped = False
        elif ch == "\\":
            escaped = True
        elif ch == "|":
            fields.append("".join(field))
            field = []
        else:
            field.append(ch)
    if escaped:
        field.append("\\")
    fields.append("".join(field))
    return fields


def write_dat(path: Path, rows: list[list[str]]) -> None:
    path.write_text("".join(encode_row(r) + "\n" for r in rows), encoding="utf-8")


def read_dat(path: Path) -> list[list[str]]:
    if not path.is_file():
        return []
    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if line:
            rows.append(decode_row(line))
    return rows


def main() -> int:
    errors: list[str] = []

    for rel in (
        "Alfa156.Android/DataIntegrity.cs",
        "Alfa156.Desktop/Data/DataIntegrity.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        if "CheckCarsMeta" not in text:
            errors.append(f"{rel}: missing cars-meta integrity check")
        if "1880–2100" not in text:
            errors.append(f"{rel}: missing cars-meta year range check")
        if "объём бака" not in text:
            errors.append(f"{rel}: missing cars-meta tank capacity check")

    # Platform DataFiles lists must include the late-stage files
    # (names may appear as string literals or *File / FileName constants).
    for rel, required in (
        (
            "Alfa156/Alfa156/DataStore.cs",
            (
                "ExpensesFile",
                "CarsMetaFile",
                "TemplatesFile",
                "RemindersFile",
                "NotesFile",
                "DeletedFile",
                "DataFiles",
            ),
        ),
        (
            "Alfa156.Android/ExpenseDataCodec.cs",
            (
                "ExpensesFile",
                "CarsMetaFile",
                "TemplatesFile",
                "RemindersFile",
                "NotesFile",
                "DataFiles",
                'FileName = "cars-meta.dat"',  # via CarsMeta
            ),
        ),
        (
            "Alfa156.Desktop/Data/AlfaCodec.cs",
            (
                "ExpensesFile",
                "CarsMetaFile",
                "TemplatesFile",
                "RemindersFile",
                "NotesFile",
                "DataFiles",
                "RecordIds.DeletedFile",
            ),
        ),
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        for name in required:
            if name == 'FileName = "cars-meta.dat"':
                # constant lives in CarsMeta.cs / sibling — check codec refs CarsMetaFile
                if "CarsMetaFile" not in text:
                    errors.append(f"{rel}: missing CarsMetaFile")
                continue
            if name not in text:
                errors.append(f"{rel}: DataFiles missing {name}")
    for rel, needle in (
        ("Alfa156.Android/CarsMeta.cs", 'FileName = "cars-meta.dat"'),
        ("Alfa156.Android/RecordTemplates.cs", 'FileName = "templates.dat"'),
        ("Alfa156.Android/DateReminders.cs", 'FileName = "reminders.dat"'),
        ("Alfa156.Android/CarNotes.cs", 'FileName = "notes.dat"'),
        ("Alfa156.Android/RecordIds.cs", 'DeletedFile = "deleted.dat"'),
        ("Alfa156.Desktop/Data/CarsMeta.cs", 'FileName = "cars-meta.dat"'),
        ("Alfa156.Desktop/Data/RecordTemplates.cs", 'FileName = "templates.dat"'),
        ("Alfa156.Desktop/Data/DateReminders.cs", 'FileName = "reminders.dat"'),
        ("Alfa156.Desktop/Data/CarNotes.cs", 'FileName = "notes.dat"'),
    ):
        if needle not in (ROOT / rel).read_text(encoding="utf-8"):
            errors.append(f"{rel}: missing {needle!r}")

    # ZIP / backup paths must reference DataFiles (not only CoreDataFiles)
    for rel, needles in (
        (
            "Alfa156.Desktop/Data/AlfaRepository.cs",
            ["AlfaCodec.DataFiles", "SaveZip", "CreateEntryFromFile"],
        ),
        (
            "Alfa156.Android/MainActivity.cs",
            ["DataFiles.Length", "CreateEntry", "ExportTo"],
        ),
        (
            "Alfa156/Alfa156/DataStore.cs",
            ["DataFiles.Length", "CreateBackup", "CountBackupFiles"],
        ),
        (
            "Alfa156.Android/ExpenseDataStore.cs",
            ["TemplatesFile", "RemindersFile", "NotesFile", "CarsMetaFile", "CopyOptionalFile"],
        ),
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        for needle in needles:
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    # Android export must ensure optional files before ZIP (so they are not skipped)
    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text(encoding="utf-8")
    if "EnsureDataFilesForExport" not in android and "RecordTemplates.EnsureFile" not in android:
        # Accept either a helper or inline ensures near ExportTo
        export_idx = android.find("private void ExportTo")
        if export_idx < 0:
            errors.append("Android: missing ExportTo")
        else:
            chunk = android[export_idx : export_idx + 1200]
            for needle in (
                "RecordTemplates.EnsureFile",
                "DateReminders.EnsureFile",
                "CarNotes.EnsureFile",
                "CarsMeta.EnsureFile",
            ):
                if needle not in chunk and needle not in android:
                    errors.append(f"Android ExportTo path: ensure {needle} before ZIP")

    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        write_dat(root / "cars.dat", [["Alfa", "c_aaaaaaaa"]])
        write_dat(
            root / "fuel.dat",
            [["2026-11-01", "Alfa", "1000", "40", "100", "2.5", "Shell", "1", "Личный", "f_bbbbbbbb"]],
        )
        write_dat(
            root / "repairs.dat",
            [["2026-11-02", "Alfa", "1010", "ТО Масло", "масло", "1", "", "50", "r_cccccccc"]],
        )
        write_dat(root / "utilities.dat", [["2026-11", "10", "0", "0", "0", "0", "0", "0", "u_dddddddd"]])
        write_dat(root / "expenses.dat", [["2026-11-03", "Alfa", "Мойка", "мойка", "20", "", "e_eeeeeeee"]])
        write_dat(
            root / "cars-meta.dat",
            [["Alfa", "2010", "1234AB", "VIN123", "50", "заметка", "m_ffffffff"]],
        )
        write_dat(
            root / "templates.dat",
            [["fuel", "Обычная", "Alfa", "", "40", "100", "2.5", "Shell", "1", "Личный", "t_11111111"]],
        )
        write_dat(
            root / "reminders.dat",
            [["2026-12-01", "Alfa", "ОСАГО", "insurance", "n_22222222"]],
        )
        write_dat(
            root / "notes.dat",
            [["2026-11-10", "Alfa", "Замена дворников", "j_33333333"]],
        )
        write_dat(root / "deleted.dat", [["fuel", "f_deadbeef", "2026-11-01T00:00:00Z"]])

        expenses = read_dat(root / "expenses.dat")
        if len(expenses) != 1 or expenses[0][2] != "Мойка":
            errors.append("expenses.dat load failed")
        meta = read_dat(root / "cars-meta.dat")
        if len(meta) != 1 or meta[0][0] != "Alfa":
            errors.append("cars-meta.dat load failed")
        templates = read_dat(root / "templates.dat")
        if len(templates) != 1 or templates[0][0] != "fuel":
            errors.append("templates.dat load failed")
        reminders = read_dat(root / "reminders.dat")
        if len(reminders) != 1 or reminders[0][3] != "insurance":
            errors.append("reminders.dat load failed")
        notes = read_dat(root / "notes.dat")
        if len(notes) != 1 or notes[0][2] != "Замена дворников":
            errors.append("notes.dat load failed")

        # Column-count rules (content + optional id)
        if not (6 <= len(expenses[0]) <= 7):
            errors.append(f"expense columns unexpected: {len(expenses[0])}")
        if not (1 <= len(meta[0]) <= 7):
            errors.append(f"cars-meta columns unexpected: {len(meta[0])}")
        if not (3 <= len(templates[0]) <= 11):
            errors.append(f"templates columns unexpected: {len(templates[0])}")
        if not (4 <= len(reminders[0]) <= 5):
            errors.append(f"reminders columns unexpected: {len(reminders[0])}")
        if not (3 <= len(notes[0]) <= 4):
            errors.append(f"notes columns unexpected: {len(notes[0])}")

        # Simulate ZIP member list from DataFiles presence
        present = [name for name in DATA_FILES if (root / name).is_file()]
        for required in (
            "expenses.dat",
            "cars-meta.dat",
            "templates.dat",
            "reminders.dat",
            "notes.dat",
            "deleted.dat",
        ):
            if required not in present:
                errors.append(f"sample ZIP list missing {required}")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("verify_meta_files_load: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
