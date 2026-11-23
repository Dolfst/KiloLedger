#!/usr/bin/env python3
"""Fail if KPK sources use .NET CF-incompatible APIs (preemptive CI guard)."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
KPK = ROOT / "KiloLedger" / "KiloLedger"

CHECKS = [
    (re.compile(r"DateTime\.TryParseExact\s*\("), "DateTime.TryParseExact (not in CF)"),
    (re.compile(r"ToUpperInvariant\s*\("), "ToUpperInvariant (not in CF; use ToUpper(CultureInfo))"),
    (
        re.compile(r"\b(?:int|long|byte|short)\.TryParse\s*\([^;]*NumberStyles"),
        "int/long.TryParse with NumberStyles (CF has only TryParse(string,out))",
    ),
    (
        re.compile(r"\bDecimal\.TryParse\s*\("),
        "Decimal.TryParse (use DataStore.TryNumber / NumberOrZero)",
    ),
    (
        re.compile(r"\b(?:double|float)\.TryParse\s*\("),
        "double/float.TryParse (prefer manual parse helpers)",
    ),
    (re.compile(r"Enum\.TryParse\s*\("), "Enum.TryParse (not in CF)"),
    (re.compile(r"Guid\.TryParse\s*\("), "Guid.TryParse (not in CF)"),
]


def main() -> int:
    errors: list[str] = []
    files = sorted(KPK.glob("*.cs"))
    if not files:
        print("FAIL: no KPK .cs files")
        return 1
    for path in files:
        for i, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if line.strip().startswith("//"):
                continue
            for pattern, message in CHECKS:
                if pattern.search(line):
                    errors.append(f"{path.relative_to(ROOT)}:{i}: {message}")
    if errors:
        print("FAIL CF-incompatible APIs:")
        for e in errors:
            print(" ", e)
        return 1
    print(f"verify_cf_apis: OK ({len(files)} files)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
