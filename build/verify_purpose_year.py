#!/usr/bin/env python3
"""Offline checks for 1.19.0 fuel purpose field layout and year totals."""
from __future__ import annotations

import re
from datetime import datetime

ID_RE = re.compile(r"^[cfrukepxmnt]_[0-9a-fA-F]{8,}$")


def looks_like_id(value: str) -> bool:
    return bool(value and ID_RE.match(value))


def strip_id(row: list[str]) -> list[str]:
    return row[:-1] if row and looks_like_id(row[-1]) else list(row)


def get_purpose(row: list[str]) -> str:
    content = strip_id(row)
    if len(content) <= 8:
        return ""
    return (content[8] or "").strip()[:32]


def is_valid_fuel_count(length: int) -> bool:
    return 5 <= length <= 10


def year_total(rows: list[list[str]], amount_idx: int, year: str) -> float:
    total = 0.0
    for row in rows:
        if len(row) <= amount_idx:
            continue
        if not (row[0] or "").startswith(year):
            continue
        try:
            total += float((row[amount_idx] or "0").replace(",", "."))
        except ValueError:
            pass
    return total


def main() -> None:
    old = ["2024-01-01", "Car", "1000", "40", "100", "2.5", "AZS", "1", "f_abcd1234"]
    assert is_valid_fuel_count(len(old))
    assert get_purpose(old) == ""

    with_purpose = [
        "2026-03-01", "Car", "1100", "40", "120", "3", "AZS", "1", "Личный", "f_abcd1234"
    ]
    assert is_valid_fuel_count(len(with_purpose))
    assert get_purpose(with_purpose) == "Личный"
    assert strip_id(with_purpose)[7] == "1"
    assert strip_id(with_purpose)[8] == "Личный"

    custom = ["2026-04-01", "Car", "1200", "10", "30", "", "", "0", "Командировка"]
    assert get_purpose(custom) == "Командировка"
    assert is_valid_fuel_count(len(custom) + 1)  # +id

    year = str(datetime.now().year)
    fuels = [
        [f"{year}-01-15", "Car", "1", "1", "10"],
        ["2019-01-15", "Car", "1", "1", "999"],
        [f"{year}-06-01", "Car", "1", "1", "5", "", "", "1", "Работа"],
    ]
    assert year_total(fuels, 4, year) == 15.0

    personal = work = 0.0
    for row in fuels:
        purpose = get_purpose(row)
        amt = float(row[4])
        if purpose == "Личный":
            personal += amt
        elif purpose == "Работа":
            work += amt
    assert personal == 0.0 and work == 5.0
    print("verify_purpose_year: OK")


if __name__ == "__main__":
    main()
