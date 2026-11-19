#!/usr/bin/env python3
"""Offline checks for 1.13.0 fullTank + л/100км full→full rule."""
from __future__ import annotations

import re

ID_RE = re.compile(r"^[cfrukepx]_[0-9a-fA-F]{8,}$")


def looks_like_id(value: str) -> bool:
    return bool(value and ID_RE.match(value))


def strip_id(row: list[str]) -> list[str]:
    return row[:-1] if row and looks_like_id(row[-1]) else list(row)


def is_valid_fuel_len(n: int) -> bool:
    return 5 <= n <= 9


def is_full_tank(row: list[str]) -> bool:
    content = strip_id(row)
    if len(content) <= 7:
        return True
    flag = (content[7] or "").strip()
    return flag == "" or flag == "1"


def fuel_consumption(row: list[str], all_rows: list[list[str]]) -> float:
    if not is_full_tank(row):
        return 0.0
    mileage = float(row[2])
    idx = all_rows.index(row)
    for i in range(idx - 1, -1, -1):
        if all_rows[i][1] != row[1]:
            continue
        if not is_full_tank(all_rows[i]):
            continue
        prev = float(all_rows[i][2])
        liters = float(row[3])
        if mileage > prev:
            return liters * 100 / (mileage - prev)
    return 0.0


def main() -> None:
    assert is_valid_fuel_len(5) and is_valid_fuel_len(9) and not is_valid_fuel_len(4)
    assert is_valid_fuel_len(8)  # content with fullTank, no id yet

    old = ["2024-01-01", "Car", "10000", "40", "100", "2.5", "АЗС", "f_aabbccdd"]
    assert is_full_tank(old)
    assert len(strip_id(old)) == 7

    full = ["2024-01-10", "Car", "10500", "45", "110", "2.4", "АЗС", "1", "f_11111111"]
    partial = ["2024-01-05", "Car", "10200", "20", "50", "2.5", "АЗС", "0", "f_22222222"]
    assert is_full_tank(full) and not is_full_tank(partial)

    rows = [old, partial, full]
    # partial must not close an interval
    assert fuel_consumption(partial, rows) == 0.0
    # full→full skips the partial endpoint and uses previous full (old)
    cons = fuel_consumption(full, rows)
    assert abs(cons - (45 * 100 / 500)) < 1e-6, cons

    # missing flag = full
    bare = ["2024-02-01", "Car", "11000", "40", "100"]
    assert is_full_tank(bare)
    print("verify_full_tank: ok")


if __name__ == "__main__":
    main()
