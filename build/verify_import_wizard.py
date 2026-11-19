#!/usr/bin/env python3
"""Sanity-check import wizard (1.27.0)."""
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
        ok = major > 1 or (major == 1 and minor >= 27)
    except (ValueError, IndexError):
        ok = False
    if not ok:
        errors.append(f"VERSION expected >= 1.27.0, got {version!r}")

    preview = ROOT / "Alfa156.Desktop/Exchange/ImportPreview.cs"
    if not preview.is_file():
        errors.append("missing ImportPreview.cs")
    else:
        text = preview.read_text()
        for needle in (
            "FromZip",
            "FromDatFolder",
            "FromCsvFolder",
            "FromExcel",
            "FormatSummary",
            "ResolveDataRoot",
        ):
            if needle not in text:
                errors.append(f"ImportPreview.cs: missing {needle!r}")

    wizard = ROOT / "Alfa156.Desktop/ImportWizardForm.cs"
    if not wizard.is_file():
        errors.append("missing ImportWizardForm.cs")
    else:
        text = wizard.read_text()
        for needle in (
            'Text = "Импорт"',
            "SourceKind",
            "BuildStepChoose",
            "BuildStepPath",
            "BuildStepPreview",
            "ApplyImport",
            "CreateSafetyBackup",
            "ExportTemplatesClicked",
            "Применить",
        ):
            if needle not in text:
                errors.append(f"ImportWizardForm.cs: missing {needle!r}")

    csv = (ROOT / "Alfa156.Desktop/Exchange/CsvExchange.cs").read_text()
    if "ExportTemplates" not in csv:
        errors.append("CsvExchange.cs: missing ExportTemplates")
    for needle in ("FuelHeaders", "RepairHeaders", "ExpenseHeaders"):
        if needle not in csv:
            errors.append(f"CsvExchange.cs: missing {needle!r}")

    csproj = (ROOT / "Alfa156.Desktop/Alfa156.Desktop.csproj").read_text()
    if "ImportWizardForm.cs" not in csproj:
        errors.append("Desktop csproj: missing ImportWizardForm.cs")
    if "ImportPreview.cs" not in csproj:
        errors.append("Desktop csproj: missing ImportPreview.cs")

    mainform = (ROOT / "Alfa156.Desktop/MainForm.cs").read_text()
    for needle in (
        'AppVersion = "1.27.0"',
        "OpenImportWizard",
        "ExportCsvTemplates",
        'MakeButton("Импорт…"',
        'MakeButton("Шаблоны CSV"',
    ):
        if needle not in mainform:
            errors.append(f"Desktop MainForm: missing {needle!r}")

    android = (ROOT / "Alfa156.Android/MainActivity.cs").read_text()
    for needle in (
        'version = "1.27.0"',
        'SetTitle("Импорт копии")',
        "Предпросмотр импорта",
        "Прочие расходы:",
        "ExpenseDataCodec.TemplatesFile",
        "ExpenseDataCodec.RemindersFile",
        "Backups/Safety_",
    ):
        if needle not in android:
            errors.append(f"Android MainActivity: missing {needle!r}")

    display = (ROOT / "Alfa156.Android/Alfa156.Android.csproj").read_text()
    if "<ApplicationDisplayVersion>1.27.0</ApplicationDisplayVersion>" not in display:
        errors.append("Android csproj: ApplicationDisplayVersion not 1.27.0")
    if "<ApplicationVersion>27</ApplicationVersion>" not in display:
        errors.append("Android csproj: ApplicationVersion not 27")

    asm = (ROOT / "Alfa156.Desktop/Properties/AssemblyInfo.cs").read_text()
    if 'AssemblyVersion("1.27.0.0")' not in asm:
        errors.append("Desktop AssemblyInfo: not 1.27.0.0")

    readme = (ROOT / "README.md").read_text()
    if "1.27.0" not in readme or "Мастер «Импорт»" not in readme:
        errors.append("README.md: missing 1.27.0 import wizard notes")

    if errors:
        for err in errors:
            print("FAIL:", err)
        return 1
    print("OK: import wizard 1.27.0 checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
