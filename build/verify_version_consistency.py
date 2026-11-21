#!/usr/bin/env python3
"""Check that application display versions remain consistent."""

from __future__ import annotations

import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    parts = version.split(".")
    if len(parts) != 3:
        print("FAIL: invalid VERSION", version)
        return 1

    try:
        minor = int(parts[1])
    except ValueError:
        print("FAIL: invalid minor version", version)
        return 1

    checks = (
        (
            "Alfa156.Android/Alfa156.Android.csproj",
            (
                f"<ApplicationDisplayVersion>{version}</ApplicationDisplayVersion>",
                f"<ApplicationVersion>{minor}</ApplicationVersion>",
            ),
        ),
        ("Alfa156.Android/MainActivity.cs", (f'version = "{version}"',)),
        ("Alfa156.Desktop/MainForm.cs", (f'AppVersion = "{version}"',)),
        (
            "Alfa156.Desktop/Properties/AssemblyInfo.cs",
            (
                f'AssemblyVersion("{version}.0")',
                f'AssemblyFileVersion("{version}.0")',
            ),
        ),
        (
            "Alfa156/Alfa156/Properties/AssemblyInfo.cs",
            (f'AssemblyVersion("{version}.0")',),
        ),
        ("Alfa156/Alfa156/Form1.cs", (f"Версия {version}",)),
        ("README.md", (f"Текущая версия: **{version}**",)),
        ("dist/download/README.md", (f"сейчас `{version}`",)),
    )

    errors = []
    for relative, needles in checks:
        path = ROOT / relative
        if not path.is_file():
            errors.append(f"missing {relative}")
            continue
        text = path.read_text(encoding="utf-8-sig")
        for needle in needles:
            if needle not in text:
                errors.append(f"{relative}: missing {needle}")

    if errors:
        print("FAIL verify_version_consistency:")
        for error in errors:
            print(" ", error)
        return 1

    print(f"verify_version_consistency: OK ({version})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
