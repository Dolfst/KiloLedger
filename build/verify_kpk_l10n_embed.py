#!/usr/bin/env python3
"""KPK csproj and build-kpk scripts must embed Localization.cs + strings.*.json."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CSPROJ = ROOT / "KiloLedger" / "KiloLedger" / "KiloLedger.csproj"
RES_DIR = ROOT / "KiloLedger" / "KiloLedger" / "resources"
BUILD_SH = ROOT / "build" / "build-kpk.sh"
BUILD_PS1 = ROOT / "build" / "build-kpk.ps1"
LANGS = ("ru", "en", "es", "fr", "de")


def main() -> int:
    errors: list[str] = []
    if not CSPROJ.is_file():
        print(f"FAIL: missing {CSPROJ.relative_to(ROOT)}")
        return 1

    csproj = CSPROJ.read_text(encoding="utf-8")
    if '<Compile Include="Localization.cs" />' not in csproj:
        errors.append("KiloLedger.csproj: missing <Compile Include=\"Localization.cs\" />")

    for lang in LANGS:
        name = f"strings.{lang}.json"
        disk = RES_DIR / name
        if not disk.is_file():
            errors.append(f"missing disk file {disk.relative_to(ROOT)}")

        include = f'resources\\strings.{lang}.json'
        # Allow either escaped backslash as written in csproj or forward slash.
        has_include = (
            f'<EmbeddedResource Include="{include}">' in csproj
            or f'<EmbeddedResource Include="resources/strings.{lang}.json">' in csproj
        )
        if not has_include:
            errors.append(f"KiloLedger.csproj: missing EmbeddedResource for {name}")
        elif f"<LogicalName>{name}</LogicalName>" not in csproj:
            errors.append(f"KiloLedger.csproj: EmbeddedResource {name} lacks LogicalName={name}")
        else:
            # Ensure LogicalName is nested under the matching EmbeddedResource block.
            block = re.search(
                rf'<EmbeddedResource Include="resources[\\/]strings\.{re.escape(lang)}\.json">\s*'
                rf"(.*?)\s*</EmbeddedResource>",
                csproj,
                re.DOTALL,
            )
            if block is None or f"<LogicalName>{name}</LogicalName>" not in block.group(1):
                errors.append(
                    f"KiloLedger.csproj: LogicalName {name} not inside its EmbeddedResource block"
                )

    for script, label in ((BUILD_SH, "build-kpk.sh"), (BUILD_PS1, "build-kpk.ps1")):
        if not script.is_file():
            errors.append(f"missing {script.relative_to(ROOT)}")
            continue
        text = script.read_text(encoding="utf-8")
        for lang in LANGS:
            name = f"strings.{lang}.json"
            if name not in text:
                errors.append(f"{label}: missing string table name {name}")

    loc = ROOT / "KiloLedger" / "KiloLedger" / "Localization.cs"
    if not loc.is_file():
        errors.append(f"missing {loc.relative_to(ROOT)}")

    if errors:
        print("FAIL verify_kpk_l10n_embed:")
        for err in errors:
            print(" ", err)
        return 1
    print(
        "verify_kpk_l10n_embed: OK "
        f"(Localization.cs + {len(LANGS)} EmbeddedResource string tables in csproj; "
        "build-kpk.sh/ps1 list them)"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
