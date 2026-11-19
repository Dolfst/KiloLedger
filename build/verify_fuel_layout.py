#!/usr/bin/env python3
"""Verify fuel.dat field layout: date|car|km|liters|cost|price|comment|fullTank|purpose|station|id."""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ID_RE = re.compile(r"^[cfrukepxmnt]_[0-9a-fA-F]{8,}$")


def looks_like_id(value: str) -> bool:
    return bool(value and ID_RE.match(value))


def strip_id(row: list[str]) -> list[str]:
    return row[:-1] if row and looks_like_id(row[-1]) else list(row)


def is_valid_fuel_len(n: int) -> bool:
    return 5 <= n <= 11


def is_full_tank(row: list[str]) -> bool:
    content = strip_id(row)
    if len(content) <= 7:
        return True
    flag = (content[7] or "").strip()
    return flag == "" or flag == "1"


def get_purpose(row: list[str]) -> str:
    content = strip_id(row)
    if len(content) <= 8:
        return ""
    return (content[8] or "").strip()[:32]


def get_station(row: list[str]) -> str:
    content = strip_id(row)
    if len(content) <= 9:
        return ""
    return (content[9] or "").strip()[:48]


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


def main() -> int:
    errors: list[str] = []

    # Layout contract across platforms
    for rel in (
        "Alfa156/Alfa156/DataStore.cs",
        "Alfa156.Android/ExpenseDataCodec.cs",
        "Alfa156.Desktop/Data/AlfaCodec.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        for needle in (
            "fullTank",
            "purpose",
            "station",
            "length >= 5 && length <= 11",
            "date|car|km|liters|cost",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    for rel in (
        "Alfa156.Android/ExpenseDataCodec.cs",
        "Alfa156.Desktop/Data/AlfaCodec.cs",
    ):
        text = (ROOT / rel).read_text(encoding="utf-8")
        for needle in (
            "IsFullTank",
            "GetPurpose",
            "NormalizePurpose",
            "GetStation",
            "NormalizeStation",
            "CollectStationSuggestions",
        ):
            if needle not in text:
                errors.append(f"{rel}: missing {needle!r}")

    # Desktop must keep purpose+station on normalize (not truncate at 8)
    repo = (ROOT / "Alfa156.Desktop/Data/AlfaRepository.cs").read_text(encoding="utf-8")
    if "if (length > 10)" not in repo and "length > 10" not in repo:
        errors.append("AlfaRepository.NormalizeFuelRow: expected max content length 10")
    if "NormalizeStation" not in repo:
        errors.append("AlfaRepository.NormalizeFuelRow: missing NormalizeStation")

    # Sample rows covering legacy → modern layouts
    samples = [
        (["2024-01-01", "Alfa", "100000", "40", "100"], True, "", ""),
        (["2024-01-01", "Alfa", "100000", "40", "100", "2.5", "Shell"], True, "", ""),
        (["2024-01-01", "Alfa", "100000", "40", "100", "2.5", "note", "1"], True, "", ""),
        (["2024-01-01", "Alfa", "100000", "20", "50", "2.5", "note", "0"], False, "", ""),
        (
            ["2024-01-01", "Alfa", "100000", "40", "100", "2.5", "note", "1", "Личный"],
            True,
            "Личный",
            "",
        ),
        (
            [
                "2024-01-01",
                "Alfa",
                "100000",
                "40",
                "100",
                "2.5",
                "note",
                "0",
                "Работа",
                "f_aabbccdd",
            ],
            False,
            "Работа",
            "",
        ),
        (
            [
                "2024-01-01",
                "Alfa",
                "100000",
                "40",
                "100",
                "2.5",
                "note",
                "1",
                "Личный",
                "Shell",
                "f_11223344",
            ],
            True,
            "Личный",
            "Shell",
        ),
    ]
    for row, expect_full, expect_purpose, expect_station in samples:
        if not is_valid_fuel_len(len(row)):
            errors.append(f"invalid length for sample {row}")
        if is_full_tank(row) != expect_full:
            errors.append(f"fullTank mismatch for {row}")
        if get_purpose(row) != expect_purpose:
            errors.append(f"purpose mismatch for {row}: {get_purpose(row)!r}")
        if get_station(row) != expect_station:
            errors.append(f"station mismatch for {row}: {get_station(row)!r}")
        content = strip_id(row)
        if looks_like_id(content[-1] if content else ""):
            errors.append(f"id leaked into content: {row}")

    # Escaping round-trip for purpose/station with pipes
    tricky = [
        "2024-01-01",
        "Alfa",
        "1",
        "1",
        "1",
        "",
        "A|B",
        "1",
        "Цель\\|X",
        "Shell|East",
        "f_11223344",
    ]
    encoded = encode_row(tricky)
    decoded = decode_row(encoded)
    if decoded != tricky:
        errors.append(f"encode/decode mismatch: {decoded}")
    if not looks_like_id(decoded[-1]):
        errors.append("id lost after round-trip")

    row2 = ["2024-01-01", "Alfa", "1", "1", "1", "", "note", "1", "A|B", "B|C", "f_99887766"]
    enc2 = encode_row(row2)
    dec2 = decode_row(enc2)
    if dec2 != row2:
        errors.append(f"pipe in purpose/station broke decode: {dec2}")
    if get_purpose(dec2) != "A|B":
        errors.append(f"purpose with pipe wrong: {get_purpose(dec2)!r}")
    if get_station(dec2) != "B|C":
        errors.append(f"station with pipe wrong: {get_station(dec2)!r}")

    # Old purpose+id row must not treat id as station
    old = ["2026-03-01", "Car", "1100", "40", "120", "3", "AZS", "1", "Личный", "f_abcd1234"]
    if get_station(old) != "":
        errors.append(f"legacy purpose+id mistaken for station: {get_station(old)!r}")
    if get_purpose(old) != "Личный":
        errors.append("legacy purpose broken")

    if errors:
        print("FAIL:")
        for e in errors:
            print(" ", e)
        return 1
    print("verify_fuel_layout: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
