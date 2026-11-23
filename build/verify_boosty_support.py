#!/usr/bin/env python3
"""About/support screens link to the Boosty page, or show the URL on the handheld."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
URL = "https://boosty.to/kiloledger"
KEY = "button.support_boosty"
LABELS = {
    "ru": "Поддержать на Boosty",
    "en": "Support on Boosty",
    "de": "Auf Boosty unterstützen",
    "es": "Apoyar en Boosty",
    "fr": "Soutenir sur Boosty",
}
STRING_ROOTS = (
    "localization/resources",
    "KiloLedger.Desktop/resources",
    "KiloLedger.Android/Assets",
    "KiloLedger/KiloLedger/resources",
)


def main() -> int:
    errors: list[str] = []
    desktop = (ROOT / "KiloLedger.Desktop/MainForm.cs").read_text(encoding="utf-8")
    android = (ROOT / "KiloLedger.Android/MainActivity.cs").read_text(encoding="utf-8")
    kpk = (ROOT / "KiloLedger/KiloLedger/Form1.cs").read_text(encoding="utf-8")

    for name, text in (("Desktop", desktop), ("Android", android), ("KPK", kpk)):
        if f'BoostySupportUrl = "{URL}"' not in text:
            errors.append(f"{name}: missing Boosty URL constant")
        if KEY not in text:
            errors.append(f"{name}: missing {KEY}")

    if desktop.count("OpenExternalUrl(BoostySupportUrl)") < 2:
        errors.append("Desktop: Boosty URL is not opened from settings and About")
    if "OpenExternalUrl(BoostySupportUrl)" not in android:
        errors.append("Android: About does not open the Boosty URL")
    about = kpk.split("private void ShowAbout", 1)[1].split("private void ExportSummaryFromMenu", 1)[0]
    if "MessageBox.Show(BoostySupportUrl" not in about:
        errors.append("KPK: About does not show the Boosty URL as text")
    if "Process.Start" in about or f'FileName = "{URL}"' in about:
        errors.append("KPK: About must not launch a browser for Boosty")

    for root in STRING_ROOTS:
        for lang, label in LABELS.items():
            path = ROOT / root / f"strings.{lang}.json"
            if not path.is_file():
                errors.append(f"missing {path.relative_to(ROOT)}")
                continue
            data = json.loads(path.read_text(encoding="utf-8"))
            if data.get(KEY) != label:
                errors.append(f"{path.relative_to(ROOT)}: {KEY} is not {label!r}")

    if errors:
        print("FAIL verify_boosty_support:")
        for err in errors:
            print(" ", err)
        return 1
    print("verify_boosty_support: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
